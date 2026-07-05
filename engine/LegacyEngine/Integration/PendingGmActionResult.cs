namespace LegacyEngine.Integration;

public sealed record PendingGmActionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    PendingGmAction Action,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Action.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Pending GM action result message is required.", nameof(Message));
        }
    }
}
