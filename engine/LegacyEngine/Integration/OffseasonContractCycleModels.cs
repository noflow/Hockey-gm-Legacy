namespace LegacyEngine.Integration;

public sealed record OffseasonContractCycleState(
    DateOnly? LastProcessedDate = null,
    IReadOnlyList<string>? ExpiryNotices = null,
    IReadOnlyList<string>? MarketPhaseNotices = null)
{
    public IReadOnlyList<string> ProcessedExpiryNotices => ExpiryNotices ?? Array.Empty<string>();

    public IReadOnlyList<string> ProcessedMarketPhaseNotices => MarketPhaseNotices ?? Array.Empty<string>();

    public static OffseasonContractCycleState Empty { get; } = new();

    public void Validate()
    {
        foreach (var notice in ProcessedExpiryNotices.Concat(ProcessedMarketPhaseNotices))
        {
            if (string.IsNullOrWhiteSpace(notice))
            {
                throw new ArgumentException("Offseason contract cycle notice keys cannot be blank.");
            }
        }
    }
}

public sealed record OffseasonContractCycleResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    ContractMarketSummary MarketSummary,
    IReadOnlyList<string> SimulationLog,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    int ExpiredContractCount,
    int NewPendingDecisionCount,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        MarketSummary.Validate();
        foreach (var item in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(item.InboxItemId) || string.IsNullOrWhiteSpace(item.Title))
            {
                throw new ArgumentException("Offseason contract-cycle inbox items require ids and titles.");
            }
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (ExpiredContractCount < 0 || NewPendingDecisionCount < 0 || string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Offseason contract-cycle result requires valid counts and message.");
        }
    }
}
