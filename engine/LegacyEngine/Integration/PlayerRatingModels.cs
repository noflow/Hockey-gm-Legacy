using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum PlayerRatingBand
{
    Unknown,
    WeakProspect,
    AverageJunior,
    StrongJunior,
    EliteDraftProspect,
    AhlDepth,
    GoodAhl,
    NhlReadyProspect,
    Depth,
    MiddleSix,
    TopSix,
    FirstLine,
    Star,
    Elite,
    Franchise,
    Generational
}

public enum PlayerRatingConfidence
{
    Unknown,
    Low,
    Medium,
    High,
    VeryHigh
}

public sealed record PlayerRating(int Low, int High)
{
    public int Midpoint => (Low + High) / 2;

    public bool IsExact => Low == High;

    public string Display => IsExact ? Low.ToString() : $"{Low}-{High}";

    public void Validate()
    {
        if (Low is < 0 or > 100 || High is < 0 or > 100 || Low > High)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerRating), "Visible player ratings must stay within 0-100.");
        }
    }
}

public sealed record PlayerPotential(int Low, int High)
{
    public int Midpoint => (Low + High) / 2;

    public bool IsExact => Low == High;

    public string Display => IsExact ? Low.ToString() : $"{Low}-{High}";

    public void Validate()
    {
        if (Low is < 0 or > 100 || High is < 0 or > 100 || Low > High)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerPotential), "Visible player potential must stay within 0-100.");
        }
    }
}

public sealed record PlayerRatingSnapshot(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    PlayerRating Overall,
    PlayerPotential Potential,
    PlayerRatingBand Band,
    PlayerRatingConfidence Confidence,
    DateOnly LastUpdated,
    string RatingSource,
    string RoleLabel,
    string DevelopmentNote)
{
    public string OverallDisplay => $"OVR {Overall.Display}";

    public string PotentialDisplay => $"POT {Potential.Display}";

    public string ShortDisplay => $"{OverallDisplay} | {PotentialDisplay} | {Confidence}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(RatingSource)
            || string.IsNullOrWhiteSpace(RoleLabel)
            || string.IsNullOrWhiteSpace(DevelopmentNote))
        {
            throw new ArgumentException("Player rating snapshot requires player identity, source, role, and development note.");
        }

        Overall.Validate();
        Potential.Validate();

        if (Potential.High < Overall.Low)
        {
            throw new ArgumentException("Visible potential should not sit below visible overall.");
        }
    }
}

public sealed record PlayerRatingHistory(IReadOnlyList<PlayerRatingSnapshot> Snapshots)
{
    public static PlayerRatingHistory Empty { get; } = new(Array.Empty<PlayerRatingSnapshot>());

    public PlayerRatingHistory Merge(IEnumerable<PlayerRatingSnapshot> snapshots)
    {
        var merged = Snapshots
            .Concat(snapshots)
            .GroupBy(snapshot => $"{snapshot.PersonId}:{snapshot.LastUpdated:yyyyMMdd}:{snapshot.RatingSource}", StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(snapshot => snapshot.PersonId, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.LastUpdated)
            .ToArray();
        var history = new PlayerRatingHistory(merged);
        history.Validate();
        return history;
    }

    public IReadOnlyList<PlayerRatingSnapshot> ForPerson(string personId) =>
        Snapshots
            .Where(snapshot => snapshot.PersonId == personId)
            .OrderBy(snapshot => snapshot.LastUpdated)
            .ToArray();

    public void Validate()
    {
        foreach (var snapshot in Snapshots)
        {
            snapshot.Validate();
        }
    }
}
