namespace LegacyEngine.Draft;

public sealed record DraftBoard(
    string BoardId,
    string OrganizationId,
    IReadOnlyList<DraftBoardEntry> Entries)
{
    public static DraftBoard Create(string boardId, string organizationId)
    {
        var board = new DraftBoard(boardId, organizationId, Array.Empty<DraftBoardEntry>());
        board.Validate();
        return board;
    }

    public DraftBoard AddProspect(DraftBoardEntry entry)
    {
        Validate();
        entry.Validate();

        if (Entries.Any(item => item.ProspectPersonId == entry.ProspectPersonId))
        {
            throw new ArgumentException("Prospect is already on this draft board.", nameof(entry));
        }

        return this with { Entries = Entries.Append(entry).OrderBy(item => item.Rank).ToArray() };
    }

    public DraftBoard UpdateRank(string prospectPersonId, int rank)
    {
        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), "Draft board rank must be positive.");
        }

        if (!Entries.Any(item => item.ProspectPersonId == prospectPersonId))
        {
            throw new ArgumentException("Prospect is not on this draft board.", nameof(prospectPersonId));
        }

        var updatedEntries = Entries
            .Select(item => item.ProspectPersonId == prospectPersonId ? item with { Rank = rank } : item)
            .Select(item => item.ProspectPersonId != prospectPersonId && item.Rank >= rank ? item with { Rank = item.Rank + 1 } : item)
            .OrderBy(item => item.Rank)
            .ToArray();

        return this with
        {
            Entries = updatedEntries
        };
    }

    public DraftBoard RemoveProspect(string prospectPersonId)
    {
        if (!Entries.Any(item => item.ProspectPersonId == prospectPersonId))
        {
            throw new ArgumentException("Prospect is not on this draft board.", nameof(prospectPersonId));
        }

        return this with { Entries = Entries.Where(item => item.ProspectPersonId != prospectPersonId).ToArray() };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BoardId))
        {
            throw new ArgumentException("Draft board id is required.", nameof(BoardId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        foreach (var entry in Entries)
        {
            entry.Validate();
        }

        if (Entries.Select(item => item.ProspectPersonId).Distinct(StringComparer.Ordinal).Count() != Entries.Count)
        {
            throw new ArgumentException("Draft board prospects must be unique.", nameof(Entries));
        }
    }
}
