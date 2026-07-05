namespace LegacyEngine.Seasons;

/// <summary>
/// Static catalog describing each season milestone: its chronological order, the
/// phase (if any) the season moves into when the milestone is reached, and a
/// human-readable label. This is milestone semantics, not league timing; the
/// actual dates come from <see cref="SeasonSettings"/> (rulebook-driven).
/// </summary>
public static class SeasonMilestones
{
    public static IReadOnlyList<SeasonMilestoneType> InScheduleOrder { get; } =
    [
        SeasonMilestoneType.TrainingCampOpens,
        SeasonMilestoneType.SeasonBegins,
        SeasonMilestoneType.TradeDeadline,
        SeasonMilestoneType.PlayoffsBegin,
        SeasonMilestoneType.Championship,
        SeasonMilestoneType.Awards,
        SeasonMilestoneType.RecruitingOpens,
        SeasonMilestoneType.RecruitingCloses,
        SeasonMilestoneType.DraftLottery,
        SeasonMilestoneType.Draft,
        SeasonMilestoneType.FreeAgencyOpens,
        SeasonMilestoneType.FreeAgencyEnds
    ];

    /// <summary>The phase entered when a milestone is reached, or null for pure markers.</summary>
    public static SeasonPhase? TargetPhase(SeasonMilestoneType type) => type switch
    {
        SeasonMilestoneType.TrainingCampOpens => SeasonPhase.Preseason,
        SeasonMilestoneType.SeasonBegins => SeasonPhase.RegularSeason,
        SeasonMilestoneType.TradeDeadline => SeasonPhase.TradeDeadline,
        SeasonMilestoneType.PlayoffsBegin => SeasonPhase.Playoffs,
        SeasonMilestoneType.Championship => SeasonPhase.Championship,
        SeasonMilestoneType.Awards => SeasonPhase.Offseason,
        SeasonMilestoneType.RecruitingOpens => SeasonPhase.Recruiting,
        SeasonMilestoneType.RecruitingCloses => SeasonPhase.Offseason,
        SeasonMilestoneType.DraftLottery => null,
        SeasonMilestoneType.Draft => SeasonPhase.Draft,
        SeasonMilestoneType.FreeAgencyOpens => SeasonPhase.FreeAgency,
        SeasonMilestoneType.FreeAgencyEnds => SeasonPhase.Offseason,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown season milestone type.")
    };

    public static string Label(SeasonMilestoneType type) => type switch
    {
        SeasonMilestoneType.TrainingCampOpens => "Training Camp Opens",
        SeasonMilestoneType.SeasonBegins => "Season Begins",
        SeasonMilestoneType.TradeDeadline => "Trade Deadline",
        SeasonMilestoneType.PlayoffsBegin => "Playoffs Begin",
        SeasonMilestoneType.Championship => "Championship",
        SeasonMilestoneType.Awards => "Awards",
        SeasonMilestoneType.RecruitingOpens => "Recruiting Opens",
        SeasonMilestoneType.RecruitingCloses => "Recruiting Closes",
        SeasonMilestoneType.DraftLottery => "Draft Lottery (future)",
        SeasonMilestoneType.Draft => "Draft",
        SeasonMilestoneType.FreeAgencyOpens => "Free Agency Opens",
        SeasonMilestoneType.FreeAgencyEnds => "Free Agency Ends",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown season milestone type.")
    };
}
