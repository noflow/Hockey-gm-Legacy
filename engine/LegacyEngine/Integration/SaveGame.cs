namespace LegacyEngine.Integration;

public sealed record SaveGame(
    SaveGameMetadata Metadata,
    NewGmScenarioSnapshot ScenarioSnapshot,
    IReadOnlyList<InboxMessage> InboxMessages,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    IReadOnlyDictionary<string, ActionCenterStatus> ActionCenterStatuses,
    BudgetSnapshot BudgetSnapshot)
{
    public void Validate()
    {
        Metadata.Validate();
        ScenarioSnapshot.Validate();
        BudgetSnapshot.Validate();

        foreach (var message in InboxMessages)
        {
            message.Validate();
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        foreach (var status in ActionCenterStatuses)
        {
            if (string.IsNullOrWhiteSpace(status.Key))
            {
                throw new ArgumentException("Action Center save status id is required.", nameof(ActionCenterStatuses));
            }
        }
    }
}
