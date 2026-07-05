namespace LegacyEngine.Integration;

public sealed record ProspectDecisionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    DraftRightsRecord Prospect,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Prospect.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Prospect decision result message is required.", nameof(Message));
        }
    }
}
