using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class BuyoutService
{
    public BuyoutWindow BuildWindow(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rules = BuyoutRulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        if (rules is null || !rules.BuyoutsEnabled)
        {
            var disabled = new BuyoutWindow(
                $"buyout-window:{scenario.Season.Year}:disabled",
                scenario.CurrentDate,
                scenario.CurrentDate,
                false,
                0,
                "Buyouts are disabled by this rulebook.");
            disabled.Validate();
            return disabled;
        }

        var calendar = SeasonCalendar.Build(scenario.Season.Year, scenario.Season.Settings);
        var opens = calendar.SeasonStart.Value.AddDays(rules.BuyoutWindowStartOffsetDays);
        var closes = calendar.SeasonStart.Value.AddDays(rules.BuyoutWindowEndOffsetDays);
        if (closes < opens)
        {
            closes = opens;
        }

        var isOpen = scenario.CurrentDate >= opens && scenario.CurrentDate <= closes;
        var daysUntilClose = isOpen ? Math.Max(0, closes.DayNumber - scenario.CurrentDate.DayNumber) : 0;
        var window = new BuyoutWindow(
            $"buyout-window:{scenario.Season.Year}:{opens:yyyyMMdd}:{closes:yyyyMMdd}",
            opens,
            closes,
            isOpen,
            daysUntilClose,
            isOpen
                ? $"Buyout window is open until {closes:yyyy-MM-dd}."
                : scenario.CurrentDate < opens
                    ? $"Buyout window opens {opens:yyyy-MM-dd}."
                    : $"Buyout window expired {closes:yyyy-MM-dd}.");
        window.Validate();
        return window;
    }

    public IReadOnlyList<BuyoutEligibility> BuildEligibility(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rules = BuyoutRulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        var window = BuildWindow(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        if (rules is null || !rules.BuyoutsEnabled)
        {
            return SignedPlayerContracts(scenario)
                .Select(contract => EligibilityFor(scenario, contract, null, window))
                .ToArray();
        }

        var output = SignedPlayerContracts(scenario)
            .Select(contract => EligibilityFor(scenario, contract, rules, window))
            .OrderByDescending(item => item.Status == BuyoutStatus.Eligible)
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        foreach (var item in output)
        {
            item.Validate();
        }

        return output;
    }

    public BuyoutResult CalculateBuyout(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rules = BuyoutRulesFor(registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var eligibility = BuildEligibility(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook)
            .FirstOrDefault(item => item.PersonId == personId);
        if (rules is null || eligibility is null || !eligibility.IsEligible)
        {
            return Result(false, scenario, null, eligibility, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), eligibility?.Reason ?? "Buyout is not available for this player.");
        }

        var contract = SignedPlayerContracts(scenario).First(contract => contract.ContractId == eligibility.ContractId);
        var calculation = BuildCalculation(scenario, contract, eligibility, rules);
        var buyout = new ContractBuyout(
            $"buyout:{personId}:{contract.ContractId}",
            personId,
            eligibility.PlayerName,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            contract.ContractId,
            BuyoutStatus.PendingConfirmation,
            scenario.CurrentDate,
            null,
            calculation,
            "Confirm only if the roster/budget benefit is worth carrying future penalty seasons.",
            $"{eligibility.PlayerName}'s agent will view a buyout as a negative relationship event.",
            "Owner and hockey staff will expect a clear replacement plan before absorbing dead cap or budget cost.");
        buyout.Validate();

        var updated = ReplaceBuyout(scenario, buyout);
        updated = AddHistory(updated, buyout, BuyoutDecisionType.Calculate, $"Buyout calculation prepared for {buyout.PlayerName}: cost {buyout.Calculation.BuyoutCost:C0} across {buyout.Calculation.PenaltySeasons} season(s).");
        var inbox = new[] { Inbox(buyout, LegacyEventType.BuyoutCalculated, LegacyEventSeverity.Notice, $"Buyout calculation ready: {buyout.PlayerName}", $"{buyout.PlayerName}'s buyout would cost {calculation.BuyoutCost:C0}, with an annual penalty of {calculation.AnnualPenalty:C0} across {calculation.PenaltySeasons} season(s). Confirm only if you want to release the player.") };
        return Result(true, updated, buyout, eligibility, inbox, Array.Empty<LeagueTransaction>(), $"Buyout calculation prepared for {buyout.PlayerName}.");
    }

    public BuyoutResult ConfirmBuyout(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var pending = scenario.ContractBuyouts.FirstOrDefault(item => item.PersonId == personId && item.Status == BuyoutStatus.PendingConfirmation);
        if (pending is null)
        {
            return Result(false, scenario, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Calculate the buyout before confirming it.");
        }

        var eligibility = BuildEligibility(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook).FirstOrDefault(item => item.PersonId == personId);
        if (eligibility is null || !eligibility.IsEligible)
        {
            return Result(false, scenario, pending, eligibility, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), eligibility?.Reason ?? "Buyout is no longer eligible.");
        }

        var completed = pending with
        {
            Status = BuyoutStatus.Completed,
            ConfirmedOn = scenario.CurrentDate,
            Recommendation = "Buyout completed. The player is now in the free-agent market and the penalty schedule remains on the club ledger.",
            PlayerAgentReaction = $"{pending.PlayerName}'s relationship with the front office took a hit because the contract was terminated by buyout.",
            OwnerStaffReaction = $"The front office now carries {pending.Calculation.AnnualPenalty:C0} annual penalty for {pending.Calculation.PenaltySeasons} season(s)."
        };
        completed.Validate();

        var updated = TerminateContract(scenario, completed);
        updated = ReplaceBuyout(updated, completed);
        updated = AddToFreeAgentMarket(updated, completed);
        updated = AddHistory(updated, completed, BuyoutDecisionType.Confirm, $"{completed.OrganizationName} bought out {completed.PlayerName}; future penalty {completed.Calculation.AnnualPenalty:C0} for {completed.Calculation.PenaltySeasons} season(s).");
        updated = new RelationshipExpansionService().RecordChange(
            updated,
            updated.AlphaSnapshot.GeneralManager.PersonId,
            completed.PersonId,
            ExpandedRelationshipType.GmPlayer,
            RelationshipChangeTrigger.Release,
            -12,
            updated.CurrentDate,
            "Contract buyout ended the player's agreement early.",
            $"{completed.PlayerName} may need trust rebuilt after being bought out.",
            null);
        updated = RecordAgentImpact(updated, completed);

        QueueEvent(registry, updated, completed, LegacyEventType.ContractBoughtOut, "Contract bought out", completed.Recommendation);
        var inbox = new[]
        {
            Inbox(completed, LegacyEventType.ContractBoughtOut, LegacyEventSeverity.Warning, $"Buyout completed: {completed.PlayerName}", $"{completed.PlayerName} was released to free agency by buyout. Annual penalty: {completed.Calculation.AnnualPenalty:C0} for {completed.Calculation.PenaltySeasons} season(s)."),
            Inbox(completed, LegacyEventType.RelationshipChanged, LegacyEventSeverity.Notice, $"Agent reaction: {completed.PlayerName}", completed.PlayerAgentReaction)
        };
        var transactions = new[]
        {
            Transaction(completed, $"{completed.OrganizationName} bought out {completed.PlayerName}; penalty schedule: {completed.Calculation.AnnualPenalty:C0} for {completed.Calculation.PenaltySeasons} season(s).")
        };
        return Result(true, updated, completed, eligibility, inbox, transactions, $"Buyout confirmed for {completed.PlayerName}.");
    }

    public BuyoutResult CancelBuyout(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var pending = scenario.ContractBuyouts.FirstOrDefault(item => item.PersonId == personId && item.Status == BuyoutStatus.PendingConfirmation);
        if (pending is null)
        {
            return Result(false, scenario, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No pending buyout confirmation was found.");
        }

        var canceled = pending with
        {
            Status = BuyoutStatus.Blocked,
            Recommendation = "Buyout calculation canceled by the GM. No contract or roster status changed.",
            OwnerStaffReaction = "No cap or budget penalty was applied."
        };
        var updated = ReplaceBuyout(scenario, canceled);
        updated = AddHistory(updated, canceled, BuyoutDecisionType.Cancel, $"Buyout calculation canceled for {canceled.PlayerName}; no contract change was made.");
        return Result(true, updated, canceled, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"Buyout canceled for {canceled.PlayerName}.");
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var items = new List<ActionCenterItem>();
        var rules = BuyoutRulesFor(rulebook ?? scenario.LeagueProfile.Rulebook);
        if (rules is null || !rules.BuyoutsEnabled)
        {
            return items;
        }

        var window = BuildWindow(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        if (window.IsOpen)
        {
            items.Add(new ActionCenterItem(
                $"action-center:buyout-window:{scenario.Season.Year}",
                window.DaysUntilClose <= 7 ? "Buyout window closing soon" : "Buyout window open",
                ActionCenterCategory.Contracts,
                window.DaysUntilClose <= 7 ? ActionCenterPriority.Important : ActionCenterPriority.Normal,
                window.ClosesOn,
                null,
                null,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                window.Summary,
                "Buyouts can clear unwanted contracts but create future cap or budget penalties.",
                "Review contracted players before confirming any buyout.",
                null,
                null,
                null));
        }

        foreach (var pending in scenario.ContractBuyouts.Where(item => item.Status == BuyoutStatus.PendingConfirmation))
        {
            items.Add(new ActionCenterItem(
                $"action-center:buyout-confirm:{pending.BuyoutId}",
                $"Buyout confirmation pending: {pending.PlayerName}",
                ActionCenterCategory.Contracts,
                ActionCenterPriority.Urgent,
                scenario.CurrentDate.AddDays(1),
                pending.PersonId,
                pending.PlayerName,
                pending.OrganizationId,
                pending.OrganizationName,
                $"A buyout calculation is waiting for confirmation. Annual penalty: {pending.Calculation.AnnualPenalty:C0}.",
                "Confirming releases the player to free agency and applies the penalty schedule.",
                "Confirm or cancel the buyout from the Contracts/Buyouts view.",
                null,
                null,
                null));
        }

        foreach (var completed in scenario.ContractBuyouts.Where(item => item.Status == BuyoutStatus.Completed && item.Calculation.Penalties.Any(penalty => penalty.SeasonYear == scenario.Season.Year)))
        {
            items.Add(new ActionCenterItem(
                $"action-center:buyout-penalty:{completed.BuyoutId}:{scenario.Season.Year}",
                $"Buyout penalty on ledger: {completed.PlayerName}",
                ActionCenterCategory.Contracts,
                ActionCenterPriority.Normal,
                null,
                completed.PersonId,
                completed.PlayerName,
                completed.OrganizationId,
                completed.OrganizationName,
                $"{completed.PlayerName}'s buyout penalty counts this season.",
                "Dead cap or operating budget penalties reduce flexibility even though the player is no longer on the roster.",
                "Account for the penalty before making major signings.",
                null,
                null,
                null));
        }

        foreach (var item in items)
        {
            item.Validate();
        }

        return items;
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var lines = new List<string>();
        var buyout = scenario.ContractBuyouts.FirstOrDefault(item => item.PersonId == personId);
        if (buyout is not null)
        {
            lines.Add($"Buyout status: {Display(buyout.Status)}.");
            lines.Add($"Buyout cost: {buyout.Calculation.BuyoutCost:C0}; annual penalty {buyout.Calculation.AnnualPenalty:C0} for {buyout.Calculation.PenaltySeasons} season(s).");
        }

        foreach (var entry in scenario.BuyoutHistory.ForPlayer(personId).Take(4))
        {
            lines.Add($"Buyout history: {entry.Date:yyyy-MM-dd} - {entry.Summary}");
        }

        return lines;
    }

    public string BuildRuleSummary(Rulebook rulebook)
    {
        var rules = BuyoutRulesFor(rulebook);
        if (rules is null || !rules.BuyoutsEnabled)
        {
            return "Buyouts disabled by rulebook.";
        }

        return $"Buyouts enabled | window offsets {rules.BuyoutWindowStartOffsetDays}-{rules.BuyoutWindowEndOffsetDays} | cost {rules.BuyoutCostPercentage:P0} | penalty years x{rules.PenaltyYearsMultiplier} | cap penalty {(rules.CapPenaltyEnabled ? "enabled" : "disabled")}.";
    }

    private static BuyoutEligibility EligibilityFor(NewGmScenarioSnapshot scenario, Contract contract, BuyoutRules? rules, BuyoutWindow window)
    {
        var personId = contract.PersonId;
        var playerName = PersonName(scenario, personId);
        var position = PositionFor(scenario, personId);
        var age = AgeFor(scenario, personId);
        var years = YearsRemaining(scenario.CurrentDate, contract.Term.EndDate);
        var pending = scenario.ContractBuyouts.FirstOrDefault(item => item.PersonId == personId && item.Status == BuyoutStatus.PendingConfirmation);

        BuyoutStatus status;
        string reason;
        string recommendation;
        if (rules is null || !rules.BuyoutsEnabled)
        {
            status = BuyoutStatus.NotEligible;
            reason = "Buyouts are disabled by this league rulebook.";
            recommendation = "Use trade, assignment, release, or future contract decisions instead.";
        }
        else if (pending is not null)
        {
            status = BuyoutStatus.PendingConfirmation;
            reason = "A buyout calculation is already pending confirmation.";
            recommendation = "Confirm or cancel the pending buyout before recalculating.";
        }
        else if (scenario.ArbitrationCases.Any(item => item.PersonId == personId && item.IsOpen))
        {
            status = BuyoutStatus.Blocked;
            reason = "Active arbitration case blocks buyout placeholder flow.";
            recommendation = "Resolve arbitration before buying out the contract.";
        }
        else if (scenario.TradeOffers.Any(offer =>
            offer.Status is TradeOfferStatus.Proposed or TradeOfferStatus.Countered
            && offer.PlayerGives.Concat(offer.PlayerReceives).Any(asset => asset.AssetId == personId)))
        {
            status = BuyoutStatus.Blocked;
            reason = "Pending trade placeholder blocks buyout until the trade decision is resolved.";
            recommendation = "Withdraw or resolve the trade before buying out this contract.";
        }
        else if (!window.IsOpen)
        {
            status = scenario.CurrentDate > window.ClosesOn ? BuyoutStatus.ExpiredWindow : BuyoutStatus.Blocked;
            reason = window.Summary;
            recommendation = "Wait for an open buyout window or use another roster path.";
        }
        else if (years < Math.Max(1, rules.MinimumContractRemainingYears))
        {
            status = BuyoutStatus.NotEligible;
            reason = $"Contract has {years} year(s) remaining; rulebook requires at least {rules.MinimumContractRemainingYears}.";
            recommendation = "Let the contract expire or use standard release paths.";
        }
        else
        {
            status = BuyoutStatus.Eligible;
            reason = $"{playerName} has {years} year(s) remaining and the buyout window is open.";
            recommendation = "Calculate the buyout before confirming; the future penalty remains on the ledger.";
        }

        var eligibility = new BuyoutEligibility(personId, playerName, contract.ContractId, position, age, years, status, reason, recommendation, window);
        eligibility.Validate();
        return eligibility;
    }

    private static BuyoutCalculation BuildCalculation(NewGmScenarioSnapshot scenario, Contract contract, BuyoutEligibility eligibility, BuyoutRules rules)
    {
        var years = Math.Max(1, eligibility.YearsRemaining);
        var remainingSalary = Math.Round(contract.Money.SalaryOrStipend * years + contract.Money.SigningBonus, 0);
        var cost = Math.Round(remainingSalary * rules.BuyoutCostPercentage, 0);
        var penaltySeasons = Math.Max(1, years * Math.Max(1, rules.PenaltyYearsMultiplier));
        var annualPenalty = Math.Round(cost / penaltySeasons, 0);
        var penalties = Enumerable.Range(0, penaltySeasons)
            .Select(index => new BuyoutPenalty(
                $"buyout-penalty:{contract.PersonId}:{contract.ContractId}:{scenario.Season.Year + index}",
                scenario.Season.Year + index,
                annualPenalty,
                contract.Money.Currency,
                $"{eligibility.PlayerName} buyout penalty season {index + 1} of {penaltySeasons}."))
            .ToArray();
        var current = rules.CapPenaltyEnabled ? penalties.Where(item => item.SeasonYear == scenario.Season.Year).Sum(item => item.Amount) : 0m;
        var future = rules.CapPenaltyEnabled ? penalties.Where(item => item.SeasonYear > scenario.Season.Year).Sum(item => item.Amount) : 0m;
        var calculation = new BuyoutCalculation(
            $"buyout-calculation:{contract.PersonId}:{contract.ContractId}:{scenario.CurrentDate:yyyyMMdd}",
            contract.PersonId,
            eligibility.PlayerName,
            contract.ContractId,
            remainingSalary,
            cost,
            penaltySeasons,
            annualPenalty,
            current,
            future,
            cost,
            penalties,
            $"{eligibility.PlayerName}'s remaining salary is {remainingSalary:C0}. Rulebook cost percentage {rules.BuyoutCostPercentage:P0} creates a total buyout cost of {cost:C0}.",
            rules.AgeBasedCostRulePlaceholder ?? "Age-based buyout rules are placeholder-only for Alpha 7.4.");
        calculation.Validate();
        return calculation;
    }

    private static NewGmScenarioSnapshot TerminateContract(NewGmScenarioSnapshot scenario, ContractBuyout buyout)
    {
        var scenarioContracts = scenario.Contracts
            .Select(contract => contract.ContractId == buyout.ContractId && contract.Status == ContractStatus.Signed ? contract.Terminate(scenario.CurrentDate) : contract)
            .ToArray();
        var alphaContracts = scenario.AlphaSnapshot.Contracts
            .Select(contract => contract.ContractId == buyout.ContractId && contract.Status == ContractStatus.Signed ? contract.Terminate(scenario.CurrentDate) : contract)
            .ToArray();
        var roster = scenario.AlphaSnapshot.Roster;
        var rosterPlayers = roster.Players
            .Select(player => player.PersonId == buyout.PersonId && player.CountsTowardRoster ? player.WithStatus(RosterStatus.Released, scenario.CurrentDate) : player)
            .ToArray();
        var snapshot = scenario.AlphaSnapshot with
        {
            Contracts = alphaContracts,
            Roster = roster with { Players = rosterPlayers }
        };
        var updated = scenario with { Contracts = scenarioContracts, AlphaSnapshot = snapshot };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot AddToFreeAgentMarket(NewGmScenarioSnapshot scenario, ContractBuyout buyout)
    {
        if (scenario.FreeAgentMarket?.Find(buyout.PersonId) is not null)
        {
            return scenario;
        }

        var person = FindPerson(scenario, buyout.PersonId);
        var position = PositionFor(scenario, buyout.PersonId);
        if (position == RosterPosition.Unknown)
        {
            position = RosterPosition.Center;
        }

        var prior = scenario.PriorSeasonStats.OrderByDescending(stat => stat.SeasonYear).FirstOrDefault(stat => stat.PersonId == buyout.PersonId)
            ?? new PriorSeasonStatLine(buyout.PersonId, buyout.PlayerName, scenario.Season.Year, buyout.OrganizationName, scenario.Season.LeagueId, position, 0);
        var career = scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == buyout.PersonId)
            ?? new CareerStatSummary(buyout.PersonId, buyout.PlayerName, position, 0, 0, PrimaryLeague: scenario.Season.LeagueId);
        var freeAgent = new FreeAgent(
            buyout.PersonId,
            buyout.PlayerName,
            position,
            position == RosterPosition.Goalie ? "Catches L" : "Shoots L",
            AgeFor(scenario, buyout.PersonId) ?? person?.CalculateAge(scenario.CurrentDate) ?? 27,
            HeightInches: 72,
            WeightPounds: 195,
            person?.Identity.Nationality ?? "Canada",
            person?.Identity.Birthplace ?? "Unknown",
            buyout.OrganizationName,
            prior,
            career,
            "Medical review required after buyout.",
            "Development/role trend will be reassessed on the market.",
            PlayerTypeFor(position),
            RoleFor(position),
            new FreeAgentContractAsk(
                ContractType.JuniorPlayerAgreement,
                Math.Max(775_000m, Math.Round(buyout.Calculation.AnnualPenalty * 1.5m, 0)),
                "USD",
                1,
                "Market ask created after contract buyout."),
            new FreeAgentInterest(35, "Market interest after buyout", "Player and agent will weigh role clarity, trust, and contract security."),
            "Player released by buyout; no active club contract rights retained in v1.",
            ScoutingConfidenceLevel.High,
            new FreeAgentFitSummary("Buyout market option", "New contract would add fresh payroll while old penalty remains.", "Review relationship and role before re-signing.", "Other teams may see value if price drops.", 45),
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

    private static NewGmScenarioSnapshot RecordAgentImpact(NewGmScenarioSnapshot scenario, ContractBuyout buyout)
    {
        var representation = scenario.AgentRepresentations.FirstOrDefault(item => item.PersonId == buyout.PersonId);
        if (representation?.AgentId is null)
        {
            return scenario;
        }

        return new RelationshipExpansionService().RecordChange(
            scenario,
            scenario.Organization.OrganizationId,
            representation.AgentId,
            ExpandedRelationshipType.OrganizationAgent,
            RelationshipChangeTrigger.Release,
            -6,
            scenario.CurrentDate,
            "Agent client was bought out.",
            $"Agent relationship cooled after {buyout.PlayerName}'s buyout.",
            null);
    }

    private static NewGmScenarioSnapshot AddHistory(NewGmScenarioSnapshot scenario, ContractBuyout buyout, BuyoutDecisionType decisionType, string summary)
    {
        var entry = new BuyoutHistoryEntry(
            $"buyout-history:{buyout.PersonId}:{decisionType}:{scenario.CurrentDate:yyyyMMdd}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            buyout.PersonId,
            buyout.PlayerName,
            buyout.Status,
            decisionType,
            buyout.OrganizationId,
            buyout.OrganizationName,
            buyout.Calculation.BuyoutCost,
            summary);
        var transaction = new TransactionHistoryRecord(
            $"transaction-history:buyout:{buyout.PersonId}:{decisionType}:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            scenario.Season.Year,
            $"Buyout{decisionType}",
            buyout.PersonId,
            buyout.PlayerName,
            buyout.OrganizationId,
            buyout.OrganizationName,
            summary);
        var careerEntry = new CareerTimelineEntry(
            $"career:buyout:{buyout.PersonId}:{decisionType}:{scenario.CurrentDate:yyyyMMdd}:{Guid.NewGuid():N}",
            decisionType == BuyoutDecisionType.Confirm ? CareerTimelineEntryType.Released : CareerTimelineEntryType.Signed,
            scenario.CurrentDate,
            scenario.Season.Year,
            buyout.PersonId,
            buyout.OrganizationId,
            buyout.OrganizationName,
            decisionType == BuyoutDecisionType.Confirm ? "Contract bought out" : "Buyout reviewed",
            summary,
            null,
            decisionType == BuyoutDecisionType.Confirm ? HistoryImportance.Major : HistoryImportance.Low);
        var updated = scenario with
        {
            BuyoutHistory = scenario.BuyoutHistory.Add(entry),
            TransactionHistory = scenario.TransactionHistory.Append(transaction).ToArray(),
            CareerTimeline = scenario.CareerTimeline.Add(careerEntry)
        };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot ReplaceBuyout(NewGmScenarioSnapshot scenario, ContractBuyout buyout)
    {
        var buyouts = scenario.ContractBuyouts
            .Where(item => item.BuyoutId != buyout.BuyoutId)
            .Append(buyout)
            .OrderByDescending(item => item.CreatedOn)
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = scenario with { ContractBuyouts = buyouts };
        updated.Validate();
        return updated;
    }

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, ContractBuyout buyout, LegacyEventType eventType, string title, string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12))), TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Warning,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: buyout.PersonId,
                OrganizationId: buyout.OrganizationId,
                LeagueId: scenario.Season.LeagueId,
                SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["buyout_id"] = buyout.BuyoutId,
                ["buyout_cost"] = buyout.Calculation.BuyoutCost,
                ["annual_penalty"] = buyout.Calculation.AnnualPenalty
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(ContractBuyout buyout, LegacyEventType eventType, LegacyEventSeverity severity, string title, string summary) =>
        new(
            $"inbox:buyout:{buyout.BuyoutId}:{eventType}:{Guid.NewGuid():N}",
            new DateTimeOffset((buyout.ConfirmedOn ?? buyout.CreatedOn).ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(13))), TimeSpan.Zero),
            eventType,
            severity,
            title,
            summary,
            buyout.PersonId);

    private static LeagueTransaction Transaction(ContractBuyout buyout, string description) =>
        new(
            $"transaction:buyout:{buyout.BuyoutId}:{Guid.NewGuid():N}",
            new DateTimeOffset((buyout.ConfirmedOn ?? buyout.CreatedOn).ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(13))), TimeSpan.Zero),
            buyout.OrganizationId,
            buyout.OrganizationName,
            buyout.PersonId,
            buyout.PlayerName,
            LeagueTransactionType.ContractBoughtOut,
            LeagueNewsCategory.Signings,
            description);

    private static BuyoutResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        ContractBuyout? buyout,
        BuyoutEligibility? eligibility,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        IReadOnlyList<LeagueTransaction> leagueTransactions,
        string message)
    {
        var result = new BuyoutResult(success, scenario, buyout, eligibility, inboxItems, leagueTransactions, message);
        result.Validate();
        return result;
    }

    private static IReadOnlyList<Contract> SignedPlayerContracts(NewGmScenarioSnapshot scenario) =>
        scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .Where(contract => contract.Status == ContractStatus.Signed
                && string.Equals(contract.OrganizationId, scenario.Organization.OrganizationId, StringComparison.Ordinal)
                && contract.ContractType == ContractType.JuniorPlayerAgreement)
            .ToArray();

    private static int YearsRemaining(DateOnly currentDate, DateOnly endDate) =>
        endDate <= currentDate ? 0 : Math.Max(1, (int)Math.Ceiling((endDate.DayNumber - currentDate.DayNumber) / 365m));

    private static Person? FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId);

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        FindPerson(scenario, personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? personId;

    private static int? AgeFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)?.Age
        ?? FindPerson(scenario, personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.FreeAgentMarket?.Find(personId)?.Age;

    private static RosterPosition PositionFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? RosterPosition.Unknown;

    private static string PlayerTypeFor(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "Goaltender",
            RosterPosition.Defense => "Defenseman",
            _ => "Forward"
        };

    private static string RoleFor(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "Depth goalie",
            RosterPosition.Defense => "Depth defense",
            _ => "Depth forward"
        };

    private static BuyoutRules? BuyoutRulesFor(Rulebook rulebook) =>
        rulebook.BuyoutRules ?? new BuyoutRules
        {
            BuyoutsEnabled = false,
            AgeBasedCostRulePlaceholder = "Buyouts disabled unless a rulebook explicitly enables them."
        };

    private static string Display(BuyoutStatus status) =>
        status switch
        {
            BuyoutStatus.NotEligible => "Not Eligible",
            BuyoutStatus.PendingConfirmation => "Pending Confirmation",
            BuyoutStatus.ExpiredWindow => "Expired Window",
            _ => status.ToString()
        };
}
