namespace LegacyEngine.Integration;

public sealed record SeasonCompletionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    SeasonArchive? Archive,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Archive?.Validate();
        foreach (var item in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(item.InboxItemId) || string.IsNullOrWhiteSpace(item.Title))
            {
                throw new ArgumentException("Season completion inbox items require ids and titles.", nameof(InboxItems));
            }
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Season completion result message is required.", nameof(Message));
        }
    }
}
