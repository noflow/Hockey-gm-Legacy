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
        var inbox = simulation.InboxItems.Concat(camp.InboxItems).ToArray();
        var summary = camp.InboxItems.Count == 0
            ? simulation.Summary
            : $"{simulation.Summary} {camp.Summary}";

        return new NewGmDailySimulationResult(camp.ScenarioSnapshot, simulation, inbox, summary);
    }

    public IReadOnlyList<AlphaSimulationResult> AdvanceDays(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        int days) =>
        _pipeline.RunDays(registry, snapshot, days);
}
