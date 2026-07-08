using LegacyEngine.Draft;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class DraftClassGenerator
{
    private static readonly DraftClassTheme[] Themes =
    {
        DraftClassTheme.EliteTopEnd,
        DraftClassTheme.DeepDefenseClass,
        DraftClassTheme.DeepForwardClass,
        DraftClassTheme.StrongGoalieClass,
        DraftClassTheme.WeakGoalieClass,
        DraftClassTheme.BalancedClass,
        DraftClassTheme.LateRoundDepth,
        DraftClassTheme.BoomBustClass,
        DraftClassTheme.HighCharacterClass,
        DraftClassTheme.InternationalHeavyClass,
        DraftClassTheme.LocalTalentClass,
        DraftClassTheme.WeakOverallClass
    };

    public DraftClassProfile GenerateProfile(
        Rulebook rulebook,
        int year,
        string leagueId,
        int totalProspects,
        DraftClassTheme? forcedTheme = null)
    {
        ArgumentNullException.ThrowIfNull(rulebook);
        var draftEnabled = rulebook.DraftRules?.DraftEnabled == true;
        var count = draftEnabled ? Math.Max(0, totalProspects) : 0;
        var theme = forcedTheme ?? Themes[Math.Abs(StableHash($"{rulebook.RulebookId}:{leagueId}:{year}")) % Themes.Length];
        var quality = QualityFor(theme, year);
        var positions = PositionalDepth(theme, count);
        var regions = RegionalDistribution(rulebook, theme, count);
        var profile = new DraftClassProfile(
            year,
            leagueId,
            count,
            theme,
            draftEnabled ? quality : DraftClassQuality.Weak,
            StrengthsFor(theme),
            WeaknessesFor(theme),
            StorylineFor(theme, year),
            UncertaintyFor(rulebook, theme),
            positions,
            regions,
            FirstRoundQuality(theme, quality),
            LateRoundQuality(theme, quality),
            DepthQuality(theme, RosterPosition.Goalie, quality),
            DepthQuality(theme, RosterPosition.Defense, quality),
            ForwardDepthQuality(theme, quality),
            ScoutQuoteFor(theme, year));
        profile.Validate();
        return profile;
    }

    public RosterPosition PositionFor(DraftClassProfile profile, int index)
    {
        var goalieSlots = profile.PositionalDepth.GetValueOrDefault(RosterPosition.Goalie);
        var defenseSlots = profile.PositionalDepth.GetValueOrDefault(RosterPosition.Defense);
        if (index < goalieSlots)
        {
            return RosterPosition.Goalie;
        }

        if (index < goalieSlots + defenseSlots)
        {
            return RosterPosition.Defense;
        }

        var forwardIndex = index - goalieSlots - defenseSlots;
        return forwardIndex % 3 == 0 ? RosterPosition.Center : forwardIndex % 3 == 1 ? RosterPosition.LeftWing : RosterPosition.RightWing;
    }

    public ScoutingConfidenceLevel StartingConfidence(DraftClassProfile profile, int rank, bool inheritedScouting)
    {
        if (!inheritedScouting)
        {
            return profile.Theme == DraftClassTheme.BoomBustClass ? ScoutingConfidenceLevel.Unknown : ScoutingConfidenceLevel.Low;
        }

        var confidence = rank <= 10 ? ScoutingConfidenceLevel.High : rank <= 45 ? ScoutingConfidenceLevel.Medium : ScoutingConfidenceLevel.Low;
        return profile.Theme == DraftClassTheme.BoomBustClass
            ? confidence switch
            {
                ScoutingConfidenceLevel.High => ScoutingConfidenceLevel.Medium,
                ScoutingConfidenceLevel.Medium => ScoutingConfidenceLevel.Low,
                _ => confidence
            }
            : confidence;
    }

    public string ProjectionFor(DraftClassProfile profile, RosterPosition position, int index)
    {
        var ceiling = profile.Theme switch
        {
            DraftClassTheme.EliteTopEnd when index < 5 => "high-end",
            DraftClassTheme.WeakOverallClass => "modest",
            DraftClassTheme.BoomBustClass => "high-variance",
            DraftClassTheme.LateRoundDepth when index > 25 => "sleeper",
            _ => "credible"
        };
        var positionText = PositionText(position);
        return position switch
        {
            RosterPosition.Goalie => $"{positionText} prospect with {ceiling} projection; class goalie context is {profile.GoalieDepth}.",
            RosterPosition.Defense => $"{positionText} prospect with {ceiling} projection; class defense depth is {profile.DefenseDepth}.",
            RosterPosition.Center => $"{positionText} prospect with {ceiling} two-way projection in a class with {profile.ForwardDepth} forward depth.",
            RosterPosition.LeftWing or RosterPosition.RightWing => $"{positionText} prospect with {ceiling} scoring/pace projection in a class with {profile.ForwardDepth} forward depth.",
            _ => $"Prospect with {ceiling} projection."
        };
    }

    public string RiskFor(DraftClassProfile profile, RosterPosition position, int index)
    {
        if (profile.Theme == DraftClassTheme.BoomBustClass)
        {
            return "High variance: projection band is wider than usual for this class.";
        }

        if (profile.Theme == DraftClassTheme.WeakOverallClass)
        {
            return "Class risk: fewer high-end players means staff should be careful with early-round certainty.";
        }

        if (profile.Theme == DraftClassTheme.WeakGoalieClass && position == RosterPosition.Goalie)
        {
            return "Goalie risk: this is a shallow goalie year, so staff need extra evidence.";
        }

        return index < 10 ? "Risk is mostly role-fit and development timeline." : "Risk is tied to projection confidence and class depth.";
    }

    public string ClassContextFor(DraftClassProfile profile, RosterPosition position, int rank)
    {
        if (profile.Theme == DraftClassTheme.DeepDefenseClass && position == RosterPosition.Defense)
        {
            return "One of the better defensemen in a strong defense class.";
        }

        if (profile.Theme == DraftClassTheme.StrongGoalieClass && position == RosterPosition.Goalie)
        {
            return "Goalie value is backed by a strong goalie class.";
        }

        if (profile.Theme == DraftClassTheme.BoomBustClass)
        {
            return "Riskier than most classes; scouts should confirm projection with more viewings.";
        }

        if (profile.Theme == DraftClassTheme.LateRoundDepth && rank > 25)
        {
            return "Could be a late-round value if the staff read is right.";
        }

        if (profile.Theme == DraftClassTheme.InternationalHeavyClass)
        {
            return "Class context leans international, so translation to this league matters.";
        }

        return $"{profile.ReadableTheme}; profile fit should be judged against {PositionText(position).ToLowerInvariant()} depth.";
    }

    public string AnalyticsFor(DraftClassProfile profile, RosterPosition position, int index)
    {
        var context = profile.Theme == DraftClassTheme.BoomBustClass ? "wide spread in viewings" : "stable early sample";
        return $"Analytics: {PositionText(position)} indicators show {context}; class fit note: {ClassContextFor(profile, position, index + 1)}";
    }

    public DraftProspectBio BuildBio(DraftClassProfile profile, RosterPosition position, int index, int birthYear, string birthplace)
    {
        var hometown = ResolveRegion(profile, birthplace, index);
        var height = position switch
        {
            RosterPosition.Goalie => 72 + (Math.Abs(StableHash($"{profile.Year}:g:{index}")) % 8),
            RosterPosition.Defense => 70 + (Math.Abs(StableHash($"{profile.Year}:d:{index}")) % 9),
            _ => 67 + (Math.Abs(StableHash($"{profile.Year}:f:{index}")) % 9)
        };
        var weight = position switch
        {
            RosterPosition.Goalie => 176 + (index * 9 % 62),
            RosterPosition.Defense => 174 + (index * 7 % 62),
            _ => 158 + (index * 6 % 58)
        };
        var bio = new DraftProspectBio(
            position,
            position == RosterPosition.Goalie ? (index % 2 == 0 ? "Catches L" : "Catches R") : (index % 3 == 0 ? "Shoots R" : "Shoots L"),
            height,
            weight,
            birthYear,
            hometown.Hometown,
            hometown.Region,
            hometown.Country,
            $"{hometown.Hometown} {TeamNickname(index)}",
            LeagueFor(profile, hometown.Country, index),
            CharacterFor(profile, index),
            RoleFor(profile, position, index));
        bio.Validate();
        return bio;
    }

    public DraftClassSummary BuildSummary(DraftClassProfile profile, DraftBoard board)
    {
        var playersToWatch = board.Entries
            .OrderBy(entry => entry.Rank)
            .Take(5)
            .Select(entry => $"#{entry.Rank} {PositionText(entry.Bio?.Position ?? RosterPosition.Unknown)} - {entry.ClassContextNote}")
            .ToArray();
        var summary = new DraftClassSummary(profile, playersToWatch);
        summary.Validate();
        return summary;
    }

    private static IReadOnlyList<DraftClassStrength> StrengthsFor(DraftClassTheme theme) =>
        theme switch
        {
            DraftClassTheme.DeepDefenseClass => [new("Defense", "several defensemen who project into meaningful lineup roles")],
            DraftClassTheme.DeepForwardClass => [new("Forwards", "useful forward depth through the middle rounds")],
            DraftClassTheme.StrongGoalieClass => [new("Goalies", "multiple goalies with believable starter upside")],
            DraftClassTheme.EliteTopEnd => [new("Top End", "a top five that scouts believe can change an organization")],
            DraftClassTheme.LateRoundDepth => [new("Depth", "late-round value beyond the obvious names")],
            DraftClassTheme.InternationalHeavyClass => [new("International", "a strong international and European footprint")],
            DraftClassTheme.LocalTalentClass => [new("Local", "regional players with strong local scouting familiarity")],
            DraftClassTheme.HighCharacterClass => [new("Character", "better-than-usual habits, leadership, and coachability")],
            _ => [new("Balance", "a playable spread of forwards, defensemen, and goalies")]
        };

    private static IReadOnlyList<DraftClassWeakness> WeaknessesFor(DraftClassTheme theme) =>
        theme switch
        {
            DraftClassTheme.WeakGoalieClass => [new("Goalies", "goalie depth is thin after the first few names")],
            DraftClassTheme.WeakOverallClass => [new("Ceiling", "fewer obvious high-end prospects than a normal year")],
            DraftClassTheme.BoomBustClass => [new("Uncertainty", "projection bands are wider and staff disagreement is expected")],
            DraftClassTheme.DeepDefenseClass => [new("Forwards", "forward scoring depth may flatten quickly")],
            DraftClassTheme.DeepForwardClass => [new("Defense", "defense depth is less certain after the first tier")],
            _ => [new("Separation", "middle-round separation still needs more viewings")]
        };

    private static DraftClassStoryline StorylineFor(DraftClassTheme theme, int year) =>
        new($"{year} {ReadableTheme(theme)}", ScoutQuoteFor(theme, year));

    private static DraftClassQuality QualityFor(DraftClassTheme theme, int year) =>
        theme switch
        {
            DraftClassTheme.EliteTopEnd => DraftClassQuality.Exceptional,
            DraftClassTheme.WeakOverallClass => DraftClassQuality.Weak,
            DraftClassTheme.DeepDefenseClass or DraftClassTheme.DeepForwardClass or DraftClassTheme.StrongGoalieClass => DraftClassQuality.Strong,
            _ => (year % 4) switch
            {
                0 => DraftClassQuality.AboveAverage,
                1 => DraftClassQuality.Average,
                2 => DraftClassQuality.BelowAverage,
                _ => DraftClassQuality.Strong
            }
        };

    private static DraftClassQuality FirstRoundQuality(DraftClassTheme theme, DraftClassQuality quality) =>
        theme == DraftClassTheme.EliteTopEnd ? DraftClassQuality.Exceptional : theme == DraftClassTheme.WeakOverallClass ? DraftClassQuality.BelowAverage : quality;

    private static DraftClassQuality LateRoundQuality(DraftClassTheme theme, DraftClassQuality quality) =>
        theme == DraftClassTheme.LateRoundDepth ? DraftClassQuality.Strong : theme == DraftClassTheme.WeakOverallClass ? DraftClassQuality.Weak : quality;

    private static DraftClassQuality DepthQuality(DraftClassTheme theme, RosterPosition position, DraftClassQuality quality)
    {
        if (position == RosterPosition.Goalie)
        {
            return theme == DraftClassTheme.StrongGoalieClass ? DraftClassQuality.Strong : theme == DraftClassTheme.WeakGoalieClass ? DraftClassQuality.Weak : DraftClassQuality.Average;
        }

        return theme == DraftClassTheme.DeepDefenseClass ? DraftClassQuality.Strong : theme == DraftClassTheme.WeakOverallClass ? DraftClassQuality.BelowAverage : quality;
    }

    private static DraftClassQuality ForwardDepthQuality(DraftClassTheme theme, DraftClassQuality quality) =>
        theme == DraftClassTheme.DeepForwardClass ? DraftClassQuality.Strong : theme == DraftClassTheme.WeakOverallClass ? DraftClassQuality.BelowAverage : quality;

    private static IReadOnlyDictionary<RosterPosition, int> PositionalDepth(DraftClassTheme theme, int total)
    {
        if (total <= 0)
        {
            return new Dictionary<RosterPosition, int>
            {
                [RosterPosition.Goalie] = 0,
                [RosterPosition.Defense] = 0,
                [RosterPosition.Center] = 0,
                [RosterPosition.LeftWing] = 0,
                [RosterPosition.RightWing] = 0
            };
        }

        var goalies = theme == DraftClassTheme.StrongGoalieClass ? Math.Max(8, total / 6) : theme == DraftClassTheme.WeakGoalieClass ? Math.Max(2, total / 18) : Math.Max(4, total / 10);
        var defense = theme == DraftClassTheme.DeepDefenseClass ? Math.Max(20, total / 2) : theme == DraftClassTheme.DeepForwardClass ? Math.Max(10, total / 4) : Math.Max(14, total / 3);
        var forwards = Math.Max(0, total - goalies - defense);
        return new Dictionary<RosterPosition, int>
        {
            [RosterPosition.Goalie] = goalies,
            [RosterPosition.Defense] = defense,
            [RosterPosition.Center] = forwards / 3,
            [RosterPosition.LeftWing] = forwards / 3,
            [RosterPosition.RightWing] = forwards - forwards / 3 - forwards / 3
        };
    }

    private static IReadOnlyDictionary<string, int> RegionalDistribution(Rulebook rulebook, DraftClassTheme theme, int total)
    {
        if (theme == DraftClassTheme.InternationalHeavyClass)
        {
            return new Dictionary<string, int> { ["Canada"] = total / 4, ["USA"] = total / 6, ["Europe"] = total / 2, ["Other"] = total - total / 4 - total / 6 - total / 2 };
        }

        if (rulebook.LeagueType == "nhl_style")
        {
            return new Dictionary<string, int> { ["Canada"] = total / 3, ["USA"] = total / 4, ["Europe"] = total / 3, ["Other"] = total - total / 3 - total / 4 - total / 3 };
        }

        if (theme == DraftClassTheme.LocalTalentClass || rulebook.LeagueType == "junior")
        {
            return new Dictionary<string, int> { ["Local"] = total / 2, ["Western Canada"] = total / 3, ["USA"] = total / 10, ["Europe"] = total - total / 2 - total / 3 - total / 10 };
        }

        return new Dictionary<string, int> { ["Canada"] = total / 2, ["USA"] = total / 5, ["Europe"] = total / 5, ["Other"] = total - total / 2 - total / 5 - total / 5 };
    }

    private static string UncertaintyFor(Rulebook rulebook, DraftClassTheme theme)
    {
        if (theme == DraftClassTheme.BoomBustClass)
        {
            return "High - scouts expect wide projection ranges and disagreement.";
        }

        return rulebook.LeagueType == "nhl_style"
            ? "Medium-high - broader geography means more early uncertainty."
            : "Medium - prior regional viewings give staff some context.";
    }

    private static string ScoutQuoteFor(DraftClassTheme theme, int year) =>
        theme switch
        {
            DraftClassTheme.DeepDefenseClass => $"{year} looks like a deep defense class with several top-pair or top-four candidates.",
            DraftClassTheme.StrongGoalieClass => $"{year} has more goalie intrigue than usual; the position needs careful viewings.",
            DraftClassTheme.BoomBustClass => $"{year} could reward aggressive scouting, but the miss rate may be higher.",
            DraftClassTheme.InternationalHeavyClass => $"{year} has a wider map, and translation to North American hockey is the story.",
            DraftClassTheme.WeakOverallClass => $"{year} may force clubs to value fit and evidence over ceiling.",
            _ => $"{year} has a distinct class identity; scouts should use context with every ranking."
        };

    private static (string Hometown, string Region, string Country) ResolveRegion(DraftClassProfile profile, string birthplace, int index)
    {
        if (profile.Theme == DraftClassTheme.InternationalHeavyClass && index % 2 == 0)
        {
            var europe = new[]
            {
                ("Turku", "Varsinais-Suomi", "Finland"),
                ("Gothenburg", "Vastra Gotaland", "Sweden"),
                ("Brno", "South Moravia", "Czechia"),
                ("Zurich", "ZH", "Switzerland"),
                ("Riga", "Riga", "Latvia")
            };
            return europe[index % europe.Length];
        }

        var parts = birthplace.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            return (parts[0], parts[1], parts[2]);
        }

        var local = new[] { ("Saskatoon", "SK", "Canada"), ("Red Deer", "AB", "Canada"), ("Brandon", "MB", "Canada"), ("Kelowna", "BC", "Canada"), ("Regina", "SK", "Canada"), ("Winnipeg", "MB", "Canada"), ("Grand Forks", "ND", "USA") };
        return local[index % local.Length];
    }

    private static string LeagueFor(DraftClassProfile profile, string country, int index) =>
        country switch
        {
            "USA" => index % 2 == 0 ? "USHL Futures" : "High School",
            "Finland" => "U18 SM-sarja",
            "Sweden" => "J18 Nationell",
            "Czechia" => "Czech U20",
            "Switzerland" => "U20-Elit",
            "Latvia" => "Latvian U20",
            _ => profile.LeagueId.Contains("nhl", StringComparison.OrdinalIgnoreCase) ? "CHL / Junior placeholder" : index % 3 == 0 ? "CSSHL U18" : index % 3 == 1 ? "SMAAAHL" : "AEHL U18"
        };

    private static string CharacterFor(DraftClassProfile profile, int index)
    {
        if (profile.Theme == DraftClassTheme.HighCharacterClass)
        {
            return "High-character profile; leadership, practice habits, and coachability stand out.";
        }

        return index % 5 == 0 ? "High-energy personality; leadership traits are emerging." :
            index % 3 == 0 ? "Driven player who wants a clear development plan." :
            "Solid character profile; staff need more viewings before a firm recommendation.";
    }

    private static string RoleFor(DraftClassProfile profile, RosterPosition position, int index)
    {
        var topEnd = profile.Theme == DraftClassTheme.EliteTopEnd && index < 5;
        return position switch
        {
            RosterPosition.Goalie => topEnd ? "potential starter goalie projection" : "development goalie with starter upside",
            RosterPosition.Defense => topEnd ? "potential top-pair defense projection" : "top-four or second-pair defense projection",
            RosterPosition.Center => topEnd ? "potential first-line center projection" : "middle-six center projection",
            RosterPosition.LeftWing or RosterPosition.RightWing => topEnd ? "potential scoring-line winger projection" : "scoring-line winger projection",
            _ => "depth lineup projection"
        };
    }

    private static string TeamNickname(int index) =>
        new[] { "Raiders", "Blazers", "Kings", "Flyers", "Storm", "Royals", "Tigers", "Saints", "Lions", "Vikings" }[index % 10];

    private static string PositionText(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "Goalie",
            RosterPosition.Defense => "Defense",
            RosterPosition.Center => "Center",
            RosterPosition.LeftWing => "Left wing",
            RosterPosition.RightWing => "Right wing",
            _ => "Prospect"
        };

    private static string ReadableTheme(DraftClassTheme theme) =>
        string.Concat(theme.ToString().Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = hash * 31 + ch;
            }

            return hash;
        }
    }
}
