using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class ArbitrationService
{
    private readonly RfaUfaService _rights = new();

    public IReadOnlyList<ArbitrationEligibility> BuildEligibility(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rules = ArbitrationRulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        if (rules is null || !rules.ArbitrationEnabled)
        {
            return Array.Empty<ArbitrationEligibility>();
        }

        var prepared = _rights.EnsureRights(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var output = prepared.PlayerRightsDecisions
            .Where(decision => decision.RightsStatus is FreeAgentRightsStatus.Qualified or FreeAgentRightsStatus.RestrictedFreeAgent or FreeAgentRightsStatus.RightsHeld)
            .Select(decision => EligibilityFromDecision(decision, rules))
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.FilingDeadline ?? DateOnly.MaxValue)
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();

        foreach (var item in output)
        {
            item.Validate();
        }

        return output;
    }

    public NewGmScenarioSnapshot EnsureArbitration(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var rules = ArbitrationRulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        if (rules is null || !rules.ArbitrationEnabled)
        {
            return scenario with { ArbitrationCases = Array.Empty<ArbitrationCase>() };
        }

        var prepared = _rights.EnsureRights(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var existing = prepared.ArbitrationCases
            .GroupBy(item => item.PersonId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedOn).First(), StringComparer.Ordinal);
        var cases = new List<ArbitrationCase>();
        foreach (var eligible in BuildEligibility(prepared, rulebook ?? prepared.LeagueProfile.Rulebook).Where(item => item.Status == ArbitrationEligibilityStatus.Eligible))
        {
            if (existing.TryGetValue(eligible.PersonId, out var current) && current.Status is not ArbitrationCaseStatus.NotEligible)
            {
                cases.Add(current);
                continue;
            }

            cases.Add(new ArbitrationCase(
                CaseId: $"arbitration:{eligible.PersonId}:{prepared.Season.Year}",
                PersonId: eligible.PersonId,
                PlayerName: eligible.PlayerName,
                Position: eligible.Position,
                OrganizationId: prepared.Organization.OrganizationId,
                OrganizationName: prepared.Organization.Name,
                Status: ArbitrationCaseStatus.Eligible,
                CreatedOn: prepared.CurrentDate,
                FilingDeadline: eligible.FilingDeadline,
                HearingDate: null,
                Filing: null,
                Award: BuildAwardEstimate(prepared, eligible, rules),
                Recommendation: "Use arbitration only if normal contract talks stall; settlement before hearing is usually less disruptive.",
                AgentComment: "Agent is open to settlement, but will use the hearing path if role, comparables, and salary do not line up."));
        }

        foreach (var current in existing.Values.Where(item => cases.All(candidate => candidate.PersonId != item.PersonId) && item.IsOpen))
        {
            cases.Add(current with
            {
                Status = ArbitrationCaseStatus.NotEligible,
                Recommendation = "Player no longer meets the arbitration case requirements.",
                AgentComment = "No active arbitration path."
            });
        }

        var updated = prepared with
        {
            ArbitrationCases = cases
                .GroupBy(item => item.PersonId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item => item.CreatedOn).First())
                .OrderBy(item => item.FilingDeadline ?? DateOnly.MaxValue)
                .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
                .ToArray()
        };
        updated.Validate();
        return updated;
    }

    public ArbitrationResult FileTeamArbitration(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var prepared = EnsureArbitration(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var rules = ArbitrationRulesFor(registry.Rulebook ?? prepared.LeagueProfile.Rulebook);
        if (rules is null || !rules.ArbitrationEnabled)
        {
            return Result(false, prepared, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Arbitration is disabled by this rulebook.");
        }

        var arbitrationCase = prepared.ArbitrationCases.FirstOrDefault(item => item.PersonId == personId);
        if (arbitrationCase is null || arbitrationCase.Status != ArbitrationCaseStatus.Eligible)
        {
            return Result(false, prepared, arbitrationCase, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No eligible arbitration case was found for that player.");
        }

        var hearing = HearingDate(prepared.CurrentDate, rules);
        var filing = new ArbitrationFiling(
            $"arbitration-filing:{personId}:{Guid.NewGuid():N}",
            arbitrationCase.CaseId,
            ArbitrationFilingType.TeamElected,
            prepared.CurrentDate,
            prepared.Organization.Name,
            $"{prepared.Organization.Name} filed team-elected arbitration for {arbitrationCase.PlayerName}.");
        var updatedCase = arbitrationCase with
        {
            Status = ArbitrationCaseStatus.HearingScheduled,
            HearingDate = hearing,
            Filing = filing,
            Award = arbitrationCase.Award ?? BuildAwardEstimate(prepared, arbitrationCase, rules),
            Recommendation = "Try one more settlement proposal before the hearing; avoid surprising the player unless budget pressure is severe.",
            AgentComment = "Agent expects a credible settlement offer before the hearing."
        };
        var updated = ReplaceCase(prepared, updatedCase);
        updated = AddHistory(updated, updatedCase, ArbitrationDecisionType.FileTeamElected, $"{prepared.Organization.Name} filed arbitration for {updatedCase.PlayerName}; hearing scheduled for {hearing:yyyy-MM-dd}.");
        QueueEvent(registry, updated, updatedCase, LegacyEventType.ArbitrationHearingScheduled, "Arbitration hearing scheduled", updatedCase.Recommendation);
        var inbox = new[] { Inbox(updatedCase, LegacyEventType.ArbitrationHearingScheduled, LegacyEventSeverity.Warning, $"Arbitration hearing: {updatedCase.PlayerName}", $"{updatedCase.PlayerName}'s hearing is scheduled for {hearing:yyyy-MM-dd}. Projected award: {updatedCase.Award!.ProjectedAwardLow:C0}-{updatedCase.Award.ProjectedAwardHigh:C0}.") };
        var transactions = new[] { Transaction(updatedCase, LeagueTransactionType.ArbitrationFiled, $"{updatedCase.OrganizationName} filed arbitration for {updatedCase.PlayerName}.") };
        return Result(true, updated, updatedCase, inbox, transactions, $"Arbitration filed for {updatedCase.PlayerName}. Hearing scheduled for {hearing:yyyy-MM-dd}.");
    }

    public ArbitrationResult PlayerFileArbitration(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var prepared = EnsureArbitration(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var rules = ArbitrationRulesFor(registry.Rulebook ?? prepared.LeagueProfile.Rulebook);
        var arbitrationCase = prepared.ArbitrationCases.FirstOrDefault(item => item.PersonId == personId);
        if (rules is null || !rules.ArbitrationEnabled || arbitrationCase is null || arbitrationCase.Status != ArbitrationCaseStatus.Eligible)
        {
            return Result(false, prepared, arbitrationCase, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Player-elected arbitration is not available for this player.");
        }

        var hearing = HearingDate(prepared.CurrentDate, rules);
        var filing = new ArbitrationFiling(
            $"arbitration-filing:{personId}:{Guid.NewGuid():N}",
            arbitrationCase.CaseId,
            ArbitrationFilingType.PlayerElected,
            prepared.CurrentDate,
            "Player agent",
            $"{arbitrationCase.PlayerName}'s agent filed arbitration.");
        var updatedCase = arbitrationCase with
        {
            Status = ArbitrationCaseStatus.HearingScheduled,
            HearingDate = hearing,
            Filing = filing,
            Award = arbitrationCase.Award ?? BuildAwardEstimate(prepared, arbitrationCase, rules),
            Recommendation = "Prepare settlement terms and cap/budget impact before the hearing.",
            AgentComment = "Agent filed to force a resolution window."
        };
        var updated = ReplaceCase(prepared, updatedCase);
        updated = AddHistory(updated, updatedCase, ArbitrationDecisionType.PlayerFiled, $"{updatedCase.PlayerName}'s agent filed arbitration; hearing scheduled for {hearing:yyyy-MM-dd}.");
        QueueEvent(registry, updated, updatedCase, LegacyEventType.ArbitrationHearingScheduled, "Player filed arbitration", updatedCase.Recommendation);
        var inbox = new[] { Inbox(updatedCase, LegacyEventType.ArbitrationHearingScheduled, LegacyEventSeverity.Warning, $"Agent filed arbitration: {updatedCase.PlayerName}", $"{updatedCase.PlayerName}'s agent filed arbitration. Hearing date: {hearing:yyyy-MM-dd}.") };
        var transactions = new[] { Transaction(updatedCase, LeagueTransactionType.ArbitrationFiled, $"{updatedCase.PlayerName}'s agent filed arbitration with {updatedCase.OrganizationName}.") };
        return Result(true, updated, updatedCase, inbox, transactions, $"{updatedCase.PlayerName}'s agent filed arbitration.");
    }

    public ArbitrationResult NegotiateSettlement(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var prepared = EnsureArbitration(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var rules = ArbitrationRulesFor(registry.Rulebook ?? prepared.LeagueProfile.Rulebook);
        var arbitrationCase = prepared.ArbitrationCases.FirstOrDefault(item => item.PersonId == personId);
        if (rules is null || arbitrationCase is null || !arbitrationCase.IsOpen)
        {
            return Result(false, prepared, arbitrationCase, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No open arbitration case is available to settle.");
        }

        var award = arbitrationCase.Award ?? BuildAwardEstimate(prepared, arbitrationCase, rules);
        var settlement = Math.Round((award.TeamOffer + award.FinalAward) / 2m, 0);
        var signed = CreateSignedContract(prepared, arbitrationCase, settlement, award.Currency, $"arbitration-settlement:{personId}:{Guid.NewGuid():N}");
        var updatedCase = arbitrationCase with
        {
            Status = ArbitrationCaseStatus.SettledBeforeHearing,
            Award = award with { FinalAward = settlement, Explanation = $"{award.Explanation} Settled before hearing at {settlement:C0}." },
            Recommendation = "Case resolved by settlement; monitor relationship and budget impact.",
            AgentComment = "Agent accepted settlement before the hearing."
        };
        var updated = AddContract(prepared, signed);
        updated = ReplaceCase(updated, updatedCase);
        updated = AddHistory(updated, updatedCase, ArbitrationDecisionType.NegotiateSettlement, $"{updatedCase.PlayerName} settled before hearing at {settlement:C0}.");
        QueueEvent(registry, updated, updatedCase, LegacyEventType.ArbitrationSettled, "Arbitration settlement reached", updatedCase.Award!.Explanation);
        var inbox = new[] { Inbox(updatedCase, LegacyEventType.ArbitrationSettled, LegacyEventSeverity.Notice, $"Arbitration settled: {updatedCase.PlayerName}", $"{updatedCase.PlayerName} signed a settlement at {settlement:C0}. Contract expires {signed.Term.EndDate:yyyy-MM-dd}.") };
        var transactions = new[] { Transaction(updatedCase, LeagueTransactionType.ArbitrationSettled, $"{updatedCase.OrganizationName} settled arbitration with {updatedCase.PlayerName} at {settlement:C0}.") };
        return Result(true, updated, updatedCase, inbox, transactions, $"{updatedCase.PlayerName} settled before hearing.");
    }

    public ArbitrationResult AcceptAward(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var prepared = EnsureArbitration(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var rules = ArbitrationRulesFor(registry.Rulebook ?? prepared.LeagueProfile.Rulebook);
        var arbitrationCase = prepared.ArbitrationCases.FirstOrDefault(item => item.PersonId == personId);
        if (rules is null || arbitrationCase is null || !arbitrationCase.IsOpen)
        {
            return Result(false, prepared, arbitrationCase, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No arbitration award is available to accept.");
        }

        var award = arbitrationCase.Award ?? BuildAwardEstimate(prepared, arbitrationCase, rules);
        var signed = CreateSignedContract(prepared, arbitrationCase, award.FinalAward, award.Currency, $"arbitration-award:{personId}:{Guid.NewGuid():N}");
        var updatedCase = arbitrationCase with
        {
            Status = ArbitrationCaseStatus.Accepted,
            Award = award,
            Recommendation = "Award accepted and converted into a contract.",
            AgentComment = "Agent accepts the award as binding club resolution."
        };
        var updated = AddContract(prepared, signed);
        updated = ReplaceCase(updated, updatedCase);
        updated = AddHistory(updated, updatedCase, ArbitrationDecisionType.AcceptAward, $"{updatedCase.PlayerName} accepted an arbitration award at {award.FinalAward:C0}.");
        QueueEvent(registry, updated, updatedCase, LegacyEventType.ArbitrationAwardIssued, "Arbitration award accepted", award.Explanation);
        var inbox = new[] { Inbox(updatedCase, LegacyEventType.ArbitrationAwardIssued, LegacyEventSeverity.Notice, $"Award accepted: {updatedCase.PlayerName}", $"{updatedCase.PlayerName}'s award was accepted at {award.FinalAward:C0}. Contract expires {signed.Term.EndDate:yyyy-MM-dd}.") };
        var transactions = new[] { Transaction(updatedCase, LeagueTransactionType.ArbitrationAwardIssued, $"{updatedCase.OrganizationName} accepted {updatedCase.PlayerName}'s arbitration award at {award.FinalAward:C0}.") };
        return Result(true, updated, updatedCase, inbox, transactions, $"{updatedCase.PlayerName}'s arbitration award was accepted.");
    }

    public ArbitrationResult WalkAway(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var prepared = EnsureArbitration(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var rules = ArbitrationRulesFor(registry.Rulebook ?? prepared.LeagueProfile.Rulebook);
        var arbitrationCase = prepared.ArbitrationCases.FirstOrDefault(item => item.PersonId == personId);
        if (rules is null || !rules.WalkAwayAllowed || arbitrationCase is null || !arbitrationCase.IsOpen)
        {
            return Result(false, prepared, arbitrationCase, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Walk-away is not allowed for this case.");
        }

        var rightsResult = _rights.DeclineQualifyingOffer(registry, prepared, personId);
        var baseScenario = rightsResult.Success ? rightsResult.ScenarioSnapshot : prepared;
        var updatedCase = arbitrationCase with
        {
            Status = ArbitrationCaseStatus.WalkedAway,
            Recommendation = "Player released to the market; replace the roster slot and repair relationship impact where needed.",
            AgentComment = "Agent views the walk-away as a clean break."
        };
        var updated = ReplaceCase(baseScenario, updatedCase);
        updated = AddHistory(updated, updatedCase, ArbitrationDecisionType.WalkAway, $"{updatedCase.OrganizationName} walked away from {updatedCase.PlayerName}'s arbitration case; player rights were released.");
        QueueEvent(registry, updated, updatedCase, LegacyEventType.ArbitrationWalkAway, "Team walked away from arbitration", updatedCase.Recommendation);
        var inbox = new[] { Inbox(updatedCase, LegacyEventType.ArbitrationWalkAway, LegacyEventSeverity.Warning, $"Walk-away: {updatedCase.PlayerName}", $"{updatedCase.OrganizationName} walked away from {updatedCase.PlayerName}'s arbitration case. The player enters the market where allowed.") };
        var transactions = new[] { Transaction(updatedCase, LeagueTransactionType.ArbitrationWalkAway, $"{updatedCase.OrganizationName} walked away from arbitration with {updatedCase.PlayerName}.") };
        return Result(true, updated, updatedCase, inbox, transactions, $"{updatedCase.PlayerName}'s arbitration case ended with a walk-away.");
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var prepared = EnsureArbitration(scenario, rulebook);
        return prepared.ArbitrationCases
            .Where(item => item.IsOpen)
            .Select(item =>
            {
                var due = item.Status == ArbitrationCaseStatus.AwardIssued ? prepared.CurrentDate.AddDays(2) : item.HearingDate ?? item.FilingDeadline;
                var days = due.HasValue ? due.Value.DayNumber - prepared.CurrentDate.DayNumber : 30;
                var priority = item.Status == ArbitrationCaseStatus.AwardIssued || days <= 3 ? ActionCenterPriority.Urgent : days <= 10 ? ActionCenterPriority.Important : ActionCenterPriority.Normal;
                return new ActionCenterItem(
                    $"action-center:arbitration:{item.PersonId}",
                    $"Arbitration review: {item.PlayerName}",
                    ActionCenterCategory.Contracts,
                    priority,
                    due,
                    item.PersonId,
                    item.PlayerName,
                    item.OrganizationId,
                    item.OrganizationName,
                    item.AgentComment,
                    "Arbitration can settle an RFA dispute, but the final award affects budget and player relationship.",
                    item.Recommendation,
                    null,
                    null,
                    null);
            })
            .ToArray();
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId, Rulebook? rulebook = null)
    {
        var prepared = EnsureArbitration(scenario, rulebook);
        var arbitrationCase = prepared.ArbitrationCases.FirstOrDefault(item => item.PersonId == personId);
        var history = prepared.ArbitrationHistory.ForPlayer(personId).Take(4).ToArray();
        if (arbitrationCase is null && history.Length == 0)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>();
        if (arbitrationCase is not null)
        {
            lines.Add($"Arbitration status: {Display(arbitrationCase.Status)}.");
            lines.Add($"Hearing date: {arbitrationCase.HearingDate?.ToString("yyyy-MM-dd") ?? "not scheduled"}.");
            if (arbitrationCase.Award is not null)
            {
                lines.Add($"Projected award: {arbitrationCase.Award.ProjectedAwardLow:C0}-{arbitrationCase.Award.ProjectedAwardHigh:C0}; player ask {arbitrationCase.Award.PlayerAsk:C0}; team offer {arbitrationCase.Award.TeamOffer:C0}.");
                lines.Add($"Award context: {arbitrationCase.Award.Explanation}");
            }

            lines.Add($"Agent comment: {arbitrationCase.AgentComment}");
            lines.Add($"Recommendation: {arbitrationCase.Recommendation}");
        }

        foreach (var entry in history)
        {
            lines.Add($"Arbitration history: {entry.Date:yyyy-MM-dd} - {entry.Summary}");
        }

        return lines;
    }

    public string BuildRuleSummary(Rulebook rulebook)
    {
        var rules = ArbitrationRulesFor(rulebook);
        if (rules is null || !rules.ArbitrationEnabled)
        {
            return "Salary arbitration is disabled by this rulebook.";
        }

        return $"Arbitration enabled | eligibility age {rules.EligibilityAge} | accrued seasons {rules.AccruedSeasonsThreshold} | filing window +{rules.FilingWindowDaysAfterQualifyingOffer} day(s) | hearing +{rules.HearingStartDaysAfterFiling}-{rules.HearingEndDaysAfterFiling} day(s) | walk-away {(rules.WalkAwayAllowed ? "allowed" : "not allowed")}.";
    }

    private static ArbitrationEligibility EligibilityFromDecision(PlayerRightsDecision decision, ArbitrationRules rules)
    {
        var qoIssued = decision.QualifyingOffer?.IsIssued == true;
        var deadline = decision.QualifyingOffer?.IssuedOn?.AddDays(rules.FilingWindowDaysAfterQualifyingOffer)
            ?? decision.ExpiryRule?.Deadline.AddDays(rules.FilingWindowDaysAfterQualifyingOffer);
        var ageOk = decision.Age.HasValue && decision.Age.Value >= rules.EligibilityAge;
        var seasonsOk = decision.AccruedSeasons >= rules.AccruedSeasonsThreshold;
        var eligible = qoIssued && ageOk && seasonsOk && decision.RightsHolderOrganizationId != "none";
        var reason = eligible
            ? $"{decision.PlayerName} is a qualified RFA who meets age and accrued-season thresholds."
            : $"{decision.PlayerName} is not arbitration eligible: {(qoIssued ? "qualifying offer issued" : "qualifying offer not issued")}, age {decision.Age?.ToString() ?? "unknown"} / required {rules.EligibilityAge}, accrued {decision.AccruedSeasons} / required {rules.AccruedSeasonsThreshold}.";
        var item = new ArbitrationEligibility(
            decision.PersonId,
            decision.PlayerName,
            decision.Position,
            decision.Age,
            decision.AccruedSeasons,
            eligible ? ArbitrationEligibilityStatus.Eligible : ArbitrationEligibilityStatus.NotEligible,
            eligible ? deadline : null,
            qoIssued,
            decision.RightsHolderOrganizationId,
            reason);
        item.Validate();
        return item;
    }

    private static ArbitrationAward BuildAwardEstimate(NewGmScenarioSnapshot scenario, ArbitrationEligibility eligibility, ArbitrationRules rules) =>
        BuildAwardEstimate(scenario, eligibility.PersonId, eligibility.PlayerName, eligibility.Position, rules);

    private static ArbitrationAward BuildAwardEstimate(NewGmScenarioSnapshot scenario, ArbitrationCase arbitrationCase, ArbitrationRules rules) =>
        BuildAwardEstimate(scenario, arbitrationCase.PersonId, arbitrationCase.PlayerName, arbitrationCase.Position, rules);

    private static ArbitrationAward BuildAwardEstimate(NewGmScenarioSnapshot scenario, string personId, string playerName, RosterPosition position, ArbitrationRules rules)
    {
        var contract = CurrentContract(scenario, personId);
        var previousSalary = contract?.Money.SalaryOrStipend ?? Math.Max(rules.MinimumAward, 900_000m);
        var stats = scenario.PlayerStats.FirstOrDefault(stat => stat.PersonId == personId);
        var prior = scenario.PriorSeasonStats.OrderByDescending(stat => stat.SeasonYear).FirstOrDefault(stat => stat.PersonId == personId);
        var career = scenario.CareerStatSummaries.FirstOrDefault(stat => stat.PersonId == personId);
        var points = stats?.Points ?? prior?.Points ?? career?.Points ?? 0;
        var games = stats?.GamesPlayed ?? prior?.GamesPlayed ?? career?.GamesPlayed ?? 0;
        var roleMultiplier = position switch
        {
            RosterPosition.Goalie => 1.18m,
            RosterPosition.Center => 1.12m,
            RosterPosition.Defense => 1.10m,
            _ => 1.06m
        };
        var productionBonus = (points * 12_000m) + (games * 1_500m);
        var playerAsk = Clamp((previousSalary * 1.35m * roleMultiplier) + productionBonus, rules.MinimumAward, rules.MaximumAward);
        var teamOffer = Clamp(previousSalary * 1.08m + productionBonus * 0.35m, rules.MinimumAward, rules.MaximumAward);
        var low = Math.Min(teamOffer, playerAsk);
        var high = Math.Max(teamOffer, playerAsk);
        var final = Clamp((teamOffer + playerAsk) / 2m, rules.MinimumAward, rules.MaximumAward);
        var cap = scenario.LeagueProfile.Rulebook.SalaryCapRules?.CapAmount is decimal capAmount
            ? $"{final:C0} would use {(capAmount == 0 ? 0 : final / capAmount):P1} of the configured cap."
            : $"{final:C0} affects the hockey operations/player budget.";
        var explanation = $"{playerName}'s estimate weighs previous salary {previousSalary:C0}, role, age/accrued seasons, recent production ({points} points, {games} GP), comparable-contract placeholder, budget context, and agent pressure.";
        var award = new ArbitrationAward(
            $"arbitration-award:{personId}:{scenario.Season.Year}",
            personId,
            playerName,
            Math.Round(playerAsk, 0),
            Math.Round(teamOffer, 0),
            Math.Round(low, 0),
            Math.Round(high, 0),
            Math.Round(final, 0),
            contract?.Money.Currency ?? "USD",
            explanation,
            cap,
            "Agent will compare role, usage, recent production, and team leverage before settlement.");
        award.Validate();
        return award;
    }

    private static DateOnly HearingDate(DateOnly filedOn, ArbitrationRules rules) =>
        filedOn.AddDays(Math.Max(0, rules.HearingStartDaysAfterFiling));

    private static Contract CreateSignedContract(NewGmScenarioSnapshot scenario, ArbitrationCase arbitrationCase, decimal salary, string currency, string contractId)
    {
        var term = ContractExpiryCalendar.TermForYears(scenario.CurrentDate, scenario.Season.Settings, 1);
        var contract = new Contract(
            contractId,
            arbitrationCase.PersonId,
            arbitrationCase.OrganizationId,
            CurrentContract(scenario, arbitrationCase.PersonId)?.ContractType ?? ContractType.JuniorPlayerAgreement,
            ContractStatus.Signed,
            term,
            new ContractMoney(salary, 0m, currency),
            Array.Empty<ContractClause>(),
            scenario.CurrentDate,
            scenario.CurrentDate,
            null,
            null,
            null);
        contract.Validate();
        return contract;
    }

    private static NewGmScenarioSnapshot AddContract(NewGmScenarioSnapshot scenario, Contract contract)
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
                    RightsStatus = FreeAgentRightsStatus.UnderContract,
                    ContractRightsStatus = ContractRightsStatus.UnderContract,
                    ContractId = contract.ContractId,
                    ContractExpiryDate = contract.Term.EndDate,
                    Recommendation = "Signed through arbitration/settlement.",
                    AgentNote = "Contract dispute resolved.",
                    Reason = $"{decision.PlayerName} signed a contract through arbitration resolution.",
                    LastUpdatedOn = scenario.CurrentDate
                }
                : decision)
            .ToArray();
        var updated = scenario with { Contracts = contracts, AlphaSnapshot = alpha, PlayerRightsDecisions = rights };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot ReplaceCase(NewGmScenarioSnapshot scenario, ArbitrationCase arbitrationCase)
    {
        arbitrationCase.Validate();
        var cases = scenario.ArbitrationCases
            .Where(item => item.PersonId != arbitrationCase.PersonId)
            .Append(arbitrationCase)
            .OrderBy(item => item.FilingDeadline ?? DateOnly.MaxValue)
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = scenario with { ArbitrationCases = cases };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot AddHistory(NewGmScenarioSnapshot scenario, ArbitrationCase arbitrationCase, ArbitrationDecisionType decisionType, string summary)
    {
        var entry = new ArbitrationHistoryEntry(
            $"arbitration-history:{arbitrationCase.PersonId}:{decisionType}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            arbitrationCase.PersonId,
            arbitrationCase.PlayerName,
            arbitrationCase.Status,
            decisionType,
            arbitrationCase.OrganizationId,
            arbitrationCase.OrganizationName,
            summary);
        var transaction = new TransactionHistoryRecord(
            $"transaction-history:arbitration:{arbitrationCase.PersonId}:{decisionType}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            scenario.Season.Year,
            $"Arbitration{decisionType}",
            arbitrationCase.PersonId,
            arbitrationCase.PlayerName,
            arbitrationCase.OrganizationId,
            arbitrationCase.OrganizationName,
            summary);
        var timeline = new CareerTimelineEntry(
            $"career:arbitration:{arbitrationCase.PersonId}:{decisionType}:{Guid.NewGuid():N}",
            decisionType is ArbitrationDecisionType.WalkAway ? CareerTimelineEntryType.Released : CareerTimelineEntryType.Signed,
            scenario.CurrentDate,
            scenario.Season.Year,
            arbitrationCase.PersonId,
            arbitrationCase.OrganizationId,
            arbitrationCase.OrganizationName,
            "Salary arbitration",
            summary,
            null,
            HistoryImportance.Important);
        return scenario with
        {
            ArbitrationHistory = scenario.ArbitrationHistory.Add(entry),
            TransactionHistory = scenario.TransactionHistory.Append(transaction).ToArray(),
            CareerTimeline = scenario.CareerTimeline.Add(timeline)
        };
    }

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, ArbitrationCase arbitrationCase, LegacyEventType eventType, string title, string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: arbitrationCase.PersonId, OrganizationId: arbitrationCase.OrganizationId),
            new Dictionary<string, object?>
            {
                ["player_name"] = arbitrationCase.PlayerName,
                ["team_name"] = arbitrationCase.OrganizationName,
                ["reason"] = description,
                ["alpha_7_3"] = true
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(ArbitrationCase arbitrationCase, LegacyEventType eventType, LegacyEventSeverity severity, string title, string summary) =>
        new(
            $"inbox:arbitration:{arbitrationCase.PersonId}:{eventType}:{Guid.NewGuid():N}",
            new DateTimeOffset(arbitrationCase.CreatedOn.Year, arbitrationCase.CreatedOn.Month, arbitrationCase.CreatedOn.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            severity,
            title,
            summary,
            arbitrationCase.PersonId);

    private static LeagueTransaction Transaction(ArbitrationCase arbitrationCase, LeagueTransactionType type, string description) =>
        new(
            $"transaction:arbitration:{arbitrationCase.PersonId}:{type}:{Guid.NewGuid():N}",
            new DateTimeOffset(arbitrationCase.CreatedOn.Year, arbitrationCase.CreatedOn.Month, arbitrationCase.CreatedOn.Day, 12, 0, 0, TimeSpan.Zero),
            arbitrationCase.OrganizationId,
            arbitrationCase.OrganizationName,
            arbitrationCase.PersonId,
            arbitrationCase.PlayerName,
            type,
            LeagueTransactionWireService.CategoryFor(type),
            description);

    private static ArbitrationResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        ArbitrationCase? arbitrationCase,
        IReadOnlyList<AlphaInboxItem> inbox,
        IReadOnlyList<LeagueTransaction> transactions,
        string message)
    {
        var result = new ArbitrationResult(success, scenario, arbitrationCase, inbox, transactions, message);
        result.Validate();
        return result;
    }

    private static Contract? CurrentContract(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .DistinctBy(contract => contract.ContractId)
            .Where(contract => contract.PersonId == personId)
            .OrderByDescending(contract => contract.Term.EndDate)
            .FirstOrDefault();

    private static ArbitrationRules? ArbitrationRulesFor(Rulebook rulebook) =>
        rulebook.ArbitrationRules ?? new ArbitrationRules
        {
            ArbitrationEnabled = rulebook.ContractRules?.ArbitrationEnabled == true,
            EligibilityAge = 22,
            AccruedSeasonsThreshold = 4,
            FilingWindowDaysAfterQualifyingOffer = 7,
            HearingStartDaysAfterFiling = 14,
            HearingEndDaysAfterFiling = 28,
            WalkAwayAllowed = true,
            MinimumAward = 775_000m,
            MaximumAward = 8_000_000m
        };

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(Math.Max(value, min), max <= 0 ? value : max);

    private static string Display(ArbitrationCaseStatus status) =>
        status switch
        {
            ArbitrationCaseStatus.NotEligible => "Not Eligible",
            ArbitrationCaseStatus.PlayerFiled => "Player Filed",
            ArbitrationCaseStatus.TeamFiled => "Team Filed",
            ArbitrationCaseStatus.HearingScheduled => "Hearing Scheduled",
            ArbitrationCaseStatus.SettledBeforeHearing => "Settled Before Hearing",
            ArbitrationCaseStatus.AwardIssued => "Award Issued",
            ArbitrationCaseStatus.WalkedAway => "Walked Away",
            _ => status.ToString()
        };
}
