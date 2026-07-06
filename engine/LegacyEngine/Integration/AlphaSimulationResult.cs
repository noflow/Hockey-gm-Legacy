namespace LegacyEngine.Integration;

public sealed record AlphaSimulationResult(
    DateOnly CurrentDate,
    int ProcessedEventCount,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    IReadOnlyList<AlphaCommunicationMessage> CommunicationMessages,
    IReadOnlyList<DailySimulationLogEntry> LogEntries,
    string Summary,
    AlphaWorldSnapshot WorldSnapshot);
