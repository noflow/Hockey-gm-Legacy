namespace LegacyEngine.Integration;

public sealed record NewGmDailySimulationResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    AlphaSimulationResult SimulationResult,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Summary);
