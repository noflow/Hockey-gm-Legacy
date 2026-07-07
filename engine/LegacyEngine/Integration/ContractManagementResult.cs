namespace LegacyEngine.Integration;

public sealed record ContractManagementResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    ContractOfferEvaluation Evaluation,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Contract management result message is required.", nameof(Message));
        }

        ScenarioSnapshot.Validate();
        Evaluation.Validate();

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }
    }
}
