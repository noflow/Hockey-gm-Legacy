using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed class DailySimulationCoordinator
{
    private readonly DailySimulationPipeline _pipeline = new();

    public AlphaSimulationResult AdvanceOneDay(EngineRegistry registry, AlphaWorldSnapshot snapshot) =>
        _pipeline.RunOneDay(registry, snapshot);

    public IReadOnlyList<AlphaSimulationResult> AdvanceDays(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        int days) =>
        _pipeline.RunDays(registry, snapshot, days);
}
