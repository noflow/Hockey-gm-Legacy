namespace LegacyEngine.Integration;

public sealed record LeagueTransaction(
    string TransactionId,
    DateTimeOffset Date,
    string? OrganizationId,
    string TeamName,
    string? PersonId,
    string PersonName,
    LeagueTransactionType TransactionType,
    LeagueNewsCategory Category,
    string Description)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TransactionId))
        {
            throw new ArgumentException("Transaction id is required.", nameof(TransactionId));
        }

        if (string.IsNullOrWhiteSpace(TeamName))
        {
            throw new ArgumentException("Transaction team name is required.", nameof(TeamName));
        }

        if (string.IsNullOrWhiteSpace(PersonName))
        {
            throw new ArgumentException("Transaction person name is required.", nameof(PersonName));
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Transaction description is required.", nameof(Description));
        }
    }
}
