using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum DevelopmentCurveType
{
    EarlyBloomer,
    SteadyDeveloper,
    LateBloomer,
    SlowBurn,
    BoomBust,
    HighFloor,
    RawToolsyProspect,
    OverAger,
    InjuryRisk,
    MentallyMature,
    NeedsPatience
}

public enum DevelopmentPace
{
    Fast,
    Normal,
    Slow,
    VerySlow,
    Unpredictable
}

public enum PotentialOutcomeResult
{
    ExceededProjection,
    MetProjection,
    BelowProjection,
    Plateaued,
    BrokeOut,
    LateBloomer,
    Bust,
    RevivedCareer
}

public enum DevelopmentEventType
{
    TrainingBreakthrough,
    ConfidenceSurge,
    RoleDrivenGrowth,
    CoachUnlocksPotential,
    RushedDevelopmentWarning,
    PlateauWarning,
    LateBloomerEmerging,
    PotentialRevisedUpward,
    PotentialRevisedDownward
}

public sealed record PotentialVariance(
    int ProjectedCeilingLow,
    int ProjectedCeilingHigh,
    int CurrentEstimatedCeiling,
    int HiddenTrueCeiling,
    int DevelopmentEnvironmentModifier,
    int ProbabilityExceedingProjection,
    int ProbabilityMissingProjection,
    int PlateauRisk,
    int BreakoutChance)
{
    public void Validate()
    {
        foreach (var value in new[] { ProjectedCeilingLow, ProjectedCeilingHigh, CurrentEstimatedCeiling, HiddenTrueCeiling })
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(PotentialVariance), "Potential variance ratings must stay within 0-100.");
            }
        }

        if (ProjectedCeilingLow > ProjectedCeilingHigh)
        {
            throw new ArgumentException("Projected ceiling range is invalid.", nameof(PotentialVariance));
        }

        foreach (var value in new[] { ProbabilityExceedingProjection, ProbabilityMissingProjection, PlateauRisk, BreakoutChance })
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(PotentialVariance), "Potential probabilities must stay within 0-100.");
            }
        }
    }
}

public sealed record DevelopmentBreakthrough(
    string BreakthroughId,
    string PersonId,
    DateOnly Date,
    DevelopmentEventType EventType,
    string Summary,
    int OverallImpact,
    int PotentialEstimateImpact)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BreakthroughId) || string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Development breakthrough requires identity and summary.");
        }
    }
}

public sealed record DevelopmentSetback(
    string SetbackId,
    string PersonId,
    DateOnly Date,
    DevelopmentEventType EventType,
    string Summary,
    int OverallImpact,
    int PlateauRiskImpact)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SetbackId) || string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Development setback requires identity and summary.");
        }
    }
}

public sealed record PotentialOutcome(
    string PersonId,
    string PlayerName,
    PotentialOutcomeResult Result,
    int ProjectedOverall,
    int ProjectedCeiling,
    int YearsElapsed,
    string Summary,
    IReadOnlyList<string> Reasons)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Potential outcome requires player identity and summary.");
        }

        if (ProjectedOverall is < 0 or > 100 || ProjectedCeiling is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(PotentialOutcome), "Potential outcome ratings must stay within 0-100.");
        }
    }
}

public sealed record DevelopmentCurveContext(
    int CoachingQuality,
    bool CorrectRole,
    bool StrongDevelopmentPlan,
    bool GoodMorale,
    bool EnoughIceTime,
    bool ProperLeaguePlacement,
    bool MajorInjury,
    bool RushedTooEarly,
    bool BrokenPromise)
{
    public static DevelopmentCurveContext Strong { get; } = new(85, true, true, true, true, true, false, false, false);

    public static DevelopmentCurveContext Poor { get; } = new(35, false, false, false, false, false, false, true, true);

    public int Score =>
        Math.Clamp((CoachingQuality - 50) / 5
            + (CorrectRole ? 8 : -8)
            + (StrongDevelopmentPlan ? 7 : -5)
            + (GoodMorale ? 5 : -7)
            + (EnoughIceTime ? 6 : -8)
            + (ProperLeaguePlacement ? 8 : -10)
            + (MajorInjury ? -14 : 0)
            + (RushedTooEarly ? -12 : 0)
            + (BrokenPromise ? -8 : 0), -45, 45);
}

public sealed record PlayerDevelopmentCurve(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    DevelopmentCurveType CurveType,
    DevelopmentPace Pace,
    PotentialVariance Variance,
    int TimeToImpactLowYears,
    int TimeToImpactHighYears,
    string StaffDevelopmentNote,
    string BestDevelopmentPath,
    IReadOnlyList<DevelopmentBreakthrough> Breakthroughs,
    IReadOnlyList<DevelopmentSetback> Setbacks,
    DateOnly LastUpdated)
{
    public string TimeToImpactDisplay => $"{TimeToImpactLowYears}-{TimeToImpactHighYears} years";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(StaffDevelopmentNote)
            || string.IsNullOrWhiteSpace(BestDevelopmentPath))
        {
            throw new ArgumentException("Development curve requires player identity, staff note, and best path.");
        }

        if (TimeToImpactLowYears < 0 || TimeToImpactHighYears < TimeToImpactLowYears)
        {
            throw new ArgumentException("Development curve ETA is invalid.");
        }

        Variance.Validate();
        foreach (var breakthrough in Breakthroughs)
        {
            breakthrough.Validate();
        }

        foreach (var setback in Setbacks)
        {
            setback.Validate();
        }
    }
}
