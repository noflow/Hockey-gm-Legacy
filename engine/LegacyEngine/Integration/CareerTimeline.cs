namespace LegacyEngine.Integration;

public sealed record CareerTimeline(IReadOnlyList<CareerTimelineEntry> Entries)
{
    public static CareerTimeline Empty { get; } = new(Array.Empty<CareerTimelineEntry>());

    public CareerTimeline Add(CareerTimelineEntry entry)
    {
        entry.Validate();
        return new CareerTimeline(Entries
            .Where(item => item.EntryId != entry.EntryId)
            .Append(entry)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.EntryId, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<CareerTimelineEntry> ForPerson(string personId) =>
        Entries
            .Where(item => string.Equals(item.PersonId, personId, StringComparison.Ordinal))
            .OrderByDescending(item => item.Date)
            .ToArray();

    public void Validate()
    {
        foreach (var entry in Entries)
        {
            entry.Validate();
        }

        if (Entries.Select(item => item.EntryId).Distinct(StringComparer.Ordinal).Count() != Entries.Count)
        {
            throw new ArgumentException("Career timeline entry ids must be unique.", nameof(Entries));
        }
    }
}
