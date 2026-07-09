using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class OfferSheetService
{
    private readonly RfaUfaService _rights = new();
    private readonly SalaryCapService _salaryCap = new();

    public IReadOnlyList<OfferSheetEligibility> BuildEligibility(
        NewGmScenarioSnapshot scenario,
        Rulebook? rulebook = null,
        decimal annualSalary = 2_500_000m,
        int termYears = 2,
        IReadOnlyList<int>? ownedCompensationRounds = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = _rights.EnsureRights(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var rules = RulesFor(rulebook ?? prepared.LeagueProfile.Rulebook);
        var offering = DefaultOfferingTeam(prepared);
        var output = prepared.PlayerRightsDecisions
            .Where(decision => decision.RightsStatus is FreeAgentRightsStatus.Qualified or FreeAgentRightsStatus.RestrictedFreeAgent or FreeAgentRightsStatus.RightsHeld)
            .Select(decision => EvaluateEligibility(prepared, rules, decision, offering.OrganizationId, offering.TeamName, annualSalary, termYears, ownedCompensationRounds))
            .OrderByDescending(item => item.IsEligible)
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();

        foreach (var item in output)
        {
            item.Validate();
        }

        return output;
    }

    public OfferSheetEligibility EvaluateEligibility(
        NewGmScenarioSnapshot scenario,
        Rulebook? rulebook,
        string personId,
        string? offeringOrganizationId = null,
        string? offeringTeamName = null,
        decimal annualSalary = 2_500_000m,
        int termYears = 2,
        IReadOnlyList<int>? ownedCompensationRounds = null)
    {
        var prepared = _rights.EnsureRights(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var decision = prepared.PlayerRightsDecisions.FirstOrDefault(item => item.PersonId == personId);
        if (decision is null)
        {
            var team = DefaultOfferingTeam(prepared);
            return new OfferSheetEligibility(
                personId,
                PersonName(prepared, personId),
                PositionFor(prepared, personId),
                AgeFor(prepared, personId),
                prepared.Organization.OrganizationId,
                prepared.Organization.Name,
                offeringOrganizationId ?? team.OrganizationId,
                offeringTeamName ?? team.TeamName,
                OfferSheetStatus.NotEligible,
                "No RFA rights record exists for this player.",
                "Review Contract Rights first; offer sheets only apply to eligible RFAs.",
                "Agent has no offer-sheet path yet.");
        }

        var offering = DefaultOfferingTeam(prepared, offeringOrganizationId, offeringTeamName);
        return EvaluateEligibility(prepared, RulesFor(rulebook ?? prepared.LeagueProfile.Rulebook), decision, offering.OrganizationId, offering.TeamName, annualSalary, termYears, ownedCompensationRounds);
    }

    public OfferSheetCompensation CalculateCompensation(
        NewGmScenarioSnapshot scenario,
        Rulebook? rulebook,
        string offeringOrganizationId,
        string offeringTeamName,
        decimal annualSalary,
        int termYears,
        IReadOnlyList<int>? ownedCompensationRounds = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var rules = RulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        var thresholds = rules?.CompensationThresholds ?? Array.Empty<OfferSheetCompensationThreshold>();
        var threshold = thresholds
            .OrderBy(item => item.MinimumAav)
            .LastOrDefault(item => annualSalary >= item.MinimumAav && (!item.MaximumAav.HasValue || annualSalary <= item.MaximumAav.Value));
        var requiredRounds = threshold?.RequiredRounds.Distinct().OrderBy(item => item).ToArray() ?? Array.Empty<int>();
        var owned = ownedCompensationRounds?.Distinct().ToArray() ?? requiredRounds;
        var missing = requiredRounds.Where(round => !owned.Contains(round)).ToArray();
        var year = Math.Max(scenario.Season.Year + 1, scenario.CurrentDate.Year + 1);
        var tradeService = new TradeService();
        var picks = requiredRounds
            .Select(round => tradeService.CreateDraftPickAsset(scenario, TradeSide.OtherOrganization, offeringOrganizationId, offeringTeamName, round, year))
            .ToArray();
        var summary = requiredRounds.Length == 0
            ? $"No draft-pick compensation required at {annualSalary:C0} AAV."
            : $"{annualSalary:C0} AAV requires {string.Join(", ", requiredRounds.Select(RoundText))} round compensation.";
        if (missing.Length > 0)
        {
            summary = $"{summary} Missing: {string.Join(", ", missing.Select(RoundText))}.";
        }

        var compensation = new OfferSheetCompensation(
            Math.Round(annualSalary, 0),
            termYears,
            requiredRounds,
            picks,
            missing,
            missing.Length == 0,
            Math.Round(annualSalary, 0),
            summary);
        compensation.Validate();
        return compensation;
    }

    public OfferSheetResult SubmitOfferSheet(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        decimal annualSalary = 2_500_000m,
        int termYears = 2,
        string? offeringOrganizationId = null,
        string? offeringTeamName = null,
        IReadOnlyList<int>? ownedCompensationRounds = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = _rights.EnsureRights(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var offering = DefaultOfferingTeam(prepared, offeringOrganizationId, offeringTeamName);
        var eligibility = EvaluateEligibility(prepared, registry.Rulebook ?? prepared.LeagueProfile.Rulebook, personId, offering.OrganizationId, offering.TeamName, annualSalary, termYears, ownedCompensationRounds);
        if (!eligibility.IsEligible)
        {
            return Result(false, prepared, null, eligibility, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), eligibility.Reason);
        }

        var compensation = CalculateCompensation(prepared, registry.Rulebook ?? prepared.LeagueProfile.Rulebook, offering.OrganizationId, offering.TeamName, annualSalary, termYears, ownedCompensationRounds);
        var rules = RulesFor(registry.Rulebook ?? prepared.LeagueProfile.Rulebook)!;
        var status = compensation.RequiredRounds.Count == 0 ? OfferSheetStatus.AcceptedByPlayer : OfferSheetStatus.CompensationRequired;
        var offerSheet = new OfferSheet(
            $"offer-sheet:{personId}:{prepared.CurrentDate:yyyyMMdd}:{Guid.NewGuid():N}",
            personId,
            eligibility.PlayerName,
            eligibility.Position,
            eligibility.RightsHolderOrganizationId,
            eligibility.RightsHolderTeamName,
            eligibility.OfferingOrganizationId,
            eligibility.OfferingTeamName,
            status,
            prepared.CurrentDate,
            prepared.CurrentDate.AddDays(Math.Max(1, rules.ResponseWindowDays)),
            annualSalary,
            termYears,
            "USD",
            compensation,
            $"Agent says {eligibility.PlayerName} is willing to sign if the rights holder does not match.",
            $"Match to keep {eligibility.PlayerName}; decline to take {compensation.Summary.ToLowerInvariant()}",
            null);
        var updated = ReplaceOfferSheet(prepared, offerSheet);
        updated = AddHistory(updated, offerSheet, OfferSheetDecision.SubmitOffer, $"{offering.TeamName} submitted an offer sheet to {offerSheet.PlayerName}.");
        updated = AddCareerTimeline(updated, offerSheet, "Offer sheet submitted", $"{offerSheet.OfferingTeamName} forced a match-or-compensation decision for {offerSheet.PlayerName}.");
        QueueEvent(registry, updated.CurrentDate, LegacyEventType.OfferSheetSubmitted, offerSheet, "Offer sheet submitted", $"{offerSheet.OfferingTeamName} offered {offerSheet.PlayerName} {annualSalary:C0} for {termYears} year(s).");
        QueueEvent(registry, updated.CurrentDate, LegacyEventType.OfferSheetAccepted, offerSheet, "Player accepted offer sheet", $"{offerSheet.PlayerName} accepted the offer sheet terms; {offerSheet.RightsHolderTeamName} must match or take compensation.");
        var inbox = new[]
        {
            Inbox(offerSheet, LegacyEventType.OfferSheetAccepted, LegacyEventSeverity.Warning, $"Offer sheet decision: {offerSheet.PlayerName}", $"{offerSheet.PlayerName} accepted {offerSheet.OfferingTeamName}'s {annualSalary:C0} x {termYears} offer sheet. {offerSheet.RightsHolderTeamName} must match by {offerSheet.ResponseDeadline:yyyy-MM-dd} or take compensation.")
        };
        var transactions = new[] { Transaction(offerSheet, LeagueTransactionType.OfferSheetSubmitted, $"{offerSheet.OfferingTeamName} submitted an offer sheet to {offerSheet.PlayerName}.") };
        return Result(true, updated, offerSheet, eligibility, inbox, transactions, $"{offerSheet.PlayerName} accepted the offer sheet. Rights-holder decision required.");
    }

    public OfferSheetResult MatchOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var offerSheet = scenario.OfferSheets.FirstOrDefault(item => item.PersonId == personId && item.IsActive);
        if (offerSheet is null)
        {
            return Result(false, scenario, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No active offer sheet was found for this player.");
        }

        var contract = CreateSignedContract(scenario, offerSheet, offerSheet.RightsHolderOrganizationId);
        var resolved = offerSheet with { Status = OfferSheetStatus.MatchedByTeam, ResolvedOn = scenario.CurrentDate };
        var updated = ReplaceOfferSheet(scenario, resolved);
        updated = AddContract(updated, contract, offerSheet, keptByRightsHolder: true);
        updated = AddHistory(updated, resolved, OfferSheetDecision.MatchOffer, $"{offerSheet.RightsHolderTeamName} matched {offerSheet.PlayerName}'s offer sheet.");
        updated = AddCareerTimeline(updated, resolved, "Offer sheet matched", $"{offerSheet.RightsHolderTeamName} matched {offerSheet.OfferingTeamName}'s offer sheet and kept {offerSheet.PlayerName}.");
        QueueEvent(registry, updated.CurrentDate, LegacyEventType.OfferSheetMatched, resolved, "Offer sheet matched", $"{offerSheet.RightsHolderTeamName} matched the offer sheet for {offerSheet.PlayerName}.");
        var inbox = new[] { Inbox(resolved, LegacyEventType.OfferSheetMatched, LegacyEventSeverity.Notice, $"Offer sheet matched: {offerSheet.PlayerName}", $"{offerSheet.RightsHolderTeamName} matched the offer sheet. {offerSheet.PlayerName} remains with the club on a {offerSheet.TermYears}-year deal.") };
        var transactions = new[] { Transaction(resolved, LeagueTransactionType.OfferSheetMatched, $"{offerSheet.RightsHolderTeamName} matched {offerSheet.PlayerName}'s offer sheet.") };
        return Result(true, updated, resolved, null, inbox, transactions, $"{offerSheet.PlayerName}'s offer sheet was matched.");
    }

    public OfferSheetResult DeclineAndTakeCompensation(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var offerSheet = scenario.OfferSheets.FirstOrDefault(item => item.PersonId == personId && item.IsActive);
        if (offerSheet is null)
        {
            return Result(false, scenario, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No active offer sheet was found for this player.");
        }

        var contract = CreateSignedContract(scenario, offerSheet, offerSheet.OfferingOrganizationId);
        var resolved = offerSheet with { Status = OfferSheetStatus.Completed, ResolvedOn = scenario.CurrentDate };
        var updated = ReplaceOfferSheet(scenario, resolved);
        updated = AddContract(updated, contract, offerSheet, keptByRightsHolder: false);
        updated = AddHistory(updated, resolved, OfferSheetDecision.DeclineAndTakeCompensation, $"{offerSheet.RightsHolderTeamName} declined to match; {offerSheet.OfferingTeamName} signed {offerSheet.PlayerName} and owes compensation.");
        updated = AddCareerTimeline(updated, resolved, "Offer sheet completed", $"{offerSheet.PlayerName} left {offerSheet.RightsHolderTeamName} for {offerSheet.OfferingTeamName}; compensation: {offerSheet.Compensation.Summary}");
        QueueEvent(registry, updated.CurrentDate, LegacyEventType.OfferSheetCompleted, resolved, "Offer sheet completed", $"{offerSheet.OfferingTeamName} signed {offerSheet.PlayerName}; {offerSheet.RightsHolderTeamName} receives configured compensation.");
        var inbox = new[] { Inbox(resolved, LegacyEventType.OfferSheetCompleted, LegacyEventSeverity.Warning, $"Offer sheet compensation: {offerSheet.PlayerName}", $"{offerSheet.PlayerName} joins {offerSheet.OfferingTeamName}. Compensation owed: {offerSheet.Compensation.Summary}") };
        var transactions = new[] { Transaction(resolved, LeagueTransactionType.OfferSheetCompleted, $"{offerSheet.OfferingTeamName} signed {offerSheet.PlayerName} by offer sheet. Compensation: {offerSheet.Compensation.Summary}") };
        return Result(true, updated, resolved, null, inbox, transactions, $"{offerSheet.PlayerName} signed with {offerSheet.OfferingTeamName}; compensation recorded.");
    }

    public IReadOnlyList<OfferSheet> GenerateAiOfferSheets(NewGmScenarioSnapshot scenario, int maxOffers = 1)
    {
        if (maxOffers <= 0 || scenario.Season.Year % 19 != 0)
        {
            return Array.Empty<OfferSheet>();
        }

        return scenario.OfferSheets.Where(item => item.IsActive).Take(maxOffers).ToArray();
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var rules = RulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        if (rules is null || !rules.OfferSheetsEnabled)
        {
            return Array.Empty<ActionCenterItem>();
        }

        return scenario.OfferSheets
            .Where(item => item.IsActive)
            .Where(item => item.RightsHolderOrganizationId == scenario.Organization.OrganizationId)
            .Select(item => new ActionCenterItem(
                $"action-center:offer-sheet:{item.OfferSheetId}",
                $"Offer sheet decision: {item.PlayerName}",
                ActionCenterCategory.Contracts,
                item.ResponseDeadline <= scenario.CurrentDate.AddDays(2) ? ActionCenterPriority.Urgent : ActionCenterPriority.Important,
                item.ResponseDeadline,
                item.PersonId,
                item.PlayerName,
                item.RightsHolderOrganizationId,
                item.RightsHolderTeamName,
                $"{item.OfferingTeamName} offered {item.AnnualSalary:C0} x {item.TermYears}.",
                "If you do not match, the player leaves and draft-pick compensation is recorded.",
                $"Match the offer or decline and take compensation before {item.ResponseDeadline:yyyy-MM-dd}.",
                null,
                null,
                null))
            .ToArray();
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId, Rulebook? rulebook = null)
    {
        var rules = RulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        var lines = new List<string>();
        if (rules?.OfferSheetsEnabled != true)
        {
            lines.Add("Offer sheets: disabled by this rulebook.");
            return lines;
        }

        var active = scenario.OfferSheets.FirstOrDefault(item => item.PersonId == personId && item.IsActive);
        if (active is not null)
        {
            lines.Add($"Active offer sheet: {active.OfferingTeamName} offered {active.AnnualSalary:C0} x {active.TermYears}.");
            lines.Add($"Rights holder: {active.RightsHolderTeamName}; deadline {active.ResponseDeadline:yyyy-MM-dd}.");
            lines.Add($"Compensation risk: {active.Compensation.Summary}");
        }
        else
        {
            var eligibility = EvaluateEligibility(scenario, rulebook ?? scenario.LeagueProfile.Rulebook, personId);
            lines.Add($"Offer sheet eligibility: {Display(eligibility.Status)} - {eligibility.Reason}");
        }

        foreach (var entry in scenario.OfferSheetHistory.ForPlayer(personId).Take(3))
        {
            lines.Add($"Offer sheet history: {entry.Date:yyyy-MM-dd} - {entry.Summary}");
        }

        return lines;
    }

    public string BuildRuleSummary(Rulebook? rulebook)
    {
        var rules = RulesFor(rulebook);
        if (rules is null || !rules.OfferSheetsEnabled)
        {
            return "Offer sheets disabled by rulebook. Junior/AHL-style leagues do not use offer sheets unless explicitly enabled.";
        }

        return $"Offer sheets enabled | response window {rules.ResponseWindowDays} day(s) | compensation tiers: {string.Join("; ", rules.CompensationThresholds.Select(item => $"{item.MinimumAav:C0}-{(item.MaximumAav.HasValue ? item.MaximumAav.Value.ToString("C0") : "open")}: {item.Description}"))}.";
    }

    private OfferSheetEligibility EvaluateEligibility(
        NewGmScenarioSnapshot scenario,
        OfferSheetRules? rules,
        PlayerRightsDecision decision,
        string offeringOrganizationId,
        string offeringTeamName,
        decimal annualSalary,
        int termYears,
        IReadOnlyList<int>? ownedCompensationRounds)
    {
        if (rules is null || !rules.OfferSheetsEnabled)
        {
            return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.NotEligible, "Offer sheets are disabled by this rulebook.", "Use standard contract/free-agency tools.", "Agent cannot use an offer-sheet path here.");
        }

        if (offeringOrganizationId == decision.RightsHolderOrganizationId)
        {
            return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.Blocked, "The offering team already holds this player's rights.", "Negotiate a normal contract instead.", "Agent expects a direct negotiation.");
        }

        if (!EligibleStatus(rules, decision.RightsStatus))
        {
            return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.NotEligible, $"{decision.PlayerName} is {decision.RightsStatus}, not an eligible RFA status.", "Review RFA/UFA rights before attempting an offer sheet.", "Agent does not see offer-sheet eligibility.");
        }

        if (HasActiveContract(scenario, decision.PersonId))
        {
            return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.Blocked, $"{decision.PlayerName} already has an active contract.", "Wait until the contract expires and rights are established.", "Agent cannot discuss an offer sheet while under contract.");
        }

        if (rules.ArbitrationBlocksOfferSheets && scenario.ArbitrationCases.Any(item => item.PersonId == decision.PersonId && item.IsOpen))
        {
            return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.Blocked, "An active arbitration case blocks offer-sheet activity by this rulebook.", "Resolve arbitration first.", "Agent is focused on arbitration.");
        }

        var compensation = CalculateCompensation(scenario, scenario.LeagueProfile.Rulebook, offeringOrganizationId, offeringTeamName, annualSalary, termYears, ownedCompensationRounds);
        if (rules.RequiredDraftPickOwnership && !compensation.HasRequiredPicks)
        {
            return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.Blocked, $"Required compensation picks are missing: {string.Join(", ", compensation.MissingRounds.Select(RoundText))}.", "Acquire the necessary draft picks before submitting.", "Agent will not proceed if the filing is invalid.");
        }

        if (rules.CapValidationEnabled)
        {
            var cap = _salaryCap.ProjectAfterSigning(scenario, scenario.LeagueProfile.Rulebook, annualSalary, termYears);
            if (!cap.IsCompliant || annualSalary > Math.Max(0m, cap.Before.Profile.CapAmount))
            {
                return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.Blocked, "The projected offer sheet is not cap compliant.", "Lower the AAV or clear cap room before submitting.", "Agent sees cap risk as a serious obstacle.");
            }
        }

        return Eligibility(scenario, decision, offeringOrganizationId, offeringTeamName, OfferSheetStatus.Eligible, $"{decision.PlayerName} is an eligible RFA for an offer sheet.", "Submit only if you are comfortable with the compensation and relationship fallout.", $"Agent interest: meaningful. Offer needs term, role clarity, and {annualSalary:C0} AAV credibility.");
    }

    private static OfferSheetEligibility Eligibility(NewGmScenarioSnapshot scenario, PlayerRightsDecision decision, string offeringOrganizationId, string offeringTeamName, OfferSheetStatus status, string reason, string recommendation, string agentInterest) =>
        new(
            decision.PersonId,
            decision.PlayerName,
            decision.Position,
            decision.Age,
            decision.RightsHolderOrganizationId,
            decision.RightsHolderTeamName,
            offeringOrganizationId,
            offeringTeamName,
            status,
            reason,
            recommendation,
            agentInterest);

    private static bool EligibleStatus(OfferSheetRules rules, FreeAgentRightsStatus status)
    {
        if (rules.EligibleRightsStatuses.Count == 0)
        {
            return status is FreeAgentRightsStatus.Qualified or FreeAgentRightsStatus.RestrictedFreeAgent or FreeAgentRightsStatus.RightsHeld;
        }

        return rules.EligibleRightsStatuses.Any(item => string.Equals(item, status.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasActiveContract(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .DistinctBy(item => item.ContractId)
            .Any(item => item.PersonId == personId && item.Status == ContractStatus.Signed && item.Term.EndDate > scenario.CurrentDate);

    private static Contract CreateSignedContract(NewGmScenarioSnapshot scenario, OfferSheet offerSheet, string organizationId)
    {
        var term = ContractExpiryCalendar.TermForYears(scenario.CurrentDate, scenario.Season.Settings, offerSheet.TermYears);
        var contract = new Contract(
            $"contract:offer-sheet:{offerSheet.PersonId}:{organizationId}:{Guid.NewGuid():N}",
            offerSheet.PersonId,
            organizationId,
            ContractType.JuniorPlayerAgreement,
            ContractStatus.Signed,
            term,
            new ContractMoney(offerSheet.AnnualSalary, 0m, offerSheet.Currency),
            new[] { new ContractClause($"clause:offer-sheet:{offerSheet.PersonId}:{Guid.NewGuid():N}", ContractClauseType.OfferSheet, "Offer sheet contract matched or completed under rulebook-driven compensation rules.") },
            scenario.CurrentDate,
            scenario.CurrentDate,
            null,
            null,
            null);
        contract.Validate();
        return contract;
    }

    private static NewGmScenarioSnapshot AddContract(NewGmScenarioSnapshot scenario, Contract contract, OfferSheet offerSheet, bool keptByRightsHolder)
    {
        var contracts = scenario.Contracts
            .Where(item => item.ContractId != contract.ContractId)
            .Append(contract)
            .OrderBy(item => item.PersonId, StringComparer.Ordinal)
            .ThenByDescending(item => item.Term.EndDate)
            .ToArray();
        var alpha = scenario.AlphaSnapshot with { Contracts = contracts };
        var rights = scenario.PlayerRightsDecisions
            .Select(decision => decision.PersonId == contract.PersonId
                ? decision with
                {
                    RightsStatus = keptByRightsHolder ? FreeAgentRightsStatus.UnderContract : FreeAgentRightsStatus.SignedElsewhere,
                    ContractRightsStatus = keptByRightsHolder ? ContractRightsStatus.UnderContract : ContractRightsStatus.SignedElsewhere,
                    ContractId = contract.ContractId,
                    ContractExpiryDate = contract.Term.EndDate,
                    RightsHolderOrganizationId = keptByRightsHolder ? offerSheet.RightsHolderOrganizationId : offerSheet.OfferingOrganizationId,
                    RightsHolderTeamName = keptByRightsHolder ? offerSheet.RightsHolderTeamName : offerSheet.OfferingTeamName,
                    Recommendation = keptByRightsHolder ? "Offer sheet matched; contract completed." : "Player signed elsewhere; compensation recorded.",
                    AgentNote = keptByRightsHolder ? "Agent secured matched terms." : "Agent completed the move through offer sheet.",
                    Reason = keptByRightsHolder
                        ? $"{offerSheet.RightsHolderTeamName} matched the offer sheet for {offerSheet.PlayerName}."
                        : $"{offerSheet.PlayerName} signed with {offerSheet.OfferingTeamName} by offer sheet.",
                    LastUpdatedOn = scenario.CurrentDate
                }
                : decision)
            .ToArray();
        var updated = scenario with { Contracts = contracts, AlphaSnapshot = alpha, PlayerRightsDecisions = rights };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot ReplaceOfferSheet(NewGmScenarioSnapshot scenario, OfferSheet offerSheet)
    {
        offerSheet.Validate();
        var sheets = scenario.OfferSheets
            .Where(item => item.OfferSheetId != offerSheet.OfferSheetId && !(item.PersonId == offerSheet.PersonId && item.IsActive))
            .Append(offerSheet)
            .OrderByDescending(item => item.SubmittedOn)
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = scenario with { OfferSheets = sheets };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot AddHistory(NewGmScenarioSnapshot scenario, OfferSheet offerSheet, OfferSheetDecision decision, string summary)
    {
        var entry = new OfferSheetHistoryEntry(
            $"offer-sheet-history:{offerSheet.PersonId}:{decision}:{scenario.CurrentDate:yyyyMMdd}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            offerSheet.PersonId,
            offerSheet.PlayerName,
            offerSheet.Status,
            decision,
            offerSheet.RightsHolderOrganizationId,
            offerSheet.RightsHolderTeamName,
            offerSheet.OfferingOrganizationId,
            offerSheet.OfferingTeamName,
            summary);
        var history = scenario.OfferSheetHistory.Add(entry);
        var transaction = new TransactionHistoryRecord(
            $"transaction-history:offer-sheet:{offerSheet.PersonId}:{decision}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            scenario.Season.Year,
            $"OfferSheet{decision}",
            offerSheet.PersonId,
            offerSheet.PlayerName,
            offerSheet.RightsHolderOrganizationId,
            offerSheet.RightsHolderTeamName,
            summary);
        return scenario with
        {
            OfferSheetHistory = history,
            TransactionHistory = scenario.TransactionHistory.Append(transaction).ToArray()
        };
    }

    private static NewGmScenarioSnapshot AddCareerTimeline(NewGmScenarioSnapshot scenario, OfferSheet offerSheet, string title, string description)
    {
        var entry = new CareerTimelineEntry(
            $"career:offer-sheet:{offerSheet.PersonId}:{offerSheet.Status}:{scenario.CurrentDate:yyyyMMdd}:{Guid.NewGuid():N}",
            CareerTimelineEntryType.Signed,
            scenario.CurrentDate,
            scenario.Season.Year,
            offerSheet.PersonId,
            offerSheet.RightsHolderOrganizationId,
            offerSheet.RightsHolderTeamName,
            title,
            description,
            null,
            HistoryImportance.Major);
        return scenario with { CareerTimeline = scenario.CareerTimeline.Add(entry) };
    }

    private static void QueueEvent(EngineRegistry registry, DateOnly date, LegacyEventType eventType, OfferSheet offerSheet, string title, string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 13, 0, 0, TimeSpan.Zero),
            eventType,
            eventType == LegacyEventType.OfferSheetAccepted ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: offerSheet.PersonId, OrganizationId: offerSheet.RightsHolderOrganizationId),
            new Dictionary<string, object?>
            {
                ["player_name"] = offerSheet.PlayerName,
                ["team_name"] = offerSheet.RightsHolderTeamName,
                ["offering_team"] = offerSheet.OfferingTeamName,
                ["reason"] = description,
                ["alpha_7_5"] = true
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(OfferSheet offerSheet, LegacyEventType eventType, LegacyEventSeverity severity, string title, string summary) =>
        new(
            $"inbox:offer-sheet:{offerSheet.PersonId}:{eventType}:{Guid.NewGuid():N}",
            new DateTimeOffset(offerSheet.SubmittedOn.Year, offerSheet.SubmittedOn.Month, offerSheet.SubmittedOn.Day, 13, 0, 0, TimeSpan.Zero),
            eventType,
            severity,
            title,
            summary,
            offerSheet.PersonId);

    private static LeagueTransaction Transaction(OfferSheet offerSheet, LeagueTransactionType type, string description) =>
        new(
            $"transaction:offer-sheet:{offerSheet.PersonId}:{type}:{Guid.NewGuid():N}",
            new DateTimeOffset(offerSheet.SubmittedOn.Year, offerSheet.SubmittedOn.Month, offerSheet.SubmittedOn.Day, 13, 0, 0, TimeSpan.Zero),
            offerSheet.RightsHolderOrganizationId,
            offerSheet.RightsHolderTeamName,
            offerSheet.PersonId,
            offerSheet.PlayerName,
            type,
            LeagueTransactionWireService.CategoryFor(type),
            description);

    private static OfferSheetResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        OfferSheet? offerSheet,
        OfferSheetEligibility? eligibility,
        IReadOnlyList<AlphaInboxItem> inbox,
        IReadOnlyList<LeagueTransaction> transactions,
        string message)
    {
        var result = new OfferSheetResult(success, scenario, offerSheet, eligibility, inbox, transactions, message);
        result.Validate();
        return result;
    }

    private static OfferSheetRules? RulesFor(Rulebook? rulebook) =>
        rulebook?.OfferSheetRules ?? new OfferSheetRules { OfferSheetsEnabled = false };

    private static TeamSelectionOption DefaultOfferingTeam(NewGmScenarioSnapshot scenario, string? organizationId = null, string? teamName = null)
    {
        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            return new TeamSelectionOption(
                organizationId,
                string.IsNullOrWhiteSpace(teamName) ? organizationId : teamName,
                "Opponent",
                "League",
                "Canada",
                "placeholder",
                "0-0-0",
                "External offer-sheet pressure",
                100_000_000m,
                50,
                "Unknown",
                "Medium",
                "Unknown");
        }

        return scenario.LeagueProfile.Teams.FirstOrDefault(team => team.OrganizationId != scenario.Organization.OrganizationId)
            ?? new TeamSelectionOption("org-rival", "Rival Club", "Rival", "League", "Canada", "placeholder", "0-0-0", "External offer-sheet pressure", 100_000_000m, 50, "Unknown", "Medium", "Unknown");
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? personId;

    private static RosterPosition PositionFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? RosterPosition.Unknown;

    private static int? AgeFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Age
        ?? scenario.FreeAgentMarket?.Find(personId)?.Age;

    private static string RoundText(int round) =>
        round switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{round}th"
        };

    private static string Display(OfferSheetStatus status) =>
        status switch
        {
            OfferSheetStatus.NotEligible => "Not Eligible",
            OfferSheetStatus.AcceptedByPlayer => "Accepted By Player",
            OfferSheetStatus.MatchedByTeam => "Matched By Team",
            OfferSheetStatus.DeclinedByPlayer => "Declined By Player",
            OfferSheetStatus.CompensationRequired => "Compensation Required",
            _ => status.ToString()
        };
}
