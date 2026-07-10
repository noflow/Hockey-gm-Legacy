using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum DraftDistributionProfileType
{
    NhlEntryDraft,
    WhlProspectsDraft,
    OhlPrioritySelection,
    QmjhlDraft,
    CustomFallback
}

public enum DraftPositionGroup
{
    Center,
    LeftWing,
    RightWing,
    Defense,
    Goalie
}

public enum DraftBoardValidationSeverity
{
    Information,
    Warning,
    Invalid
}

public sealed record DraftCountRange(int Minimum, int Maximum)
{
    public bool Contains(int value) => value >= Minimum && value <= Maximum;

    public void Validate()
    {
        if (Minimum < 0 || Maximum < Minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(DraftCountRange), "Draft count ranges must be non-negative and ordered.");
        }
    }
}

public sealed record HistoricalDraftDistributionProfile(
    DraftDistributionProfileType ProfileType,
    string LeagueLabel,
    DraftCountRange TopFiveGoalies,
    DraftCountRange TopTenGoalies,
    DraftCountRange FirstRoundForwards,
    DraftCountRange FirstRoundDefense,
    DraftCountRange FirstRoundGoalies,
    DraftCountRange TopFiftyForwards,
    DraftCountRange TopFiftyDefense,
    DraftCountRange TopFiftyGoalies,
    int FirstRoundSize,
    int MaximumConsecutiveSamePosition,
    int MaximumConsecutiveGoalies,
    DraftCountRange ExpectedFirstGoalieRange,
    DraftCountRange ExpectedFirstDefenseRange,
    DraftCountRange ExpectedFirstWingerRange,
    string Summary)
{
    public static HistoricalDraftDistributionProfile ForLeague(string leagueType, string leagueId)
    {
        var id = leagueId.ToLowerInvariant();
        if (leagueType.Equals("nhl", StringComparison.OrdinalIgnoreCase) || id.Contains("nhl", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                DraftDistributionProfileType.NhlEntryDraft,
                "NHL Entry style",
                new DraftCountRange(0, 1),
                new DraftCountRange(0, 1),
                new DraftCountRange(16, 24),
                new DraftCountRange(6, 13),
                new DraftCountRange(0, 3),
                new DraftCountRange(28, 39),
                new DraftCountRange(9, 18),
                new DraftCountRange(1, 6),
                32,
                6,
                2,
                new DraftCountRange(8, 48),
                new DraftCountRange(1, 14),
                new DraftCountRange(1, 16),
                "NHL-style boards favor skaters early, give centers/RD modest premiums, and usually require goalies to separate clearly.");
        }

        if (id.Contains("whl", StringComparison.OrdinalIgnoreCase))
        {
            return Junior(DraftDistributionProfileType.WhlProspectsDraft, "WHL prospects style", 7, 4, "WHL-style boards tolerate more regional variance and a little extra defense value.");
        }

        if (id.Contains("ohl", StringComparison.OrdinalIgnoreCase))
        {
            return Junior(DraftDistributionProfileType.OhlPrioritySelection, "OHL priority style", 6, 3, "OHL-style boards lean forward-heavy but still avoid single-position clusters.");
        }

        if (id.Contains("qmjhl", StringComparison.OrdinalIgnoreCase) || id.Contains("q", StringComparison.OrdinalIgnoreCase))
        {
            return Junior(DraftDistributionProfileType.QmjhlDraft, "QMJHL style", 6, 4, "QMJHL-style boards allow wider goalie and regional uncertainty than NHL boards.");
        }

        return leagueType.Equals("junior", StringComparison.OrdinalIgnoreCase)
            ? Junior(DraftDistributionProfileType.CustomFallback, "Junior fallback style", 6, 4, "Junior fallback boards use broad ranges with less goalie discounting than NHL boards.")
            : new(
                DraftDistributionProfileType.CustomFallback,
                "Custom fallback style",
                new DraftCountRange(0, 2),
                new DraftCountRange(0, 2),
                new DraftCountRange(12, 25),
                new DraftCountRange(5, 16),
                new DraftCountRange(0, 4),
                new DraftCountRange(24, 40),
                new DraftCountRange(8, 20),
                new DraftCountRange(1, 7),
                30,
                7,
                2,
                new DraftCountRange(6, 55),
                new DraftCountRange(1, 18),
                new DraftCountRange(1, 18),
                "Custom fallback boards use broad ranges and preserve exceptional talent.");
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeagueLabel) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Historical draft distribution profile requires readable context.");
        }

        foreach (var range in new[]
        {
            TopFiveGoalies,
            TopTenGoalies,
            FirstRoundForwards,
            FirstRoundDefense,
            FirstRoundGoalies,
            TopFiftyForwards,
            TopFiftyDefense,
            TopFiftyGoalies,
            ExpectedFirstGoalieRange,
            ExpectedFirstDefenseRange,
            ExpectedFirstWingerRange
        })
        {
            range.Validate();
        }
    }

    private static HistoricalDraftDistributionProfile Junior(DraftDistributionProfileType type, string label, int maxRun, int firstRoundGoaliesMax, string summary) =>
        new(
            type,
            label,
            new DraftCountRange(0, 1),
            new DraftCountRange(0, 2),
            new DraftCountRange(12, 22),
            new DraftCountRange(5, 13),
            new DraftCountRange(0, firstRoundGoaliesMax),
            new DraftCountRange(26, 39),
            new DraftCountRange(8, 18),
            new DraftCountRange(2, 8),
            28,
            maxRun,
            2,
            new DraftCountRange(5, 42),
            new DraftCountRange(1, 16),
            new DraftCountRange(1, 14),
            summary);
}

public sealed record DraftPositionAdjustment(
    DraftPositionGroup Position,
    int BaseAdjustment,
    int ScarcityAdjustment,
    int ThemeAdjustment,
    int RiskAdjustment,
    string Explanation)
{
    public int TotalAdjustment => BaseAdjustment + ScarcityAdjustment + ThemeAdjustment + RiskAdjustment;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("Draft position adjustment requires an explanation.", nameof(Explanation));
        }
    }
}

public sealed record DraftPositionValueProfile(
    string LeagueType,
    string LeagueId,
    IReadOnlyList<DraftPositionAdjustment> Adjustments,
    string Summary)
{
    public DraftPositionAdjustment For(DraftPositionGroup position) =>
        Adjustments.FirstOrDefault(adjustment => adjustment.Position == position)
        ?? new DraftPositionAdjustment(position, 0, 0, 0, 0, "No position adjustment configured.");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeagueType) || string.IsNullOrWhiteSpace(LeagueId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft position value profile requires league context.");
        }

        foreach (var adjustment in Adjustments)
        {
            adjustment.Validate();
        }
    }
}

public sealed record DraftBoardRealismProfile(
    HistoricalDraftDistributionProfile HistoricalDistribution,
    int ComparableValueWindow,
    int MaximumPasses,
    bool PreserveEliteExceptions,
    string Summary)
{
    public void Validate()
    {
        HistoricalDistribution.Validate();
        if (ComparableValueWindow <= 0 || MaximumPasses <= 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft board realism profile requires comparable window, passes, and summary.");
        }
    }
}

public sealed record DraftBoardValidationIssue(
    DraftBoardValidationSeverity Severity,
    string Code,
    string Scope,
    string Message)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Scope) || string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Draft board validation issue requires code, scope, and message.");
        }
    }
}

public sealed record DraftBoardValidationResult(
    bool IsValid,
    IReadOnlyList<DraftBoardValidationIssue> Issues,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft board validation result requires a summary.", nameof(Summary));
        }

        foreach (var issue in Issues)
        {
            issue.Validate();
        }
    }
}

public sealed record DraftPositionValueEvaluation(
    string ProspectPersonId,
    string ProspectName,
    DraftPositionGroup Position,
    int Rank,
    int DraftValue,
    string Explanation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId) || string.IsNullOrWhiteSpace(ProspectName) || string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("Draft position value evaluation requires prospect context.");
        }
    }
}

public sealed record DraftBoardRebalancingResult(
    bool Rebalanced,
    int Passes,
    IReadOnlyList<string> Moves,
    DraftBoardValidationResult Before,
    DraftBoardValidationResult After,
    string Summary)
{
    public void Validate()
    {
        Before.Validate();
        After.Validate();
        if (Passes < 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft board rebalancing result requires passes and summary.");
        }
    }
}

public static class DraftPositionGroupMapper
{
    public static DraftPositionGroup FromRosterPosition(RosterPosition position) =>
        position switch
        {
            RosterPosition.Center => DraftPositionGroup.Center,
            RosterPosition.LeftWing => DraftPositionGroup.LeftWing,
            RosterPosition.RightWing => DraftPositionGroup.RightWing,
            RosterPosition.Goalie => DraftPositionGroup.Goalie,
            _ => DraftPositionGroup.Defense
        };

    public static bool IsForward(DraftPositionGroup position) =>
        position is DraftPositionGroup.Center or DraftPositionGroup.LeftWing or DraftPositionGroup.RightWing;
}
