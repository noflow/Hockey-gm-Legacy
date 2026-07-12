namespace LegacyEngine.Integration;

/// <summary>Free Agency v3 facade: target board, timing, competition, and delayed responses.</summary>
public sealed class FreeAgencyV3Service
{
    private readonly FreeAgencyV2Service _v2 = new();
    private readonly FreeAgentMarketService _market = new();

    public NewGmScenarioSnapshot EnsureMarket(EngineRegistry registry, NewGmScenarioSnapshot scenario) =>
        _v2.EnsureMarketState(registry, scenario);

    public FreeAgencyV2Result SubmitOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, decimal? annualSalary = null, int? termYears = null) =>
        _v2.SubmitOffer(registry, scenario, personId, annualSalary, termYears);

    public FreeAgencyV2Result AdvanceMarket(EngineRegistry registry, NewGmScenarioSnapshot scenario) =>
        _v2.ProgressMarket(registry, scenario);

    public FreeAgencyTargetBoard BuildTargetBoard(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var prepared = _v2.EnsureMarketState(registry, scenario);
        var state = prepared.FreeAgencyMarketState!;
        var targets = (prepared.FreeAgentMarket?.FreeAgents ?? Array.Empty<FreeAgent>())
            .Where(agent => agent.Status is not FreeAgentStatus.Signed and not FreeAgentStatus.Unavailable)
            .Select(agent =>
            {
                var competition = state.ActiveCompetitions(agent.PersonId);
                var priority = agent.IsShortlisted
                    ? FreeAgencyTargetPriority.Priority
                    : agent.FitSummary.FitScore >= 75 ? FreeAgencyTargetPriority.Need : FreeAgencyTargetPriority.Watch;
                var timing = state.Window.Phase switch
                {
                    FreeAgencyPhase.OpeningDay => "Opening day: act quickly before the market moves.",
                    FreeAgencyPhase.LateMarket => "Late market: value may improve, but options are thinning.",
                    FreeAgencyPhase.Closed => "Window closed: monitor for the next rights period.",
                    _ => $"{state.Window.DaysUntilClose(prepared.CurrentDate)} day(s) remain in the window."
                };
                return new FreeAgencyTarget(
                    agent.PersonId,
                    agent.Name,
                    agent.Position,
                    priority,
                    agent.IsShortlisted,
                    agent.Interest.PlayerOrganizationInterest,
                    competition.Count,
                    timing,
                    $"{agent.FitSummary.RosterNeed} {agent.FitSummary.StaffRecommendation}",
                    competition.Count == 0 ? "Monitor and compare against the team's contract plan." : $"{competition.Count} competing organization(s) are also active.");
            })
            .OrderByDescending(target => target.Priority)
            .ThenByDescending(target => target.PlayerInterest)
            .ThenBy(target => target.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var board = new FreeAgencyTargetBoard(prepared.CurrentDate, state.Window, targets, $"{targets.Length} available target(s); {targets.Count(target => target.CompetingOfferCount > 0)} face active competition.");
        board.Validate();
        return board;
    }

    public FreeAgentMarketResult Shortlist(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId) =>
        _market.Shortlist(registry, scenario, personId);

    public FreeAgentMarketResult RemoveFromShortlist(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId) =>
        _market.RemoveFromShortlist(registry, scenario, personId);
}
