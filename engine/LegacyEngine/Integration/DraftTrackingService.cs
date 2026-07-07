namespace LegacyEngine.Integration;

public sealed class DraftTrackingService
{
    private readonly CareerHistoryService _careerHistoryService = new();

    public NewGmScenarioSnapshot RecordDraftCompleted(NewGmScenarioSnapshot scenario, IReadOnlyList<DraftPickSummary> selections) =>
        _careerHistoryService.RecordDraftCompleted(scenario, selections);

    public IReadOnlyList<WhereAreTheyNowRecord> BuildWhereAreTheyNow(NewGmScenarioSnapshot scenario) =>
        _careerHistoryService.BuildWhereAreTheyNow(scenario);
}
