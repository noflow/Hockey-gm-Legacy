namespace LegacyEngine.Integration;

public sealed record FreeAgentMarketResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    FreeAgent? FreeAgent,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Free agent market result message is required.", nameof(Message));
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }
    }
}
