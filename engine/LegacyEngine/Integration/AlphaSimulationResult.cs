namespace LegacyEngine.Integration;

public sealed record AlphaSimulationResult(
    DateOnly CurrentDate,
    int ProcessedEventCount,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary,
    AlphaWorldSnapshot WorldSnapshot);
