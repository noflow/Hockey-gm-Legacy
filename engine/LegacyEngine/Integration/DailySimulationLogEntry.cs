namespace LegacyEngine.Integration;

public sealed record DailySimulationLogEntry(
    DailySimulationStep Step,
    bool Success,
    string Message,
    IReadOnlyDictionary<string, object?> Details);
