using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.RuleEngine;
using LegacyEngine.Rosters;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class FreeAgencyV2Service
{
    private readonly ContractManagementService _contracts = new();

    public FreeAgencyWindow BuildWindow(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var opens = MilestoneDate(scenario.Season.Calendar, SeasonMilestoneType.FreeAgencyOpens)
            ?? ContractExpiryCalendar.CommonExpiryDate(scenario.Season.Year, scenario.Season.Settings).AddDays(1);
        var closes = MilestoneDate(scenario.Season.Calendar, SeasonMilestoneType.FreeAgencyEnds)
            ?? scenario.Season.Calendar.SeasonStart.Value.AddDays(-1);
        if (closes < opens)
        {
            closes = opens.AddDays(60);
        }

        var window = new FreeAgencyWindow(opens, closes, PhaseFor(opens, closes, scenario.CurrentDate));
        window.Validate();
        return window;
    }

    public NewGmScenarioSnapshot EnsureMarketState(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var window = BuildWindow(scenario);
        var existing = scenario.FreeAgencyMarketState;
        var state = existing is not null && existing.Window.OpensOn == window.OpensOn && existing.Window.ClosesOn == window.ClosesOn
            ? existing with { Window = window }
            : FreeAgencyMarketState.Empty(window);

        var agents = scenario.FreeAgentMarket?.FreeAgents ?? Array.Empty<FreeAgent>();
        var profiles = agents
            .Select((agent, index) => state.FindMotivations(agent.PersonId) ?? BuildMotivationProfile(agent, index))
            .ToArray();
        var competitions = state.Competitions.Count == 0
            ? agents.SelectMany((agent, index) => BuildCompetitions(agent, index, window.Phase)).ToArray()
            : state.Competitions;
        var updates = state.Updates;
        if (existing?.Window.Phase != window.Phase)
        {
            updates = updates.Append(new FreeAgencyMarketUpdate(
                $"free-agency-update:{Guid.NewGuid():N}",
                scenario.CurrentDate,
                window.Phase,
                $"Free agency phase is now {window.Phase}.",
                window.Phase is FreeAgencyPhase.OpeningDay or FreeAgencyPhase.Closed)).ToArray();
            QueueEvent(registry, scenario, window.Phase == FreeAgencyPhase.Closed ? LegacyEventType.FreeAgencyClosed : LegacyEventType.FreeAgencyPhaseChanged, "Free agency phase changed", $"Free agency is now {window.Phase}.", null);
        }

        var next = new FreeAgencyMarketState(window, profiles, competitions, state.OfferStates, updates);
        next.Validate();
        return scenario with { FreeAgencyMarketState = next };
    }

    public FreeAgencyV2Result ProgressMarket(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var prepared = EnsureMarketState(registry, scenario);
        var state = prepared.FreeAgencyMarketState!;
        var adjusted = state.Window.Phase == FreeAgencyPhase.LateMarket
            ? ApplyLateMarketAdjustments(prepared)
            : prepared;
        prepared = adjusted;
        state = prepared.FreeAgencyMarketState!;

        var due = ResolveDueResponses(registry, prepared);
        if (due.ScenarioSnapshot != prepared || due.InboxItems.Count > 0 || due.LeagueTransactions.Count > 0)
        {
            return due;
        }

        return Result(true, prepared, state, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"Free agency phase: {state.Window.Phase}.");
    }

    public ContractOfferEvaluation BuildOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, decimal? annualSalary = null, int? termYears = null)
    {
        var prepared = EnsureMarketState(registry, scenario);
        var agent = RequireAgent(prepared, personId);
        var ask = _contracts.BuildAsk(prepared, ContractAskType.FreeAgent, personId);
        var request = new ContractOfferBuildRequest(
            personId,
            ContractAskType.FreeAgent,
            annualSalary ?? ask.RequestedSalary,
            termYears ?? ask.RequestedTermYears,
            ask.DesiredRole,
            agent.DevelopmentTrend,
            false,
            "No staff promise",
            "Free Agency v2 offer");
        return _contracts.BuildOffer(registry, prepared, request);
    }

    public FreeAgencyV2Result SubmitOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, decimal? annualSalary = null, int? termYears = null)
    {
        var prepared = EnsureMarketState(registry, scenario);
        var state = prepared.FreeAgencyMarketState!;
        if (state.Window.Phase is FreeAgencyPhase.NotOpen or FreeAgencyPhase.Closed)
        {
            return Result(false, prepared, state, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"Free agency is {state.Window.Phase}; normal signings are blocked.");
        }

        var agent = RequireAgent(prepared, personId);
        if (agent.Status is FreeAgentStatus.Signed or FreeAgentStatus.Unavailable)
        {
            return Result(false, prepared, state, agent, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"{agent.Name} is no longer available.");
        }

        var evaluation = BuildOffer(registry, prepared, personId, annualSalary, termYears);
        var cap = new SalaryCapService().ProjectAfterSigning(
            prepared,
            registry.Rulebook ?? prepared.LeagueProfile.Rulebook,
            evaluation.AnnualCost,
            evaluation.OfferRequest.TermYears);
        if (!cap.IsCompliant)
        {
            return Result(false, prepared, state, agent, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), string.Join(" ", cap.Reasons));
        }

        var delay = ResponseDelayDays(prepared, agent, evaluation);
        var pressure = MarketPressure(state, personId);
        var responseStatus = delay == 0 ? DecisionForEvaluation(evaluation) : FreeAgencyDecision.AwaitingResponse;
        var offerState = new FreeAgencyOfferState(
            $"free-agency-offer:{Guid.NewGuid():N}",
            personId,
            agent.Name,
            prepared.CurrentDate,
            prepared.CurrentDate.AddDays(delay),
            responseStatus,
            delay,
            pressure,
            evaluation,
            delay == 0 ? evaluation.Explanation.Summary : $"{evaluation.AgentName} will respond for {agent.Name} by {prepared.CurrentDate.AddDays(delay):yyyy-MM-dd}. {evaluation.AgentOpinion}");
        offerState.Validate();

        var updatedAgent = agent with { Status = delay == 0 ? agent.Status : FreeAgentStatus.Offered };
        var next = ReplaceAgent(prepared, updatedAgent);
        var nextState = state with { OfferStates = UpsertOffer(state.OfferStates, offerState) };
        next = next with { FreeAgencyMarketState = nextState };
        QueueEvent(registry, next, LegacyEventType.FreeAgentOfferSubmitted, $"Offer submitted to {evaluation.AgentName}", $"{evaluation.AgentName} is reviewing a {evaluation.OfferRequest.TermYears}-year offer from {next.Organization.Name} for {agent.Name}.", personId);

        if (delay > 0)
        {
            QueueEvent(registry, next, LegacyEventType.FreeAgentOfferResponseDue, "Agent response due", $"{evaluation.AgentName} is expected to respond for {agent.Name} on {offerState.ResponseDate:yyyy-MM-dd}.", personId, LegacyEventSeverity.Warning);
            var inbox = new[]
            {
                Inbox(next, LegacyEventType.FreeAgentOfferSubmitted, $"Agent reviewing free agent offer: {evaluation.AgentName}", $"{evaluation.AgentOpinion} Response due {offerState.ResponseDate:yyyy-MM-dd}. Likelihood: {evaluation.Likelihood}. {evaluation.AgentRisk}", personId)
            };
            return Result(true, next, nextState, updatedAgent, offerState, inbox, Array.Empty<LeagueTransaction>(), $"{evaluation.AgentName} is considering the offer for {agent.Name}.");
        }

        return ApplyImmediateDecision(registry, next, offerState);
    }

    public FreeAgencyV2Result ResolveDueResponses(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var prepared = EnsureMarketState(registry, scenario);
        var state = prepared.FreeAgencyMarketState!;
        var due = state.OfferStates.Where(offer => offer.IsPendingResponse && offer.ResponseDate <= prepared.CurrentDate).ToArray();
        if (due.Length == 0)
        {
            return Result(true, prepared, state, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No free agent responses are due.");
        }

        var next = prepared;
        var inbox = new List<AlphaInboxItem>();
        var league = new List<LeagueTransaction>();
        FreeAgencyOfferState? lastOffer = null;
        FreeAgent? lastAgent = null;

        foreach (var offer in due)
        {
            var resolved = ResolveSingleResponse(registry, next, offer);
            next = resolved.ScenarioSnapshot;
            inbox.AddRange(resolved.InboxItems);
            league.AddRange(resolved.LeagueTransactions);
            lastOffer = resolved.OfferState;
            lastAgent = resolved.FreeAgent;
        }

        return Result(true, next, next.FreeAgencyMarketState!, lastAgent, lastOffer, inbox, league, $"{due.Length} free agent response(s) resolved.");
    }

    public FreeAgencyStaffRecommendations BuildStaffRecommendations(NewGmScenarioSnapshot scenario, string personId)
    {
        var agent = RequireAgent(scenario, personId);
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());
        var recommendations = new FreeAgencyStaffRecommendations(
            personId,
            agent.Position == RosterPosition.Goalie ? "Head coach: useful camp competition if goalie workload is a concern." : $"Head coach: role fit is {agent.ProjectedLineupRole}.",
            $"Scout: {agent.ScoutingConfidence} confidence; {agent.FitSummary.StaffRecommendation}",
            $"Medical: {agent.InjuryRisk}",
            budget.RemainingBudget - agent.ContractAsk.AnnualAmount < 0 ? "Owner: budget concern if this ask is approved." : "Owner: affordable if the hockey case is strong.",
            $"Assistant GM: market value is around {agent.ContractAsk.AnnualAmount:C0}; risk is {agent.FitSummary.RiskSummary}");
        recommendations.Validate();
        return recommendations;
    }

    public IReadOnlyList<FreeAgentMotivationScore> TopMotivations(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.FreeAgencyMarketState?.FindMotivations(personId)?.TopMotivations
        ?? BuildMotivationProfile(RequireAgent(scenario, personId), 0).TopMotivations;

    public IReadOnlyList<FreeAgencyCompetition> Competitions(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.FreeAgencyMarketState?.ActiveCompetitions(personId) ?? Array.Empty<FreeAgencyCompetition>();

    private FreeAgencyV2Result ApplyImmediateDecision(EngineRegistry registry, NewGmScenarioSnapshot scenario, FreeAgencyOfferState offerState)
    {
        if (offerState.ResponseStatus == FreeAgencyDecision.Accepted)
        {
            var contract = _contracts.SubmitOffer(registry, scenario, offerState.Evaluation.OfferRequest);
            var acceptedState = contract.ScenarioSnapshot.FreeAgencyMarketState ?? scenario.FreeAgencyMarketState!;
            var updatedState = acceptedState with { OfferStates = UpsertOffer(acceptedState.OfferStates, offerState with { ResponseStatus = FreeAgencyDecision.Accepted, Explanation = offerState.Evaluation.Explanation.Summary }) };
            var acceptedSnapshot = contract.ScenarioSnapshot with { FreeAgencyMarketState = updatedState };
            var acceptedAgent = acceptedSnapshot.FreeAgentMarket?.Find(offerState.PersonId);
            return Result(true, acceptedSnapshot, updatedState, acceptedAgent, offerState, contract.InboxItems, contract.LeagueTransactions, contract.Message);
        }

        var agent = RequireAgent(scenario, offerState.PersonId);
        var status = offerState.ResponseStatus == FreeAgencyDecision.Rejected ? FreeAgentStatus.Rejected : FreeAgentStatus.Negotiating;
        var nextAgent = agent with { Status = status };
        var next = ReplaceAgent(scenario, nextAgent);
        var state = next.FreeAgencyMarketState!;
        state = state with { OfferStates = UpsertOffer(state.OfferStates, offerState) };
        next = next with { FreeAgencyMarketState = state };
        var eventType = offerState.ResponseStatus == FreeAgencyDecision.WantsMore ? LegacyEventType.FreeAgentOfferNeedsRevision : LegacyEventType.FreeAgentOfferRejected;
        var title = offerState.ResponseStatus == FreeAgencyDecision.WantsMore ? $"Agent wants more: {agent.Name}" : $"Agent rejected offer: {agent.Name}";
        var summary = offerState.ResponseStatus == FreeAgencyDecision.WantsMore
            ? $"{offerState.Evaluation.AgentOpinion} {offerState.Evaluation.AgentCounterSuggestion}"
            : offerState.Evaluation.AgentOpinion;
        QueueEvent(registry, next, eventType, title, summary, agent.PersonId, LegacyEventSeverity.Warning);
        return Result(true, next, state, nextAgent, offerState, new[] { Inbox(next, eventType, title, summary, agent.PersonId, LegacyEventSeverity.Warning) }, Array.Empty<LeagueTransaction>(), summary);
    }

    private FreeAgencyV2Result ResolveSingleResponse(EngineRegistry registry, NewGmScenarioSnapshot scenario, FreeAgencyOfferState offer)
    {
        var state = scenario.FreeAgencyMarketState!;
        var agent = RequireAgent(scenario, offer.PersonId);
        var competition = state.ActiveCompetitions(agent.PersonId).FirstOrDefault();
        var signsElsewhere = competition is not null && competition.PlayerInterest >= offer.Evaluation.DecisionScore + 5;
        if (signsElsewhere)
        {
            var resolved = offer with { ResponseStatus = FreeAgencyDecision.SignedElsewhere, Explanation = $"{agent.Name} chose {competition!.TeamName}: {competition.WhyPlayerMayChooseThem}" };
            var nextAgent = agent with { Status = FreeAgentStatus.Unavailable };
            var next = ReplaceAgent(scenario, nextAgent);
            var nextState = next.FreeAgencyMarketState! with
            {
                OfferStates = UpsertOffer(next.FreeAgencyMarketState!.OfferStates, resolved),
                Competitions = next.FreeAgencyMarketState.Competitions.Select(item => item.PersonId == agent.PersonId ? item with { IsActive = false } : item).ToArray()
            };
            next = next with { FreeAgencyMarketState = nextState };
            var transaction = new LeagueTransaction(
                $"league-free-agency:{Guid.NewGuid():N}",
                At(next.CurrentDate, 16),
                null,
                competition.TeamName,
                agent.PersonId,
                agent.Name,
                LeagueTransactionType.PlayerSigned,
                LeagueNewsCategory.Signings,
                $"{competition.TeamName} signed free agent {agent.Name}. {competition.WhyPlayerMayChooseThem}");
            QueueEvent(registry, next, LegacyEventType.FreeAgentSignedElsewhere, "Free agent signed elsewhere", transaction.Description, agent.PersonId, LegacyEventSeverity.Warning);
            return Result(true, next, nextState, nextAgent, resolved, new[] { Inbox(next, LegacyEventType.FreeAgentSignedElsewhere, $"Signed elsewhere: {agent.Name}", resolved.Explanation, agent.PersonId, LegacyEventSeverity.Warning) }, new[] { transaction }, resolved.Explanation);
        }

        var decision = DecisionForEvaluation(offer.Evaluation);
        var resolvedOffer = offer with { ResponseStatus = decision, Explanation = offer.Evaluation.Explanation.Summary };
        return ApplyImmediateDecision(registry, scenario, resolvedOffer);
    }

    private static FreeAgencyDecision DecisionForEvaluation(ContractOfferEvaluation evaluation) =>
        evaluation.Decision switch
        {
            ContractOfferDecision.Accepted => FreeAgencyDecision.Accepted,
            ContractOfferDecision.Rejected => FreeAgencyDecision.Rejected,
            ContractOfferDecision.WantsMore => FreeAgencyDecision.WantsMore,
            _ => FreeAgencyDecision.AwaitingResponse
        };

    private static int ResponseDelayDays(NewGmScenarioSnapshot scenario, FreeAgent agent, ContractOfferEvaluation evaluation)
    {
        var hasCompetition = scenario.FreeAgencyMarketState?.ActiveCompetitions(agent.PersonId).Count > 0;
        if (evaluation.Decision == ContractOfferDecision.Accepted && evaluation.DecisionScore >= 88 && !hasCompetition)
        {
            return 0;
        }

        var seed = Math.Abs(HashCode.Combine(agent.PersonId, scenario.CurrentDate.DayNumber, evaluation.DecisionScore));
        var agentDelay = evaluation.AgentReview?.ResponseDelayDays ?? 0;
        return Math.Max(agentDelay, 2 + seed % 4);
    }

    private static int MarketPressure(FreeAgencyMarketState state, string personId)
    {
        var competition = state.ActiveCompetitions(personId).FirstOrDefault()?.PlayerInterest ?? 0;
        var phasePressure = state.Window.Phase switch
        {
            FreeAgencyPhase.OpeningDay => 85,
            FreeAgencyPhase.ActiveMarket => 70,
            FreeAgencyPhase.SlowMarket => 45,
            FreeAgencyPhase.LateMarket => 25,
            _ => 10
        };
        return Math.Clamp((competition + phasePressure) / 2, 0, 100);
    }

    private NewGmScenarioSnapshot ApplyLateMarketAdjustments(NewGmScenarioSnapshot scenario)
    {
        if (scenario.FreeAgentMarket is null)
        {
            return scenario;
        }

        var agents = scenario.FreeAgentMarket.FreeAgents
            .Select(agent =>
            {
                if (agent.Status is FreeAgentStatus.Signed or FreeAgentStatus.Unavailable)
                {
                    return agent;
                }

                var lowered = Math.Round(agent.ContractAsk.AnnualAmount * 0.9m, 0);
                var ask = agent.ContractAsk with { AnnualAmount = lowered, Notes = $"{agent.ContractAsk.Notes}; late-market ask softened" };
                var interest = agent.Interest with
                {
                    PlayerOrganizationInterest = Math.Clamp(agent.Interest.PlayerOrganizationInterest + 8, 0, 100),
                    MotivationSummary = $"{agent.Interest.MotivationSummary} Late market has made role and stability more important."
                };
                return agent with { ContractAsk = ask, Interest = interest };
            })
            .ToArray();
        var market = scenario.FreeAgentMarket with { FreeAgents = agents };
        var state = scenario.FreeAgencyMarketState!;
        if (state.Updates.Any(update => update.Summary.Contains("late-market ask", StringComparison.OrdinalIgnoreCase)))
        {
            return scenario;
        }

        state = state with
        {
            Updates = state.Updates.Append(new FreeAgencyMarketUpdate($"free-agency-update:{Guid.NewGuid():N}", scenario.CurrentDate, state.Window.Phase, "Late-market ask reductions applied to available free agents.", false)).ToArray()
        };
        return scenario with { FreeAgentMarket = market, FreeAgencyMarketState = state };
    }

    private static FreeAgentMotivationProfile BuildMotivationProfile(FreeAgent agent, int index)
    {
        var motivations = new[]
        {
            new FreeAgentMotivationScore(FreeAgentMotivation.Money, Math.Clamp(55 + index % 35, 0, 100), "Wants fair salary for market value."),
            new FreeAgentMotivationScore(FreeAgentMotivation.Term, Math.Clamp(45 + agent.ContractAsk.TermYears * 12, 0, 100), "Term matters for stability."),
            new FreeAgentMotivationScore(FreeAgentMotivation.Role, agent.ProjectedLineupRole.Contains("depth", StringComparison.OrdinalIgnoreCase) ? 70 : 82, "Role clarity shapes interest."),
            new FreeAgentMotivationScore(FreeAgentMotivation.Winning, 35 + index % 45, "Winning culture is a factor."),
            new FreeAgentMotivationScore(FreeAgentMotivation.Development, agent.Age <= 18 ? 88 : 42, "Development path matters more for younger players."),
            new FreeAgentMotivationScore(FreeAgentMotivation.Location, 40 + index % 40, "Location and travel comfort matter."),
            new FreeAgentMotivationScore(FreeAgentMotivation.TeamReputation, agent.FitSummary.FitScore, "Team reputation and roster fit affect interest."),
            new FreeAgentMotivationScore(FreeAgentMotivation.RelationshipWithGm, agent.Interest.PlayerOrganizationInterest, "Trust in the GM affects response quality."),
            new FreeAgentMotivationScore(FreeAgentMotivation.RelationshipWithCoachStaff, agent.FitSummary.FitScore, "Staff confidence affects comfort."),
            new FreeAgentMotivationScore(FreeAgentMotivation.Stability, 50 + index % 35, "Stable role and communication reduce risk."),
            new FreeAgentMotivationScore(FreeAgentMotivation.PathwayToHigherLeague, agent.Age <= 19 ? 76 : 38, "Pathway matters for players still climbing."),
            new FreeAgentMotivationScore(FreeAgentMotivation.FamilyHome, 35 + index % 35, "Family/home preference is tracked as a placeholder.")
        };
        var profile = new FreeAgentMotivationProfile(agent.PersonId, motivations);
        profile.Validate();
        return profile;
    }

    private static IEnumerable<FreeAgencyCompetition> BuildCompetitions(FreeAgent agent, int index, FreeAgencyPhase phase)
    {
        if (index % 3 != 0 && index % 5 != 0)
        {
            yield break;
        }

        var teams = new[] { "Regina Plainsmen", "Calgary Wolves", "Swift Current Riders", "Red Deer Royals", "Brandon Kings" };
        var pressure = phase is FreeAgencyPhase.OpeningDay or FreeAgencyPhase.ActiveMarket ? 12 : phase == FreeAgencyPhase.LateMarket ? -10 : 0;
        var competition = new FreeAgencyCompetition(
            $"free-agency-competition:{agent.PersonId}:{index}",
            agent.PersonId,
            teams[index % teams.Length],
            Math.Round(agent.ContractAsk.AnnualAmount * (1.05m + (index % 4) * 0.05m), 0),
            agent.ContractAsk.TermYears,
            agent.ProjectedLineupRole,
            Math.Clamp(agent.Interest.PlayerOrganizationInterest + pressure + index % 16, 0, 100),
            index % 2 == 0 ? "They can offer immediate role certainty." : "They are closer to the player's preferred situation.",
            index % 5 == 0,
            true);
        competition.Validate();
        yield return competition;
    }

    private static FreeAgencyPhase PhaseFor(DateOnly opens, DateOnly closes, DateOnly date)
    {
        if (date < opens)
        {
            return FreeAgencyPhase.NotOpen;
        }

        if (date > closes)
        {
            return FreeAgencyPhase.Closed;
        }

        var days = date.DayNumber - opens.DayNumber;
        if (days == 0)
        {
            return FreeAgencyPhase.OpeningDay;
        }

        var windowLength = Math.Max(1, closes.DayNumber - opens.DayNumber + 1);
        var activeEnds = Math.Max(1, (int)Math.Ceiling(windowLength * 0.35));
        var slowEnds = Math.Max(activeEnds + 1, (int)Math.Ceiling(windowLength * 0.7));
        return days <= activeEnds
            ? FreeAgencyPhase.ActiveMarket
            : days <= slowEnds
                ? FreeAgencyPhase.SlowMarket
                : FreeAgencyPhase.LateMarket;
    }

    private static DateOnly? MilestoneDate(SeasonCalendar calendar, SeasonMilestoneType type) =>
        calendar.Milestones.FirstOrDefault(milestone => milestone.Type == type)?.Date.Value;

    private static FreeAgent RequireAgent(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.FreeAgentMarket?.Find(personId)
        ?? throw new ArgumentException("Free agent was not found.", nameof(personId));

    private static NewGmScenarioSnapshot ReplaceAgent(NewGmScenarioSnapshot scenario, FreeAgent freeAgent) =>
        scenario with { FreeAgentMarket = (scenario.FreeAgentMarket ?? throw new InvalidOperationException("Free agent market has not been generated.")).Replace(freeAgent) };

    private static IReadOnlyList<FreeAgencyOfferState> UpsertOffer(IReadOnlyList<FreeAgencyOfferState> offers, FreeAgencyOfferState offer) =>
        offers.Where(item => item.OfferStateId != offer.OfferStateId).Append(offer).ToArray();

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string description, string? personId, LegacyEventSeverity severity = LegacyEventSeverity.Notice)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            At(scenario.CurrentDate, 15),
            eventType,
            severity,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["person_name"] = personId is null ? null : scenario.FreeAgentMarket?.Find(personId)?.Name,
                ["team_name"] = scenario.Organization.Name,
                ["reason"] = description
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, string personId, LegacyEventSeverity severity = LegacyEventSeverity.Notice) =>
        new($"inbox:free-agency-v2:{Guid.NewGuid():N}", At(scenario.CurrentDate, 15), eventType, severity, title, summary, personId);

    private static FreeAgencyV2Result Result(bool success, NewGmScenarioSnapshot scenario, FreeAgencyMarketState state, FreeAgent? agent, FreeAgencyOfferState? offer, IReadOnlyList<AlphaInboxItem> inbox, IReadOnlyList<LeagueTransaction> league, string message)
    {
        var result = new FreeAgencyV2Result(success, scenario, state, agent, offer, inbox, league, message);
        result.Validate();
        return result;
    }

    private static DateTimeOffset At(DateOnly date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.Zero);
}
