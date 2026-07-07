namespace LegacyEngine.Integration;

public sealed record TransactionHistoryRecord(
    string TransactionHistoryId,
    DateOnly Date,
    int SeasonYear,
    string TransactionType,
    string? PersonId,
    string PersonName,
    string OrganizationId,
    string OrganizationName,
    string Summary,
    string? RelatedEventId = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TransactionHistoryId)
            || string.IsNullOrWhiteSpace(TransactionType)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Transaction history requires identity, type, organization, and summary.");
        }
    }
}
