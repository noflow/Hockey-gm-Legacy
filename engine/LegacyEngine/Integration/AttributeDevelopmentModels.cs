using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum AttributeGrowthReason
{
    AgeCurve,
    TrainingFocus,
    LineupRole,
    SpecialTeamsUsage,
    CoachSpecialty,
    DevelopmentStaff,
    Morale,
    RelationshipTrust,
    WorkEthic,
    Coachability,
    Professionalism,
    TeamCulture,
    Experience,
    LateBloomer,
    DevelopmentReport
}

public enum AttributeRegressionReason
{
    Injury,
    Fatigue,
    PoorRole,
    RushedTooEarly,
    LowMorale,
    PoorChemistry,
    BadFit,
    Plateau,
    Aging,
    BrokenPromise
}

public sealed record AttributeDevelopmentModifier(
    int Age,
    int YearsElapsed,
    LeagueExperience LeagueLevel,
    DevelopmentIceTimeRole LineupRole,
    bool PowerPlayUsage,
    bool PenaltyKillUsage,
    DevelopmentCoachSpecialty? CoachSpecialty,
    int DevelopmentStaffQuality,
    int Morale,
    int RelationshipTrust,
    int InjuryPenalty,
    int FatiguePenalty,
    int WorkEthic,
    int Coachability,
    int Professionalism,
    int TeamCulture,
    bool RushedTooEarly,
    bool PoorRole,
    bool UpdateVisibleEstimate,
    int RandomSeedModifier = 0)
{
    public void Validate()
    {
        foreach (var score in new[] { DevelopmentStaffQuality, Morale, RelationshipTrust, WorkEthic, Coachability, Professionalism, TeamCulture })
        {
            if (score is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(AttributeDevelopmentModifier), "Development modifier scores must stay within 0-100.");
            }
        }

        if (Age < 0 || YearsElapsed < 0 || InjuryPenalty < 0 || FatiguePenalty < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AttributeDevelopmentModifier), "Age, years, and penalties cannot be negative.");
        }
    }
}

public sealed record AttributeGrowthEvent(
    string EventId,
    string PersonId,
    string PlayerName,
    DateOnly Date,
    PlayerAttributeKey Attribute,
    int Delta,
    AttributeGrowthReason? GrowthReason,
    AttributeRegressionReason? RegressionReason,
    bool IsBreakthrough,
    bool IsPlateau,
    string Summary)
{
    public bool IsRegression => Delta < 0 || RegressionReason is not null;

    public bool IsMeaningful => IsBreakthrough || IsRegression || IsPlateau || Math.Abs(Delta) >= 2;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId) || string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Attribute growth event requires identity and summary.");
        }

        if (GrowthReason is null && RegressionReason is null)
        {
            throw new ArgumentException("Attribute growth event requires a growth or regression reason.");
        }
    }
}

public sealed record AttributeDevelopmentSnapshot(
    string SnapshotId,
    string PersonId,
    string PlayerName,
    DateOnly Date,
    IReadOnlyList<DevelopmentPlanFocus> TrainingFocus,
    int OverallBefore,
    int OverallAfter,
    int PotentialBefore,
    int PotentialAfter,
    string BiggestGain,
    string BiggestRegression,
    bool VisibleEstimateUpdated,
    bool VisibleEstimateStale,
    string StaffNote,
    IReadOnlyList<AttributeGrowthEvent> Events)
{
    public int OverallDelta => OverallAfter - OverallBefore;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SnapshotId) || string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(StaffNote))
        {
            throw new ArgumentException("Attribute development snapshot requires identity and staff note.");
        }

        if (TrainingFocus.Count == 0)
        {
            throw new ArgumentException("Attribute development snapshot requires a training focus.", nameof(TrainingFocus));
        }

        foreach (var score in new[] { OverallBefore, OverallAfter, PotentialBefore, PotentialAfter })
        {
            if (score is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(AttributeDevelopmentSnapshot), "Development ratings must stay within 0-100.");
            }
        }

        foreach (var growthEvent in Events)
        {
            growthEvent.Validate();
        }
    }
}

public sealed record AttributeDevelopmentSummary(
    DateOnly ReportDate,
    IReadOnlyList<AttributeDevelopmentSnapshot> Snapshots,
    IReadOnlyList<string> BiggestGains,
    IReadOnlyList<string> BiggestRegressions,
    IReadOnlyList<string> BreakthroughCandidates,
    IReadOnlyList<string> PlateauRisks,
    IReadOnlyList<string> RushedProspects,
    IReadOnlyList<string> RecommendedFocusChanges,
    IReadOnlyList<string> StaffComments)
{
    public void Validate()
    {
        foreach (var snapshot in Snapshots)
        {
            snapshot.Validate();
        }
    }
}

public sealed record AttributeDevelopmentResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    AttributeDevelopmentSnapshot Snapshot,
    IReadOnlyList<ActionCenterItem> ActionItems,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Snapshot.Validate();
        foreach (var item in ActionItems)
        {
            item.Validate();
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Attribute development result requires a summary.", nameof(Summary));
        }
    }
}
