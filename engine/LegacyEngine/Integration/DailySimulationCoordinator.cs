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
        var report = scouting.ScenarioSnapshot.Season.Status == LegacyEngine.Seasons.SeasonStatus.Completed
            && scouting.ScenarioSnapshot.ExecutiveReports.Find($"executive-report:{scouting.ScenarioSnapshot.Season.SeasonId}:{ExecutiveReportKind.EndOfSeasonExecutiveReview}") is null
            ? new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(registry, scouting.ScenarioSnapshot)
            : null;
        var finalScenario = report?.Success == true ? report.ScenarioSnapshot : scouting.ScenarioSnapshot;
        var inbox = simulation.InboxItems
            .Concat(camp.InboxItems)
            .Concat(scouting.InboxItems)
            .Concat(report?.InboxItems ?? Array.Empty<AlphaInboxItem>())
            .ToArray();
        var summary = camp.InboxItems.Count == 0 && scouting.InboxItems.Count == 0 && report?.Success != true
            ? simulation.Summary
            : $"{simulation.Summary} {camp.Summary} {scouting.Message}{(report?.Success == true ? $" {report.Message}" : string.Empty)}";

        return new NewGmDailySimulationResult(finalScenario, simulation, inbox, summary);
    }

    public IReadOnlyList<AlphaSimulationResult> AdvanceDays(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        int days) =>
        _pipeline.RunDays(registry, snapshot, days);
}
