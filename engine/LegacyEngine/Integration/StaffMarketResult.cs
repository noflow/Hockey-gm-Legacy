namespace LegacyEngine.Integration;

public sealed record StaffMarketResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    StaffMarket? StaffMarket,
    StaffMarketCandidate? Candidate,
    StaffMovementRecord? Movement,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        StaffMarket?.Validate();
        Candidate?.Validate();
        Movement?.Validate();
        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Staff market result message is required.", nameof(Message));
        }
    }
}
