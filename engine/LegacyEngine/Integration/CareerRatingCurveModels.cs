using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum PlayerGrowthStage
{
    RawProspect,
    EarlyDevelopment,
    Developing,
    ApproachingNhl,
    Emerging,
    NearPeak,
    Peak,
    MaintainingPeak,
    EarlyDecline,
    Declining,
    LateCareer,
    Retired
}

public sealed record PlayerPeakWindow(int ExpectedStartAge, int ExpectedEndAge, int? ActualPeakAge = null)
{
    public void Validate()
    {
        if (ExpectedStartAge < 16 || ExpectedEndAge < ExpectedStartAge || ActualPeakAge is < 16)
        {
            throw new ArgumentException("Player peak window is invalid.");
        }
    }
}

public sealed record PlayerPeakProfile(int ExpectedPeakOverall, int CareerHighOverall, bool AtEstimatedCeiling, string Summary)
{
    public void Validate()
    {
        if (ExpectedPeakOverall is < 0 or > 100 || CareerHighOverall is < 0 or > 100 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player peak profile is invalid.");
        }
    }
}

public sealed record PlayerDeclineProfile(int DeclineStartAge, int DeclineSpeed, int InjurySensitivity, string Summary)
{
    public void Validate()
    {
        if (DeclineStartAge < 16 || DeclineSpeed is < 1 or > 10 || InjurySensitivity is < 0 or > 100 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player decline profile is invalid.");
        }
    }
}

public sealed record PlayerDevelopmentTarget(
    string PrimaryFocus,
    string BestLeaguePlacement,
    string RecommendedUsage,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PrimaryFocus) || string.IsNullOrWhiteSpace(BestLeaguePlacement)
            || string.IsNullOrWhiteSpace(RecommendedUsage) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player development target is invalid.");
        }
    }
}

public sealed record PlayerRatingTrajectory(
    int OriginalPotentialEstimate,
    int CurrentPotentialEstimate,
    int ProjectedPeakOverall,
    int CareerHighOverall,
    string Trend,
    string OutcomeSummary)
{
    public void Validate()
    {
        if (OriginalPotentialEstimate is < 0 or > 100 || CurrentPotentialEstimate is < 0 or > 100
            || ProjectedPeakOverall is < 0 or > 100 || CareerHighOverall is < 0 or > 100
            || string.IsNullOrWhiteSpace(Trend) || string.IsNullOrWhiteSpace(OutcomeSummary))
        {
            throw new ArgumentException("Player rating trajectory is invalid.");
        }
    }
}

public sealed record PlayerCareerRatingCurve(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    PlayerGrowthStage GrowthStage,
    PlayerPeakWindow PeakWindow,
    PlayerPeakProfile PeakProfile,
    PlayerDeclineProfile DeclineProfile,
    PlayerDevelopmentTarget DevelopmentTarget,
    PlayerRatingTrajectory Trajectory,
    DateOnly LastUpdated)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName))
        {
            throw new ArgumentException("Career rating curve requires player identity.");
        }

        PeakWindow.Validate();
        PeakProfile.Validate();
        DeclineProfile.Validate();
        DevelopmentTarget.Validate();
        Trajectory.Validate();
    }
}
