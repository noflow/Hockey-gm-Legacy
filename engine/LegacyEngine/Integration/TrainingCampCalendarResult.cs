namespace LegacyEngine.Integration;

public sealed record TrainingCampCalendarResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary);
