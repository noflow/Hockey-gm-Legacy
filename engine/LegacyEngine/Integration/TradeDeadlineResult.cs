namespace LegacyEngine.Integration;

public sealed record TradeDeadlineResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    TradeDeadlineWindow Window,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Window.Validate();
        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Trade deadline result summary is required.", nameof(Summary));
        }
    }
}
