namespace LegacyEngine.Integration;

public sealed record FreeAgencyV2Result(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    FreeAgencyMarketState MarketState,
    FreeAgent? FreeAgent,
    FreeAgencyOfferState? OfferState,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        MarketState.Validate();
        FreeAgent?.Validate();
        OfferState?.Validate();
        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Free agency v2 result message is required.", nameof(Message));
        }
    }
}
