namespace LegacyEngine.Integration;

public sealed record TradeDecisionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    TradeOffer? TradeOffer,
    TradeEvaluation? Evaluation,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        TradeOffer?.Validate();
        Evaluation?.Validate();
        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Trade result message is required.", nameof(Message));
        }
    }
}
