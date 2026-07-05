namespace LegacyEngine.Integration;

public sealed record ExecutiveReportGenerationResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    ExecutiveReportRecord? Report,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Report?.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Executive report generation result message is required.", nameof(Message));
        }
    }
}
