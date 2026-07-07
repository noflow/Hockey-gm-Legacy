namespace LegacyEngine.Integration;

public sealed record CareerTimelineEntry(
    string EntryId,
    CareerTimelineEntryType EntryType,
    DateOnly Date,
    int SeasonYear,
    string? PersonId,
    string? OrganizationId,
    string? TeamName,
    string Title,
    string Description,
    string? RelatedEventId,
    HistoryImportance Importance)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EntryId)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Career timeline entry requires id, title, and description.");
        }

        if (SeasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Season year must be positive.");
        }
    }
}
