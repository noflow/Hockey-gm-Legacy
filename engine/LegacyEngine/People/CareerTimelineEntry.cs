namespace LegacyEngine.People;

public sealed record CareerTimelineEntry(
    string EntryId,
    DateOnly Date,
    CareerTimelineEntryType EntryType,
    string Summary,
    IReadOnlyDictionary<string, object?> Details)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EntryId))
        {
            throw new ArgumentException("Timeline entry id is required.", nameof(EntryId));
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Timeline entry summary is required.", nameof(Summary));
        }
    }
}
