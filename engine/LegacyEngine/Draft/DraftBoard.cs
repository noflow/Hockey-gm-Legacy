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

        return (this with
        {
            Entries = updatedEntries
        }).NormalizeRanks();
    }

    public DraftBoard MoveProspect(string prospectPersonId, int direction)
    {
        if (direction == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(direction), "Draft board move direction must not be zero.");
        }

        var ordered = Entries.OrderBy(item => item.Rank).ToArray();
        var currentIndex = Array.FindIndex(ordered, item => item.ProspectPersonId == prospectPersonId);
        if (currentIndex < 0)
        {
            throw new ArgumentException("Prospect is not on this draft board.", nameof(prospectPersonId));
        }

        var targetIndex = Math.Clamp(currentIndex + (direction < 0 ? -1 : 1), 0, ordered.Length - 1);
        if (targetIndex == currentIndex)
        {
            return this;
        }

        (ordered[currentIndex], ordered[targetIndex]) = (ordered[targetIndex], ordered[currentIndex]);
        return this with
        {
            Entries = ordered
                .Select((item, index) => item with { Rank = index + 1 })
                .ToArray()
        };
    }

    public DraftBoard SetStarred(string prospectPersonId, bool isStarred) =>
        UpdateEntry(prospectPersonId, entry => entry with { IsStarred = isStarred });

    public DraftBoard UpdatePersonalNotes(string prospectPersonId, string notes) =>
        UpdateEntry(prospectPersonId, entry => entry with { PersonalNotes = notes.Trim() });

    public DraftBoard UpdateAnalyticsSummary(string prospectPersonId, string analyticsSummary) =>
        UpdateEntry(prospectPersonId, entry => entry with { AnalyticsSummary = analyticsSummary.Trim() });

    private DraftBoard UpdateEntry(string prospectPersonId, Func<DraftBoardEntry, DraftBoardEntry> update)
    {
        if (!Entries.Any(item => item.ProspectPersonId == prospectPersonId))
        {
            throw new ArgumentException("Prospect is not on this draft board.", nameof(prospectPersonId));
        }

        return this with
        {
            Entries = Entries
                .Select(item => item.ProspectPersonId == prospectPersonId ? update(item) : item)
                .OrderBy(item => item.Rank)
                .ToArray()
        };
    }

    public DraftBoard RemoveProspect(string prospectPersonId)
    {
        if (!Entries.Any(item => item.ProspectPersonId == prospectPersonId))
        {
            throw new ArgumentException("Prospect is not on this draft board.", nameof(prospectPersonId));
        }

        return (this with { Entries = Entries.Where(item => item.ProspectPersonId != prospectPersonId).ToArray() }).NormalizeRanks();
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

    private DraftBoard NormalizeRanks() =>
        this with
        {
            Entries = Entries
                .OrderBy(item => item.Rank)
                .ThenBy(item => item.ProspectPersonId, StringComparer.Ordinal)
                .Select((item, index) => item with { Rank = index + 1 })
                .ToArray()
        };
}
