using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class RfaUfaService
{
    public IReadOnlyList<PlayerRightsDecision> BuildRightsDecisions(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rules = RightsRules(rulebook ?? scenario.LeagueProfile.Rulebook);
        if (rules is null || !rules.RfaUfaSystemEnabled)
        {
            return Array.Empty<PlayerRightsDecision>();
        }

        var existing = scenario.PlayerRightsDecisions
            .GroupBy(decision => decision.PersonId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(decision => decision.LastUpdatedOn).First(), StringComparer.Ordinal);
        var decisions = new List<PlayerRightsDecision>();
        foreach (var contract in RelevantContracts(scenario, rules))
        {
            var current = existing.GetValueOrDefault(contract.PersonId);
            if (current is not null && IsResolvedStatus(current.RightsStatus))
            {
                decisions.Add(current);
                continue;
            }

            var built = BuildDecisionForContract(scenario, contract, rules, current);
            decisions.Add(built);
        }

        foreach (var current in existing.Values.Where(decision => decisions.All(item => item.PersonId != decision.PersonId)))
        {
            if (IsResolvedStatus(current.RightsStatus))
            {
                decisions.Add(current);
            }
        }

        var output = decisions
            .GroupBy(decision => decision.PersonId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(decision => decision.LastUpdatedOn).First())
            .OrderBy(decision => decision.ExpiryRule?.Deadline ?? decision.ContractExpiryDate ?? DateOnly.MaxValue)
            .ThenBy(decision => decision.PlayerName, StringComparer.Ordinal)
            .ToArray();
        foreach (var decision in output)
        {
            decision.Validate();
        }

        return output;
    }

    public NewGmScenarioSnapshot EnsureRights(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var decisions = BuildRightsDecisions(scenario, rulebook);
        var updated = scenario with { PlayerRightsDecisions = decisions };
        foreach (var decision in decisions.Where(decision => decision.RightsStatus == FreeAgentRightsStatus.UnrestrictedFreeAgent
            && decision.ContractExpiryDate <= updated.CurrentDate))
        {
            updated = AddToFreeAgentMarket(updated, decision);
        }

        updated.Validate();
        return updated;
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var withRights = EnsureRights(scenario, rulebook);
        return withRights.PlayerRightsDecisions
            .Where(decision => decision.IsOpenDecision)
            .Where(decision => decision.ExpiryRule is not null)
            .Where(decision => decision.ContractExpiryDate is null || decision.ContractExpiryDate <= withRights.CurrentDate.AddDays(400))
            .Select(decision =>
            {
                var days = decision.ExpiryRule!.Deadline.DayNumber - withRights.CurrentDate.DayNumber;
                var priority = days <= 3 ? ActionCenterPriority.Urgent : days <= 14 ? ActionCenterPriority.Important : ActionCenterPriority.Normal;
                return new ActionCenterItem(
                    $"action-center:rfa-ufa:{decision.PersonId}",
                    $"{decision.PlayerName}: {Display(decision.RightsStatus)} decision",
                    ActionCenterCategory.Contracts,
                    priority,
                    decision.ExpiryRule.Deadline,
                    decision.PersonId,
                    decision.PlayerName,
                    decision.OrganizationId,
                    decision.OrganizationName,
                    decision.Reason,
                    decision.RightsStatus == FreeAgentRightsStatus.PendingRfa
                        ? "A qualifying offer preserves club rights and keeps negotiation with the current team open."
                        : "UFA status means the club cannot retain rights unless the player signs a new deal.",
                    decision.Recommendation,
                    null,
                    null,
                    null);
            })
            .ToArray();
    }

    public RightsDecisionResult IssueQualifyingOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = EnsureRights(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var decision = prepared.PlayerRightsDecisions.FirstOrDefault(item => item.PersonId == personId);
        if (decision is null)
        {
            return Result(false, prepared, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No RFA/UFA rights decision was found for that player.");
        }

        if (decision.RightsStatus is not (FreeAgentRightsStatus.PendingRfa or FreeAgentRightsStatus.RestrictedFreeAgent or FreeAgentRightsStatus.RightsHeld))
        {
            return Result(false, prepared, decision, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"{decision.PlayerName} is not eligible for a qualifying offer.");
        }

        var offer = (decision.QualifyingOffer ?? BuildQualifyingOffer(prepared, decision, RightsRules(registry.Rulebook ?? prepared.LeagueProfile.Rulebook)!))
            with
            {
                IsIssued = true,
                IssuedOn = prepared.CurrentDate,
                AgentReaction = "Agent acknowledges the club retained rights; negotiation continues."
            };
        var updatedDecision = decision with
        {
            RightsStatus = FreeAgentRightsStatus.Qualified,
            ContractRightsStatus = ContractRightsStatus.Qualified,
            QualifyingOffer = offer,
            Recommendation = "Negotiate a contract before camp; the qualifying offer prevents silent rights loss.",
            AgentNote = "Agent expects a serious bridge or long-term offer now that rights are retained.",
            Reason = $"{decision.PlayerName} was qualified before the deadline. {decision.OrganizationName} keeps RFA rights.",
            LastUpdatedOn = prepared.CurrentDate
        };
        var updated = ReplaceDecision(prepared, updatedDecision);
        updated = AddHistory(updated, updatedDecision, PlayerRightsDecisionType.Qualify, $"{decision.OrganizationName} issued a qualifying offer to {decision.PlayerName}.");
        updated = AddCareerTimeline(updated, updatedDecision, "RFA rights retained", $"{decision.OrganizationName} qualified {decision.PlayerName}; RFA negotiation continues.");
        QueueEvent(registry, prepared.CurrentDate, LegacyEventType.PlayerQualifiedAsRfa, updatedDecision, "Qualifying offer issued", updatedDecision.Reason);
        var inbox = new[]
        {
            Inbox(updatedDecision, LegacyEventType.PlayerQualifiedAsRfa, LegacyEventSeverity.Notice, $"Qualified RFA: {decision.PlayerName}", $"{decision.PlayerName} remains tied to {decision.OrganizationName}. Qualifying offer: {offer.RequiredSalary:C0} {offer.Currency}.")
        };
        var transactions = new[] { Transaction(updatedDecision, LeagueTransactionType.RfaQualified, $"{decision.OrganizationName} qualified {decision.PlayerName} as an RFA.") };
        return Result(true, updated, updatedDecision, inbox, transactions, $"{decision.PlayerName} was qualified. Club rights are retained.");
    }

    public RightsDecisionResult DeclineQualifyingOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = EnsureRights(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var decision = prepared.PlayerRightsDecisions.FirstOrDefault(item => item.PersonId == personId);
        if (decision is null)
        {
            return Result(false, prepared, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No RFA/UFA rights decision was found for that player.");
        }

        var ufa = decision.RightsStatus is FreeAgentRightsStatus.PendingUfa or FreeAgentRightsStatus.UnrestrictedFreeAgent;
        var updatedDecision = decision with
        {
            RightsStatus = ufa ? FreeAgentRightsStatus.UnrestrictedFreeAgent : FreeAgentRightsStatus.NotQualified,
            ContractRightsStatus = ufa ? ContractRightsStatus.UnrestrictedFreeAgent : ContractRightsStatus.RightsReleased,
            QualifyingOffer = decision.QualifyingOffer is null ? null : decision.QualifyingOffer with { IsIssued = false, AgentReaction = "Agent expects open-market access." },
            RightsHolderOrganizationId = "none",
            RightsHolderTeamName = "none",
            Recommendation = "Player enters the free-agent market. Re-sign only through normal UFA/free-agent negotiation.",
            AgentNote = "Agent expects market access and will compare role, money, relationship, and team fit.",
            Reason = ufa
                ? $"{decision.PlayerName} reached UFA status and enters the market."
                : $"{decision.PlayerName} was not qualified; club rights were released and the player enters the market.",
            LastUpdatedOn = prepared.CurrentDate
        };
        var updated = ReplaceDecision(prepared, updatedDecision);
        updated = AddToFreeAgentMarket(updated, updatedDecision);
        updated = AddHistory(updated, updatedDecision, ufa ? PlayerRightsDecisionType.ReleaseRights : PlayerRightsDecisionType.DoNotQualify, updatedDecision.Reason);
        updated = AddCareerTimeline(updated, updatedDecision, ufa ? "Became UFA" : "Rights released", updatedDecision.Reason);
        var eventType = ufa ? LegacyEventType.PlayerBecameUfa : LegacyEventType.PlayerNotQualified;
        QueueEvent(registry, prepared.CurrentDate, eventType, updatedDecision, ufa ? "Player became UFA" : "Player not qualified", updatedDecision.Reason);
        var inbox = new[]
        {
            Inbox(updatedDecision, eventType, LegacyEventSeverity.Warning, ufa ? $"UFA market: {decision.PlayerName}" : $"Rights released: {decision.PlayerName}", updatedDecision.Reason)
        };
        var transactions = new[]
        {
            Transaction(updatedDecision, ufa ? LeagueTransactionType.PlayerBecameUfa : LeagueTransactionType.RfaNotQualified, updatedDecision.Reason)
        };
        return Result(true, updated, updatedDecision, inbox, transactions, updatedDecision.Reason);
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId, Rulebook? rulebook = null)
    {
        var prepared = EnsureRights(scenario, rulebook);
        var decision = prepared.PlayerRightsDecisions.FirstOrDefault(item => item.PersonId == personId);
        var history = prepared.RightsHistory.ForPlayer(personId).Take(4).ToArray();
        if (decision is null && history.Length == 0)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>();
        if (decision is not null)
        {
            lines.Add($"RFA/UFA status: {Display(decision.RightsStatus)}.");
            lines.Add($"Contract rights: {Display(decision.ContractRightsStatus)}.");
            lines.Add($"Rights holder: {(string.IsNullOrWhiteSpace(decision.RightsHolderTeamName) ? "none" : decision.RightsHolderTeamName)}.");
            lines.Add($"Age/accrued seasons: {decision.Age?.ToString() ?? "unknown"} / {decision.AccruedSeasons}.");
            lines.Add($"Expiry: {decision.ContractExpiryDate?.ToString("yyyy-MM-dd") ?? "unknown"}.");
            if (decision.QualifyingOffer is not null)
            {
                lines.Add($"Qualifying offer: {decision.QualifyingOffer.RequiredSalary:C0} {decision.QualifyingOffer.Currency}, deadline {decision.QualifyingOffer.Deadline:yyyy-MM-dd}, {(decision.QualifyingOffer.IsIssued ? "issued" : "not issued")}.");
            }

            lines.Add($"Agent note: {decision.AgentNote}");
            lines.Add($"Recommendation: {decision.Recommendation}");
        }

        foreach (var entry in history)
        {
            lines.Add($"Rights history: {entry.Date:yyyy-MM-dd} - {entry.Summary}");
        }

        return lines;
    }

    private static PlayerRightsDecision BuildDecisionForContract(NewGmScenarioSnapshot scenario, Contract contract, FreeAgentRightsRules rules, PlayerRightsDecision? current)
    {
        var person = FindPerson(scenario, contract.PersonId);
        var name = person?.Identity.DisplayName ?? PersonName(scenario, contract.PersonId);
        var age = PersonAge(scenario, contract.PersonId);
        var accrued = AccruedSeasons(scenario, contract.PersonId, age);
        var isUfa = IsUfa(age, accrued, rules);
        var rightsStatus = current?.RightsStatus switch
        {
            FreeAgentRightsStatus.Qualified => FreeAgentRightsStatus.Qualified,
            FreeAgentRightsStatus.RestrictedFreeAgent => FreeAgentRightsStatus.RestrictedFreeAgent,
            FreeAgentRightsStatus.UnrestrictedFreeAgent => FreeAgentRightsStatus.UnrestrictedFreeAgent,
            _ => isUfa
                ? contract.Term.EndDate <= scenario.CurrentDate || contract.Status == ContractStatus.Expired
                    ? FreeAgentRightsStatus.UnrestrictedFreeAgent
                    : FreeAgentRightsStatus.PendingUfa
                : FreeAgentRightsStatus.PendingRfa
        };
        var contractStatus = rightsStatus switch
        {
            FreeAgentRightsStatus.PendingRfa => ContractRightsStatus.PendingRfa,
            FreeAgentRightsStatus.PendingUfa => ContractRightsStatus.PendingUfa,
            FreeAgentRightsStatus.Qualified => ContractRightsStatus.Qualified,
            FreeAgentRightsStatus.UnrestrictedFreeAgent => ContractRightsStatus.UnrestrictedFreeAgent,
            FreeAgentRightsStatus.RestrictedFreeAgent => ContractRightsStatus.RestrictedFreeAgent,
            _ => ContractRightsStatus.UnderContract
        };
        var deadlineBase = contract.Term.EndDate > scenario.CurrentDate ? contract.Term.EndDate : scenario.CurrentDate;
        var deadline = deadlineBase.AddDays(rules.QualifyingOfferDeadlineDaysAfterExpiry);
        var expiryRule = new RightsExpiryRule(
            rules.RightsExpiryRule,
            isUfa ? "UFA rights cannot be retained after expiry unless the player signs." : "Qualify by the rulebook deadline or release RFA rights.",
            deadline);
        var decision = new PlayerRightsDecision(
            DecisionId: current?.DecisionId ?? $"rights:{contract.PersonId}:{contract.ContractId}",
            PersonId: contract.PersonId,
            PlayerName: name,
            Position: PositionFor(scenario, contract.PersonId),
            Age: age,
            AccruedSeasons: accrued,
            OrganizationId: scenario.Organization.OrganizationId,
            OrganizationName: scenario.Organization.Name,
            ContractId: contract.ContractId,
            ContractExpiryDate: contract.Term.EndDate,
            RightsStatus: rightsStatus,
            ContractRightsStatus: contractStatus,
            QualifyingOfferRequired: !isUfa && rules.QualifyingOfferRequired,
            QualifyingOffer: isUfa ? null : current?.QualifyingOffer ?? BuildQualifyingOffer(scenario, contract, name, rules, deadline),
            ExpiryRule: expiryRule,
            RightsHolderOrganizationId: isUfa ? "none" : scenario.Organization.OrganizationId,
            RightsHolderTeamName: isUfa ? "none" : scenario.Organization.Name,
            Recommendation: isUfa ? "Prepare a UFA re-sign or walk-away decision; rights will not be retained." : "Review and issue a qualifying offer if the club wants to retain RFA rights.",
            AgentNote: isUfa ? "Agent is preparing for open-market conversations." : "Agent expects a qualifying offer to keep negotiations open.",
            Reason: isUfa
                ? $"{name} meets UFA threshold by age or accrued seasons."
                : $"{name} is below the configured UFA threshold and projects as RFA eligible.",
            CreatedOn: current?.CreatedOn ?? scenario.CurrentDate,
            LastUpdatedOn: current?.LastUpdatedOn ?? scenario.CurrentDate);
        decision.Validate();
        return decision;
    }

    private static QualifyingOffer BuildQualifyingOffer(NewGmScenarioSnapshot scenario, PlayerRightsDecision decision, FreeAgentRightsRules rules)
    {
        var contract = scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .FirstOrDefault(contract => contract.ContractId == decision.ContractId);
        var deadline = decision.ExpiryRule?.Deadline ?? scenario.CurrentDate.AddDays(rules.QualifyingOfferDeadlineDaysAfterExpiry);
        return BuildQualifyingOffer(scenario, contract, decision.PersonId, decision.PlayerName, rules, deadline);
    }

    private static QualifyingOffer BuildQualifyingOffer(NewGmScenarioSnapshot scenario, Contract? contract, string playerName, FreeAgentRightsRules rules, DateOnly deadline) =>
        BuildQualifyingOffer(scenario, contract, contract?.PersonId ?? string.Empty, playerName, rules, deadline);

    private static QualifyingOffer BuildQualifyingOffer(NewGmScenarioSnapshot scenario, Contract? contract, string personId, string playerName, FreeAgentRightsRules rules, DateOnly deadline)
    {
        var salary = contract is null ? rules.MinimumQualifyingOffer : Math.Max(contract.Money.SalaryOrStipend * rules.QualifyingOfferSalaryMultiplier, rules.MinimumQualifyingOffer);
        return new QualifyingOffer(
            $"qo:{personId}:{deadline:yyyyMMdd}",
            personId,
            playerName,
            salary,
            contract?.Money.Currency ?? "USD",
            deadline,
            "Issuing the qualifying offer preserves RFA rights for the current club.",
            "Budget impact is placeholder-only for Alpha 7.2; salary is shown for decision context.",
            "Agent expects a qualifying offer before serious negotiation continues.",
            IsIssued: false);
    }

    private static NewGmScenarioSnapshot ReplaceDecision(NewGmScenarioSnapshot scenario, PlayerRightsDecision decision)
    {
        var decisions = scenario.PlayerRightsDecisions
            .Where(item => item.PersonId != decision.PersonId)
            .Append(decision)
            .OrderBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = scenario with { PlayerRightsDecisions = decisions };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot AddToFreeAgentMarket(NewGmScenarioSnapshot scenario, PlayerRightsDecision decision)
    {
        if (scenario.FreeAgentMarket?.Find(decision.PersonId) is not null)
        {
            return scenario;
        }

        var person = FindPerson(scenario, decision.PersonId);
        var prior = scenario.PriorSeasonStats.OrderByDescending(stat => stat.SeasonYear).FirstOrDefault(stat => stat.PersonId == decision.PersonId)
            ?? new PriorSeasonStatLine(decision.PersonId, decision.PlayerName, scenario.Season.Year, decision.OrganizationName, scenario.Season.LeagueId, decision.Position == RosterPosition.Unknown ? RosterPosition.Center : decision.Position, 0);
        var career = scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == decision.PersonId)
            ?? new CareerStatSummary(decision.PersonId, decision.PlayerName, decision.Position == RosterPosition.Unknown ? RosterPosition.Center : decision.Position, 0, 0, PrimaryLeague: scenario.Season.LeagueId);
        var contract = scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.PersonId == decision.PersonId)
            .OrderByDescending(contract => contract.Term.EndDate)
            .FirstOrDefault();
        var freeAgent = new FreeAgent(
            decision.PersonId,
            decision.PlayerName,
            decision.Position == RosterPosition.Unknown ? prior.Position : decision.Position,
            decision.Position == RosterPosition.Goalie ? "Catches L" : "Shoots L",
            decision.Age ?? person?.CalculateAge(scenario.CurrentDate) ?? 26,
            HeightInches: 72,
            WeightPounds: 190,
            person?.Identity.Nationality ?? "Canada",
            person?.Identity.Birthplace ?? "Unknown",
            decision.OrganizationName,
            prior,
            career,
            "Standard market medical review required.",
            "Free-agent development trend pending staff review.",
            PlayerTypeFor(decision.Position),
            RoleFor(decision.Position),
            new FreeAgentContractAsk(
                contract?.ContractType ?? ContractType.JuniorPlayerAgreement,
                Math.Max(contract?.Money.SalaryOrStipend ?? 950_000m, 775_000m),
                contract?.Money.Currency ?? "USD",
                1,
                "Market ask created from released RFA/UFA rights."),
            new FreeAgentInterest(45, "League interest expected", "Player is weighing money, role, relationship, and team fit."),
            "No current club rights retained.",
            ScoutingConfidenceLevel.High,
            new FreeAgentFitSummary("Replacement/retention review", "Counts against player budget if signed.", "Review before re-signing.", "Market competition may increase cost.", 55),
            FreeAgentStatus.Available,
            IsShortlisted: false);
        var market = scenario.FreeAgentMarket ?? new FreeAgentMarket($"free-agent-market:{scenario.Season.Year}", scenario.CurrentDate, Array.Empty<FreeAgent>());
        var updatedMarket = market with
        {
            FreeAgents = market.FreeAgents.Append(freeAgent).DistinctBy(agent => agent.PersonId).OrderBy(agent => agent.Name, StringComparer.Ordinal).ToArray()
        };
        var updated = scenario with { FreeAgentMarket = updatedMarket };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot AddHistory(NewGmScenarioSnapshot scenario, PlayerRightsDecision decision, PlayerRightsDecisionType decisionType, string summary)
    {
        var entry = new RightsHistoryEntry(
            $"rights-history:{decision.PersonId}:{decisionType}:{scenario.CurrentDate:yyyyMMdd}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            decision.PersonId,
            decision.PlayerName,
            decision.RightsStatus,
            decisionType,
            decision.OrganizationId,
            decision.OrganizationName,
            summary);
        var history = scenario.RightsHistory.Add(entry);
        var transaction = new TransactionHistoryRecord(
            $"transaction-history:rights:{decision.PersonId}:{decisionType}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            scenario.Season.Year,
            decisionType.ToString(),
            decision.PersonId,
            decision.PlayerName,
            decision.OrganizationId,
            decision.OrganizationName,
            summary);
        return scenario with
        {
            RightsHistory = history,
            TransactionHistory = scenario.TransactionHistory.Append(transaction).ToArray()
        };
    }

    private static NewGmScenarioSnapshot AddCareerTimeline(NewGmScenarioSnapshot scenario, PlayerRightsDecision decision, string title, string description)
    {
        var entry = new CareerTimelineEntry(
            $"career:rights:{decision.PersonId}:{decision.RightsStatus}:{scenario.CurrentDate:yyyyMMdd}:{Guid.NewGuid():N}",
            CareerTimelineEntryType.Signed,
            scenario.CurrentDate,
            scenario.Season.Year,
            decision.PersonId,
            decision.OrganizationId,
            decision.OrganizationName,
            title,
            description,
            null,
            HistoryImportance.Important);
        return scenario with { CareerTimeline = scenario.CareerTimeline.Add(entry) };
    }

    private static void QueueEvent(EngineRegistry registry, DateOnly date, LegacyEventType eventType, PlayerRightsDecision decision, string title, string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: decision.PersonId, OrganizationId: decision.OrganizationId),
            new Dictionary<string, object?>
            {
                ["player_name"] = decision.PlayerName,
                ["team_name"] = decision.OrganizationName,
                ["reason"] = decision.Reason,
                ["alpha_7_2"] = true
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(PlayerRightsDecision decision, LegacyEventType eventType, LegacyEventSeverity severity, string title, string summary) =>
        new(
            $"inbox:rights:{decision.PersonId}:{eventType}:{Guid.NewGuid():N}",
            new DateTimeOffset(decision.LastUpdatedOn.Year, decision.LastUpdatedOn.Month, decision.LastUpdatedOn.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            severity,
            title,
            summary,
            decision.PersonId);

    private static LeagueTransaction Transaction(PlayerRightsDecision decision, LeagueTransactionType type, string description) =>
        new(
            $"transaction:rights:{decision.PersonId}:{type}:{Guid.NewGuid():N}",
            new DateTimeOffset(decision.LastUpdatedOn.Year, decision.LastUpdatedOn.Month, decision.LastUpdatedOn.Day, 12, 0, 0, TimeSpan.Zero),
            decision.OrganizationId,
            decision.OrganizationName,
            decision.PersonId,
            decision.PlayerName,
            type,
            LeagueTransactionWireService.CategoryFor(type),
            description);

    private static RightsDecisionResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        PlayerRightsDecision? decision,
        IReadOnlyList<AlphaInboxItem> inbox,
        IReadOnlyList<LeagueTransaction> transactions,
        string message)
    {
        var result = new RightsDecisionResult(success, scenario, decision, inbox, transactions, message);
        result.Validate();
        return result;
    }

    private static IReadOnlyList<Contract> RelevantContracts(NewGmScenarioSnapshot scenario, FreeAgentRightsRules rules) =>
        scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .DistinctBy(contract => contract.ContractId)
            .Where(contract => contract.OrganizationId == scenario.Organization.OrganizationId)
            .Where(contract => contract.Status is ContractStatus.Signed or ContractStatus.Expired)
            .Where(contract => FindPerson(scenario, contract.PersonId) is not null)
            // A replacement contract supersedes the old expiry record for rights purposes.
            // Without this grouping, a signed extension can leave a stale RFA decision behind.
            .GroupBy(contract => contract.PersonId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(contract => contract.Term.EndDate)
                .ThenByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
                .First())
            .Where(contract => contract.Term.EndDate <= scenario.CurrentDate.AddDays(rules.ContractTenderWindowDays)
                || contract.Term.EndDate <= scenario.CurrentDate
                || contract.Status == ContractStatus.Expired)
            .ToArray();

    private static bool IsResolvedStatus(FreeAgentRightsStatus status) =>
        status is FreeAgentRightsStatus.Qualified
            or FreeAgentRightsStatus.NotQualified
            or FreeAgentRightsStatus.RightsReleased
            or FreeAgentRightsStatus.SignedElsewhere
            or FreeAgentRightsStatus.UnrestrictedFreeAgent;

    private static bool IsUfa(int? age, int accruedSeasons, FreeAgentRightsRules rules) =>
        (age.HasValue && age.Value >= rules.UfaAge) || accruedSeasons >= rules.UfaAccruedSeasonsThreshold;

    private static FreeAgentRightsRules? RightsRules(Rulebook rulebook) =>
        rulebook.FreeAgentRightsRules ?? new FreeAgentRightsRules
        {
            RfaUfaSystemEnabled = false,
            RightsExpiryRule = "disabled",
            QualifyingOfferRequired = false,
            ContractTenderWindowDays = 0,
            QualifyingOfferDeadlineDaysAfterExpiry = 0
        };

    private static int? PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        FindPerson(scenario, personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Age
        ?? scenario.FreeAgentMarket?.Find(personId)?.Age;

    private static int AccruedSeasons(NewGmScenarioSnapshot scenario, string personId, int? age) =>
        scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == personId)?.Seasons
        ?? Math.Max(0, (age ?? 18) - 18);

    private static RosterPosition PositionFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == personId)?.Position
        ?? RosterPosition.Center;

    private static Person? FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId);

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        FindPerson(scenario, personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? personId;

    private static string PlayerTypeFor(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "Goalie",
            RosterPosition.Defense => "Defense",
            RosterPosition.Center => "Center",
            RosterPosition.LeftWing or RosterPosition.RightWing => "Winger",
            _ => "Skater"
        };

    private static string RoleFor(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "Depth/starter competition goalie",
            RosterPosition.Defense => "Depth defense / second-pair upside",
            RosterPosition.Center => "Middle-six center option",
            RosterPosition.LeftWing or RosterPosition.RightWing => "Middle-six wing option",
            _ => "Roster depth option"
        };

    private static string Display(object value) =>
        value.ToString() switch
        {
            "PendingRfa" => "Pending RFA",
            "RestrictedFreeAgent" => "Restricted Free Agent",
            "PendingUfa" => "Pending UFA",
            "UnrestrictedFreeAgent" => "Unrestricted Free Agent",
            "UnderContract" => "Under Contract",
            "NotQualified" => "Not Qualified",
            "RightsHeld" => "Rights Held",
            "RightsReleased" => "Rights Released",
            "SignedElsewhere" => "Signed Elsewhere",
            var text => text ?? "Unknown"
        };
}
