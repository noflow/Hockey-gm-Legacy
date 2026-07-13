using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

/// <summary>
/// Coordinates contract decisions across extensions, rights, arbitration, and free agency.
/// Explicit offers submitted from this market complete immediately when the agent accepts;
/// counteroffers remain open for another GM revision.
/// </summary>
public sealed class ContractMarketService
{
    private readonly ContractManagementService _contracts = new();

    public ContractMarketSummary BuildSummary(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var effectiveRulebook = rulebook ?? scenario.LeagueProfile.Rulebook;
        var prepared = new RfaUfaService().EnsureRights(scenario, effectiveRulebook);
        var active = ActiveContracts(prepared);
        var expiring = active
            .Where(contract => contract.Term.EndDate <= prepared.CurrentDate.AddDays(365))
            .OrderBy(contract => contract.Term.EndDate)
            .ThenBy(contract => contract.PersonId, StringComparer.Ordinal)
            .ToArray();
        var rights = prepared.PlayerRightsDecisions
            .Where(decision => decision.IsOpenDecision || decision.RightsStatus is FreeAgentRightsStatus.Qualified or FreeAgentRightsStatus.RightsHeld)
            .OrderBy(decision => decision.ContractExpiryDate ?? DateOnly.MaxValue)
            .ThenBy(decision => decision.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var freeAgents = prepared.FreeAgentMarket?.FreeAgents
            .Where(agent => agent.Status is not FreeAgentStatus.Signed and not FreeAgentStatus.Unavailable)
            .OrderBy(agent => agent.Name, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<FreeAgent>();
        var deadlines = BuildDeadlines(prepared, expiring, rights, effectiveRulebook);
        var summary = $"{expiring.Length} contract(s) expire within a year, {rights.Length} rights decision(s) are open, and {freeAgents.Length} free agent(s) are available. {prepared.ContractNegotiations.Count(item => item.IsOpen)} negotiation(s) are active.";
        var result = new ContractMarketSummary(
            prepared.ContractNegotiations.OrderByDescending(item => item.LastUpdatedOn).ToArray(),
            expiring,
            rights,
            freeAgents,
            deadlines,
            prepared.CurrentOrganizationPlan,
            summary);
        result.Validate();
        return result;
    }

    public IReadOnlyList<ContractComparable> BuildComparables(NewGmScenarioSnapshot scenario, string personId, int maximum = 5)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        var target = FindPlayerContext(scenario, personId);
        var contracts = ActiveContracts(scenario)
            .Where(contract => contract.PersonId != personId)
            .Select(contract =>
            {
                var context = FindPlayerContext(scenario, contract.PersonId);
                return new ContractComparable(
                    $"comparable:{contract.ContractId}",
                    contract.PersonId,
                    context.Name,
                    context.Position,
                    context.Age,
                    contract.Money.SalaryOrStipend,
                    Math.Max(1, contract.Term.EndDate.Year - contract.Term.StartDate.Year + 1),
                    RoleFor(context.Position),
                    "Current organization contract",
                    $"Signed {contract.SignedOn:yyyy-MM-dd}; expires {contract.Term.EndDate:yyyy-MM-dd}.");
            })
            .Where(comparable => target.Position == RosterPosition.Unknown || comparable.Position == target.Position)
            .OrderBy(comparable => comparable.AnnualSalary)
            .ThenBy(comparable => comparable.PlayerName, StringComparer.Ordinal)
            .Take(Math.Max(1, maximum))
            .ToArray();

        foreach (var comparable in contracts)
        {
            comparable.Validate();
        }

        return contracts;
    }

    public ContractMarketResult StartNegotiation(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(personId))
        {
            return Failure(scenario, "A player or staff member must be selected before opening negotiations.");
        }

        var existing = scenario.ContractNegotiations
            .Where(item => item.PersonId == personId && item.IsOpen)
            .OrderByDescending(item => item.LastUpdatedOn)
            .FirstOrDefault();
        if (existing is not null)
        {
            return Failure(scenario, $"Negotiations with {existing.PersonName} are already open.", existing);
        }

        var askType = DetermineAskType(scenario, personId);
        ContractAsk ask;
        try
        {
            ask = _contracts.BuildAsk(scenario, askType, personId);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Failure(scenario, exception.Message);
        }

        var status = MarketStatusFor(scenario, personId, askType);
        var demand = new ContractDemand(
            ask.PersonId,
            ask.PersonName,
            ask.RequestedSalary,
            ask.RequestedTermYears,
            ask.DesiredRole,
            $"Money {ask.Preference.MoneyImportance}/100; term {ask.Preference.TermImportance}/100; role {ask.Preference.RoleImportance}/100; development {ask.Preference.DevelopmentImportance}/100; relationship {ask.Preference.RelationshipImportance}/100.",
            string.Join(" ", ask.SigningPriority, ask.DevelopmentPathwayConcern, ask.StaffCoachConfidence),
            scenario.CurrentDate)
        {
            TeamPreference = ask.TeamPreference
        };
        var negotiation = new ContractNegotiation(
            $"contract-negotiation:{personId}:{Guid.NewGuid():N}",
            personId,
            ask.PersonName,
            scenario.Organization.OrganizationId,
            askType,
            ContractNegotiationStatus.InitialInterest,
            status,
            demand,
            null,
            null,
            scenario.CurrentDate,
            scenario.CurrentDate,
            DeadlineFor(scenario, personId, askType, registry.Rulebook ?? scenario.LeagueProfile.Rulebook),
            0,
            "Initial demand prepared from player, agent, role, relationship, and market context.",
            "Choose an offer and submit it for agent review.");
        negotiation.Validate();

        var next = ReplaceNegotiation(scenario, negotiation);
        var history = new ContractNegotiationHistoryEntry(
            $"contract-negotiation-history:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            personId,
            ask.PersonName,
            negotiation.Status,
            null,
            0,
            $"Negotiation opened. {ask.PersonName} is seeking {ask.RequestedTermYears} year(s) at {ask.RequestedSalary:C0} with a {ask.DesiredRole} role.");
        next = next with { ContractNegotiationHistory = next.ContractNegotiationHistory.Add(history) };
        QueueEvent(registry, next, LegacyEventType.ContractAskCreated, $"Contract market opened: {ask.PersonName}", $"The contract desk prepared a {Display(status).ToLowerInvariant()} negotiation for {ask.PersonName}.", personId);
        var inbox = new[] { Inbox(next, LegacyEventType.ContractAskCreated, $"Contract decision opened: {ask.PersonName}", $"{ask.PersonName} is seeking {ask.RequestedSalary:C0} per year for {ask.RequestedTermYears} year(s). Desired role: {ask.DesiredRole}.", personId) };
        return Success(next, negotiation, null, inbox, $"Contract negotiation opened for {ask.PersonName}.");
    }

    public ContractMarketResult SubmitOffer(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        decimal annualSalary,
        int termYears,
        string? rolePromise = null,
        string? notes = null,
        string? positionPromise = null,
        string? iceTimePromise = null,
        string? nhlRosterPromise = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var negotiation = scenario.ContractNegotiations
            .Where(item => item.PersonId == personId && item.IsOpen)
            .OrderByDescending(item => item.LastUpdatedOn)
            .FirstOrDefault();
        if (negotiation is null)
        {
            return Failure(scenario, "Open the contract negotiation before submitting an offer.");
        }

        var negotiationRules = registry.Rulebook?.ContractRules?.NegotiationRules;
        var maximumRounds = Math.Max(1, negotiationRules?.MaxRounds ?? 3);
        if (negotiation.Round >= maximumRounds)
        {
            return Failure(scenario, $"Negotiation limit reached after {maximumRounds} round(s). Use the last response, withdraw, or allow the player to enter the market.", negotiation);
        }

        annualSalary = annualSalary > 0 ? annualSalary : negotiation.Demand.AnnualSalary;
        termYears = termYears > 0 ? termYears : negotiation.Demand.TermYears;
        var request = new ContractOfferBuildRequest(
            personId,
            negotiation.AskType,
            annualSalary,
            termYears,
            rolePromise ?? negotiation.Demand.DesiredRole,
            negotiation.Demand.Priorities,
            negotiation.AskType is ContractAskType.Prospect or ContractAskType.Recruit,
            "No additional staff promise",
            notes ?? "Contract Market v4 offer",
            ContractTypeFor(negotiation.AskType))
        {
            PositionPromise = positionPromise,
            IceTimePromise = iceTimePromise,
            NhlRosterPromise = nhlRosterPromise
        };
        ContractManagementResult contractResult;
        try
        {
            contractResult = _contracts.SubmitOffer(registry, scenario, request, completeAcceptedOffer: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Failure(scenario, exception.Message, negotiation);
        }

        var evaluation = contractResult.Evaluation;
        var response = evaluation.Decision switch
        {
            ContractOfferDecision.Accepted => ContractOfferResponse.Accepted,
            ContractOfferDecision.Rejected => ContractOfferResponse.Rejected,
            ContractOfferDecision.WantsMore => ContractOfferResponse.Countered,
            _ => ContractOfferResponse.Waiting
        };
        var status = response switch
        {
            ContractOfferResponse.Accepted => ContractNegotiationStatus.Signed,
            ContractOfferResponse.Rejected => ContractNegotiationStatus.Rejected,
            ContractOfferResponse.Countered => ContractNegotiationStatus.Countered,
            _ => ContractNegotiationStatus.AgentReviewing
        };
        var offer = new ContractOffer(
            $"contract-market-offer:{negotiation.NegotiationId}:{negotiation.Round + 1}",
            personId,
            scenario.Organization.OrganizationId,
            request.ContractType ?? ContractType.JuniorPlayerAgreement,
            evaluation.Term,
            new ContractMoney(annualSalary, 0m, scenario.FreeAgentMarket?.Find(personId)?.ContractAsk.Currency ?? "USD"),
            Array.Empty<ContractClause>(),
            scenario.CurrentDate,
            request.Notes)
        {
            PositionPromise = request.PositionPromise,
            IceTimePromise = request.IceTimePromise,
            NhlRosterPromise = request.NhlRosterPromise
        };
        var nextNegotiation = negotiation with
        {
            Status = status,
            MarketStatus = status == ContractNegotiationStatus.Signed ? ContractMarketStatus.Signed : negotiation.MarketStatus,
            CurrentOffer = offer,
            LastEvaluation = evaluation,
            LastUpdatedOn = scenario.CurrentDate,
            DecisionDeadline = scenario.CurrentDate.AddDays(ResponseDays(registry.Rulebook ?? scenario.LeagueProfile.Rulebook)),
            Round = negotiation.Round + 1,
            LastResponse = evaluation.Explanation.Summary,
            NextAction = status switch
            {
                ContractNegotiationStatus.Signed => "Complete. The accepted offer has been signed.",
                ContractNegotiationStatus.Countered => $"Respond to the counter. {evaluation.AgentCounterSuggestion}",
                ContractNegotiationStatus.Rejected => "Withdraw, change the role, or revisit the market later.",
                _ => "Wait for the agent response."
            }
        };
        nextNegotiation.Validate();
        var next = ReplaceNegotiation(contractResult.ScenarioSnapshot, nextNegotiation);
        var history = new ContractNegotiationHistoryEntry(
            $"contract-negotiation-history:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            personId,
            negotiation.PersonName,
            status,
            response,
            nextNegotiation.Round,
            $"Offer {annualSalary:C0} for {termYears} year(s): {evaluation.Decision}. {evaluation.Explanation.Summary}");
        next = next with { ContractNegotiationHistory = next.ContractNegotiationHistory.Add(history) };
        QueueEvent(registry, next, LegacyEventType.ContractOfferSubmitted, $"Contract offer submitted: {negotiation.PersonName}", $"The GM offered {annualSalary:C0} per year for {termYears} year(s). {evaluation.Decision}.", personId);
        return Success(next, nextNegotiation, evaluation, contractResult.InboxItems, contractResult.Message, contractResult.LeagueTransactions);
    }

    public ContractMarketResult Respond(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        ContractOfferResponse response,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var negotiation = scenario.ContractNegotiations
            .Where(item => item.PersonId == personId && item.IsOpen)
            .OrderByDescending(item => item.LastUpdatedOn)
            .FirstOrDefault();
        if (negotiation is null)
        {
            return Failure(scenario, "No open contract negotiation was found.");
        }

        var status = response switch
        {
            ContractOfferResponse.Accepted => ContractNegotiationStatus.AcceptedInPrinciple,
            ContractOfferResponse.Rejected => ContractNegotiationStatus.Rejected,
            ContractOfferResponse.Countered => ContractNegotiationStatus.Countered,
            _ => ContractNegotiationStatus.AgentReviewing
        };
        var updated = negotiation with
        {
            Status = status,
            LastUpdatedOn = scenario.CurrentDate,
            LastResponse = note ?? $"GM recorded {response}.",
            NextAction = response == ContractOfferResponse.Accepted
                ? "Review the pending GM approval before signing."
                : response == ContractOfferResponse.Countered ? "Submit a revised offer or wait." : "No signing action is pending."
        };
        var next = ReplaceNegotiation(scenario, updated) with
        {
            ContractNegotiationHistory = scenario.ContractNegotiationHistory.Add(new ContractNegotiationHistoryEntry(
                $"contract-negotiation-history:{Guid.NewGuid():N}",
                scenario.CurrentDate,
                personId,
                updated.PersonName,
                status,
                response,
                updated.Round,
                updated.LastResponse))
        };
        return Success(next, updated, updated.LastEvaluation, Array.Empty<AlphaInboxItem>(), $"{updated.PersonName} negotiation marked {response}.");
    }

    public ContractMarketResult Withdraw(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId) =>
        Respond(registry, scenario, personId, ContractOfferResponse.Rejected, "GM withdrew the negotiation before signing.");

    private static NewGmScenarioSnapshot ReplaceNegotiation(NewGmScenarioSnapshot scenario, ContractNegotiation negotiation) =>
        scenario with
        {
            ContractNegotiations = scenario.ContractNegotiations
                .Where(item => item.PersonId != negotiation.PersonId || item.NegotiationId == negotiation.NegotiationId)
                .Where(item => item.NegotiationId != negotiation.NegotiationId)
                .Append(negotiation)
                .OrderByDescending(item => item.LastUpdatedOn)
                .ThenBy(item => item.PersonName, StringComparer.Ordinal)
                .ToArray()
        };

    private static ContractAskType DetermineAskType(NewGmScenarioSnapshot scenario, string personId)
    {
        if (scenario.FreeAgentMarket?.Find(personId) is not null) return ContractAskType.FreeAgent;
        if (scenario.ProspectRights.Any(item => item.ProspectPersonId == personId)) return ContractAskType.Prospect;
        if (scenario.AlphaSnapshot.Recruits.Any(item => item.RecruitPersonId == personId)) return ContractAskType.Recruit;
        if (scenario.StaffMembers.Any(item => item.PersonId == personId)) return ContractAskType.StaffMember;
        return ContractAskType.RosterPlayer;
    }

    private static ContractMarketStatus MarketStatusFor(NewGmScenarioSnapshot scenario, string personId, ContractAskType askType)
    {
        if (askType == ContractAskType.FreeAgent) return ContractMarketStatus.FreeAgent;
        var rights = scenario.PlayerRightsDecisions.FirstOrDefault(item => item.PersonId == personId);
        if (rights?.RightsStatus is FreeAgentRightsStatus.RestrictedFreeAgent or FreeAgentRightsStatus.PendingRfa or FreeAgentRightsStatus.Qualified)
            return ContractMarketStatus.Rfa;
        if (rights?.RightsStatus is FreeAgentRightsStatus.UnrestrictedFreeAgent or FreeAgentRightsStatus.PendingUfa)
            return ContractMarketStatus.Ufa;
        var contract = ActiveContracts(scenario).FirstOrDefault(item => item.PersonId == personId);
        return contract is not null && contract.Term.EndDate <= scenario.CurrentDate.AddDays(365)
            ? ContractMarketStatus.Expiring
            : contract is not null ? ContractMarketStatus.UnderContract : ContractMarketStatus.Negotiating;
    }

    private static DateOnly? DeadlineFor(NewGmScenarioSnapshot scenario, string personId, ContractAskType askType, Rulebook rulebook)
    {
        if (askType == ContractAskType.FreeAgent)
            return scenario.CurrentDate.AddDays(rulebook.ContractRules?.NegotiationRules?.OfferExpirationDays ?? 14);
        return ActiveContracts(scenario).FirstOrDefault(item => item.PersonId == personId)?.Term.EndDate
            ?? scenario.PlayerRightsDecisions.FirstOrDefault(item => item.PersonId == personId)?.ExpiryRule?.Deadline
            ?? scenario.CurrentDate.AddDays(rulebook.ContractRules?.NegotiationRules?.ExtensionWindowDays ?? 45);
    }

    private static int ResponseDays(Rulebook rulebook) => Math.Max(1, rulebook.ContractRules?.NegotiationRules?.ResponseDays ?? 7);

    private static ContractType ContractTypeFor(ContractAskType askType) => askType switch
    {
        ContractAskType.StaffMember => ContractType.StaffContract,
        ContractAskType.FreeAgent or ContractAskType.RosterPlayer => ContractType.JuniorPlayerAgreement,
        _ => ContractType.JuniorPlayerAgreement
    };

    private static IReadOnlyList<ContractDecisionDeadline> BuildDeadlines(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<Contract> expiring,
        IReadOnlyList<PlayerRightsDecision> rights,
        Rulebook? rulebook)
    {
        var deadlines = new List<ContractDecisionDeadline>();
        foreach (var contract in expiring)
        {
            var name = FindPlayerContext(scenario, contract.PersonId).Name;
            deadlines.Add(new ContractDecisionDeadline(
                $"deadline:extension:{contract.ContractId}",
                contract.PersonId,
                name,
                ContractDecisionDeadlineType.Extension,
                contract.Term.EndDate,
                "Let the contract expire or open an extension negotiation.",
                true));
        }

        foreach (var decision in rights.Where(item => item.ExpiryRule is not null))
        {
            deadlines.Add(new ContractDecisionDeadline(
                $"deadline:rights:{decision.DecisionId}",
                decision.PersonId,
                decision.PlayerName,
                decision.RightsStatus is FreeAgentRightsStatus.PendingRfa or FreeAgentRightsStatus.RestrictedFreeAgent
                    ? ContractDecisionDeadlineType.QualifyingOffer
                    : ContractDecisionDeadlineType.FreeAgency,
                decision.ExpiryRule!.Deadline,
                decision.Recommendation,
                decision.IsOpenDecision));
        }

        foreach (var arbitrationCase in scenario.ArbitrationCases.Where(item => item.IsOpen && item.FilingDeadline is not null))
        {
            deadlines.Add(new ContractDecisionDeadline(
                $"deadline:arbitration:{arbitrationCase.CaseId}",
                arbitrationCase.PersonId,
                arbitrationCase.PlayerName,
                ContractDecisionDeadlineType.ArbitrationFiling,
                arbitrationCase.FilingDeadline!.Value,
                "File, settle, or prepare for arbitration before the deadline.",
                true));
        }

        return deadlines.OrderBy(item => item.DueOn).ThenBy(item => item.PersonName, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<Contract> ActiveContracts(NewGmScenarioSnapshot scenario) =>
        scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .DistinctBy(contract => contract.ContractId)
            .Where(contract => contract.Status == ContractStatus.Signed)
            .ToArray();

    private static PlayerContext FindPlayerContext(NewGmScenarioSnapshot scenario, string personId)
    {
        var person = scenario.AlphaSnapshot.People.FirstOrDefault(item => item.PersonId == personId);
        var roster = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        var prospect = scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == personId);
        var freeAgent = scenario.FreeAgentMarket?.Find(personId);
        var staff = scenario.StaffMembers.FirstOrDefault(item => item.PersonId == personId);
        var name = person?.Identity.DisplayName
            ?? freeAgent?.Name
            ?? prospect?.ProspectName
            ?? personId;
        var position = freeAgent?.Position ?? prospect?.Position ?? roster?.Position ?? RosterPosition.Unknown;
        var age = freeAgent?.Age ?? prospect?.Age ?? roster?.Age;
        return new PlayerContext(name, position, age);
    }

    private static string RoleFor(RosterPosition position) => position switch
    {
        RosterPosition.Goalie => "Goalie",
        RosterPosition.Defense => "Defense",
        RosterPosition.Center => "Center",
        RosterPosition.LeftWing => "Left wing",
        RosterPosition.RightWing => "Right wing",
        _ => "Hockey role"
    };

    private static string Display(ContractMarketStatus status) => status switch
    {
        ContractMarketStatus.Rfa => "RFA",
        ContractMarketStatus.Ufa => "UFA",
        ContractMarketStatus.FreeAgent => "free agent",
        ContractMarketStatus.Expiring => "expiring contract",
        _ => status.ToString()
    };

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string description, string personId)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: scenario.Organization.OrganizationId),
            new Dictionary<string, object?> { ["person_name"] = FindPlayerContext(scenario, personId).Name, ["alpha_8_7"] = true });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, string personId) =>
        new(
            $"inbox:contract-market:{personId}:{eventType}:{Guid.NewGuid():N}",
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            title,
            summary,
            personId);

    private static ContractMarketResult Success(
        NewGmScenarioSnapshot scenario,
        ContractNegotiation negotiation,
        ContractOfferEvaluation? evaluation,
        IReadOnlyList<AlphaInboxItem> inbox,
        string message,
        IReadOnlyList<LeagueTransaction>? leagueTransactions = null)
    {
        var result = new ContractMarketResult(true, scenario, negotiation, evaluation, inbox, message)
        {
            LeagueTransactions = leagueTransactions ?? Array.Empty<LeagueTransaction>()
        };
        result.Validate();
        return result;
    }

    private static ContractMarketResult Failure(NewGmScenarioSnapshot scenario, string message, ContractNegotiation? negotiation = null)
    {
        var result = new ContractMarketResult(false, scenario, negotiation, negotiation?.LastEvaluation, Array.Empty<AlphaInboxItem>(), message);
        result.Validate();
        return result;
    }

    private sealed record PlayerContext(string Name, RosterPosition Position, int? Age);
}
