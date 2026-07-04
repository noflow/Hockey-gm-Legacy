using LegacyEngine.Events;

namespace LegacyEngine.World;

public sealed record DailySimulationResult(
    WorldId WorldId,
    WorldDate PreviousDate,
    WorldDate CurrentDate,
    int CurrentSeasonYear,
    WorldPhase CurrentPhase,
    int ProcessedEventCount,
    IReadOnlyList<LegacyEventResult> ProcessedEvents);
