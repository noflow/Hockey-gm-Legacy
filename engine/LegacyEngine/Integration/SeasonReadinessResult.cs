namespace LegacyEngine.Integration;

public sealed record SeasonReadinessResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    SeasonReadinessReport Report,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Report.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Season readiness result message is required.", nameof(Message));
        }
    }
}
