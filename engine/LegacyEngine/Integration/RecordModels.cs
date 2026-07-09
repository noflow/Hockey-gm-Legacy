namespace LegacyEngine.Integration;

public enum RecordType
{
    Goals,
    Assists,
    Points,
    GoalieWins,
    Shutouts,
    GamesPlayed,
    TeamWins,
    Championships,
    PlayoffPoints
}

public enum RecordScope
{
    League,
    Team,
    Career,
    SingleSeason,
    Playoff
}

public sealed record RecordEntry(
    string RecordId,
    RecordType RecordType,
    RecordScope Scope,
    int SeasonYear,
    DateOnly DateSet,
    string HolderId,
    string HolderName,
    string HolderKind,
    string? OrganizationId,
    string? OrganizationName,
    int Value,
    string Summary,
    bool WasBrokenThisUpdate)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecordId)
            || string.IsNullOrWhiteSpace(HolderId)
            || string.IsNullOrWhiteSpace(HolderName)
            || string.IsNullOrWhiteSpace(HolderKind)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Record entry requires id, holder, kind, and summary.");
        }
    }
}

public sealed record RecordBook(IReadOnlyList<RecordEntry> Records)
{
    public static RecordBook Empty { get; } = new(Array.Empty<RecordEntry>());

    public RecordBook Merge(IEnumerable<RecordEntry> records)
    {
        var merged = Records
            .Concat(records)
            .GroupBy(record => record.RecordId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(record => record.Value).ThenByDescending(record => record.DateSet).First())
            .OrderBy(record => record.Scope)
            .ThenBy(record => record.RecordType)
            .ThenByDescending(record => record.Value)
            .ToArray();
        var book = new RecordBook(merged);
        book.Validate();
        return book;
    }

    public IReadOnlyList<RecordEntry> ForPerson(string personId) =>
        Records
            .Where(record => string.Equals(record.HolderId, personId, StringComparison.Ordinal))
            .OrderBy(record => record.Scope)
            .ThenBy(record => record.RecordType)
            .ToArray();

    public IReadOnlyList<RecordEntry> ForOrganization(string organizationId) =>
        Records
            .Where(record => string.Equals(record.OrganizationId, organizationId, StringComparison.Ordinal)
                || string.Equals(record.HolderId, organizationId, StringComparison.Ordinal))
            .OrderBy(record => record.Scope)
            .ThenBy(record => record.RecordType)
            .ToArray();

    public void Validate()
    {
        foreach (var record in Records)
        {
            record.Validate();
        }

        if (Records.Select(record => record.RecordId).Distinct(StringComparer.Ordinal).Count() != Records.Count)
        {
            throw new ArgumentException("Record ids must be unique.", nameof(Records));
        }
    }
}
