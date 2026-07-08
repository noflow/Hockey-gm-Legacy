using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum DraftClassTheme
{
    EliteTopEnd,
    DeepDefenseClass,
    DeepForwardClass,
    StrongGoalieClass,
    WeakGoalieClass,
    BalancedClass,
    LateRoundDepth,
    BoomBustClass,
    HighCharacterClass,
    InternationalHeavyClass,
    LocalTalentClass,
    WeakOverallClass
}

public enum DraftClassQuality
{
    Weak,
    BelowAverage,
    Average,
    AboveAverage,
    Strong,
    Exceptional
}

public sealed record DraftClassStrength(string Category, string Description)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Draft class strength requires category and description.");
        }
    }
}

public sealed record DraftClassWeakness(string Category, string Description)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Draft class weakness requires category and description.");
        }
    }
}

public sealed record DraftClassStoryline(string Headline, string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Headline) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft class storyline requires headline and summary.");
        }
    }
}

public sealed record DraftClassProfile(
    int Year,
    string LeagueId,
    int TotalProspects,
    DraftClassTheme Theme,
    DraftClassQuality Quality,
    IReadOnlyList<DraftClassStrength> Strengths,
    IReadOnlyList<DraftClassWeakness> Weaknesses,
    DraftClassStoryline TopStoryline,
    string ScoutingUncertainty,
    IReadOnlyDictionary<RosterPosition, int> PositionalDepth,
    IReadOnlyDictionary<string, int> RegionalDistribution,
    DraftClassQuality ExpectedFirstRoundQuality,
    DraftClassQuality LateRoundValue,
    DraftClassQuality GoalieDepth,
    DraftClassQuality DefenseDepth,
    DraftClassQuality ForwardDepth,
    string ScoutQuote)
{
    public string PreviewText =>
        $"{Year} looks like {ReadableTheme} with {Strengths.First().Description.ToLowerInvariant()}, but scouts are watching {Weaknesses.First().Description.ToLowerInvariant()}.";

    public string ReadableTheme => Theme switch
    {
        DraftClassTheme.EliteTopEnd => "an elite top-end class",
        DraftClassTheme.DeepDefenseClass => "a deep defense class",
        DraftClassTheme.DeepForwardClass => "a deep forward class",
        DraftClassTheme.StrongGoalieClass => "a strong goalie class",
        DraftClassTheme.WeakGoalieClass => "a weak goalie class",
        DraftClassTheme.BalancedClass => "a balanced class",
        DraftClassTheme.LateRoundDepth => "a late-round depth class",
        DraftClassTheme.BoomBustClass => "a boom/bust class",
        DraftClassTheme.HighCharacterClass => "a high-character class",
        DraftClassTheme.InternationalHeavyClass => "an international-heavy class",
        DraftClassTheme.LocalTalentClass => "a local talent class",
        DraftClassTheme.WeakOverallClass => "a weak overall class",
        _ => "a draft class"
    };

    public void Validate()
    {
        if (Year < 1 || string.IsNullOrWhiteSpace(LeagueId) || string.IsNullOrWhiteSpace(ScoutingUncertainty) || string.IsNullOrWhiteSpace(ScoutQuote))
        {
            throw new ArgumentException("Draft class profile requires year, league, uncertainty, and scout quote.");
        }

        if (TotalProspects < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalProspects), "Draft class prospect count cannot be negative.");
        }

        if (Strengths.Count == 0 || Weaknesses.Count == 0 || PositionalDepth.Count == 0 || RegionalDistribution.Count == 0)
        {
            throw new ArgumentException("Draft class profile requires strengths, weaknesses, depth, and regional distribution.");
        }

        foreach (var strength in Strengths)
        {
            strength.Validate();
        }

        foreach (var weakness in Weaknesses)
        {
            weakness.Validate();
        }

        TopStoryline.Validate();
    }
}

public sealed record DraftClassSummary(DraftClassProfile Profile, IReadOnlyList<string> PlayersToWatch)
{
    public void Validate()
    {
        Profile.Validate();
        foreach (var player in PlayersToWatch)
        {
            if (string.IsNullOrWhiteSpace(player))
            {
                throw new ArgumentException("Players to watch cannot contain blanks.", nameof(PlayersToWatch));
            }
        }
    }
}
