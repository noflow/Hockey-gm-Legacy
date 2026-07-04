namespace LegacyEngine.Events;

public sealed record LegacyEventResult(
    string EventId,
    bool Success,
    LegacyEventStatus Status,
    DateTimeOffset ProcessedAt,
    string Message,
    IReadOnlyDictionary<string, object?> Details);
