namespace LegacyEngine.Integration;

public sealed record PendingGmActionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    PendingGmAction Action,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message,
    IReadOnlyList<LeagueTransaction>? LeagueTransactions = null)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Action.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Pending GM action result message is required.", nameof(Message));
        }

        foreach (var transaction in LeagueTransactions ?? Array.Empty<LeagueTransaction>())
        {
            transaction.Validate();
        }
    }
}
