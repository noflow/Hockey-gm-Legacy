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
        var games = scouting.ScenarioSnapshot.SeasonReadiness.SeasonBegun
            ? new SeasonFrameworkService().SimulateScheduledGamesForCurrentDate(registry, scouting.ScenarioSnapshot)
            : new SeasonSimulationResult(scouting.ScenarioSnapshot, Array.Empty<ScheduledGame>(), Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), "Season has not begun.");
        var report = games.ScenarioSnapshot.Season.Status == LegacyEngine.Seasons.SeasonStatus.Completed
            && games.ScenarioSnapshot.ExecutiveReports.Find($"executive-report:{games.ScenarioSnapshot.Season.SeasonId}:{ExecutiveReportKind.EndOfSeasonExecutiveReview}") is null
            ? new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(registry, games.ScenarioSnapshot)
            : null;
        var finalScenario = report?.Success == true ? report.ScenarioSnapshot : games.ScenarioSnapshot;
        var inbox = simulation.InboxItems
            .Concat(camp.InboxItems)
            .Concat(scouting.InboxItems)
            .Concat(games.InboxItems)
            .Concat(report?.InboxItems ?? Array.Empty<AlphaInboxItem>())
            .ToArray();
        var summary = camp.InboxItems.Count == 0 && scouting.InboxItems.Count == 0 && games.SimulatedGames.Count == 0 && report?.Success != true
            ? simulation.Summary
            : $"{simulation.Summary} {camp.Summary} {scouting.Message} {games.Summary}{(report?.Success == true ? $" {report.Message}" : string.Empty)}";

        return new NewGmDailySimulationResult(finalScenario, simulation, inbox, summary);
    }

    public IReadOnlyList<AlphaSimulationResult> AdvanceDays(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        int days) =>
        _pipeline.RunDays(registry, snapshot, days);
}
