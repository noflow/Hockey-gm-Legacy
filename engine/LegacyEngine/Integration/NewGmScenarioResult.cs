namespace LegacyEngine.Integration;

public sealed record NewGmScenarioResult(
    EngineRegistry Registry,
    NewGmScenarioSnapshot ScenarioSnapshot,
    AlphaWorldSnapshot AlphaSnapshot,
    IReadOnlyList<AlphaInboxItem> FirstDayInbox,
    string Summary);
