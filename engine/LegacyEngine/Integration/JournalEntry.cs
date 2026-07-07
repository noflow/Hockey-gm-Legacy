namespace LegacyEngine.Integration;

public sealed record JournalEntry(
    string JournalEntryId,
    DateTimeOffset Date,
    JournalCategory Category,
    string Title,
    string Summary,
    string? RelatedPersonId,
    string SearchText)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(JournalEntryId)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(SearchText))
        {
            throw new ArgumentException("Journal entries require id, title, summary, and search text.");
        }
    }
}
