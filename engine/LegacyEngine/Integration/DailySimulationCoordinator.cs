using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed class DailySimulationCoordinator
{
    private readonly DailySimulationPipeline _pipeline = new();

    public AlphaSimulationResult AdvanceOneDay(EngineRegistry registry, AlphaWorldSnapshot snapshot) =>
        _pipeline.RunOneDay(registry, snapshot);

    public NewGmDailySimulationResult AdvanceScenarioOneDay(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);

        var simulation = _pipeline.RunOneDay(registry, scenario.AlphaSnapshot);
        var updatedScenario = scenario with
        {
            AlphaSnapshot = simulation.WorldSnapshot,
            Season = simulation.WorldSnapshot.Season ?? scenario.Season
        };

        var camp = new TrainingCampService().AdvanceCalendar(registry, updatedScenario);
        var scouting = new ScoutingOperationsService().AdvanceAssignments(registry, camp.ScenarioSnapshot);
        var deadline = new TradeDeadlineService().AdvanceDeadline(registry, scouting.ScenarioSnapshot);
        var games = deadline.ScenarioSnapshot.SeasonReadiness.SeasonBegun
            ? new SeasonFrameworkService().SimulateScheduledGamesForCurrentDate(registry, deadline.ScenarioSnapshot)
            : new SeasonSimulationResult(deadline.ScenarioSnapshot, Array.Empty<ScheduledGame>(), Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), "Season has not begun.");
        var playoffs = games.ScenarioSnapshot.SeasonReadiness.SeasonBegun
            ? new PlayoffService().AdvanceForCurrentDate(registry, games.ScenarioSnapshot)
            : new PlayoffSimulationResult(true, games.ScenarioSnapshot, games.ScenarioSnapshot.Playoffs.Bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Season has not begun.");
        var report = playoffs.ScenarioSnapshot.Playoffs.Bracket?.Status == PlayoffStatus.Completed
            && playoffs.ScenarioSnapshot.ExecutiveReports.Find($"executive-report:{playoffs.ScenarioSnapshot.Season.SeasonId}:{ExecutiveReportKind.EndOfSeasonExecutiveReview}") is null
            ? new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(registry, playoffs.ScenarioSnapshot)
            : null;
        var finalScenario = report?.Success == true ? report.ScenarioSnapshot : playoffs.ScenarioSnapshot;
        finalScenario = new DevelopmentPlanningService().EnsureScenarioPlans(finalScenario);
        finalScenario = new StoryService().EnsureStories(finalScenario, registry);
        if (finalScenario.Schedule is { Games.Count: > 0 }
            && finalScenario.Schedule.Games.All(game => game.Status == GameStatus.Completed))
        {
            finalScenario = new AwardService().EnsureAwards(finalScenario);
            finalScenario = new RecordService().EnsureRecordBook(finalScenario);
        }
        if (finalScenario.CurrentDate.Day == 1)
        {
            var recommendations = new DevelopmentPlanningService().BuildMonthlyRecommendations(finalScenario)
                .Where(recommendation => finalScenario.DevelopmentRecommendations.All(existing => existing.RecommendationId != recommendation.RecommendationId))
                .ToArray();
            if (recommendations.Length > 0)
            {
                finalScenario = finalScenario with
                {
                    DevelopmentRecommendations = finalScenario.DevelopmentRecommendations.Concat(recommendations).ToArray()
                };
            }
        }
        var inbox = simulation.InboxItems
            .Concat(camp.InboxItems)
            .Concat(scouting.InboxItems)
            .Concat(deadline.InboxItems)
            .Concat(games.InboxItems)
            .Concat(playoffs.InboxItems)
            .Concat(report?.InboxItems ?? Array.Empty<AlphaInboxItem>())
            .ToArray();
        var leagueTransactions = simulation.LeagueTransactions.Concat(deadline.LeagueTransactions).Concat(playoffs.LeagueNews).ToArray();
        finalScenario = new HockeyIntelligenceRatingService().EnsureRatings(finalScenario);
        finalScenario = new DevelopmentCurveService().EnsureCurves(finalScenario);
        finalScenario = new PlayerRatingService().EnsureRatings(finalScenario);
        finalScenario = new MediaService().EnsureMediaFeed(finalScenario, leagueTransactions, registry);
        var summary = camp.InboxItems.Count == 0 && scouting.InboxItems.Count == 0 && deadline.InboxItems.Count == 0 && games.SimulatedGames.Count == 0 && playoffs.GameRecaps.Count == 0 && report?.Success != true
            ? simulation.Summary
            : $"{simulation.Summary} {camp.Summary} {scouting.Message} {deadline.Summary} {games.Summary} {playoffs.Message}{(report?.Success == true ? $" {report.Message}" : string.Empty)}";

        return new NewGmDailySimulationResult(finalScenario, simulation, inbox, leagueTransactions, summary);
    }

    public IReadOnlyList<AlphaSimulationResult> AdvanceDays(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        int days) =>
        _pipeline.RunDays(registry, snapshot, days);
}
