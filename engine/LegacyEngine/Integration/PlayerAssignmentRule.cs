namespace LegacyEngine.Integration;

public sealed record PlayerAssignmentRule(
    int JuniorAgeCutoff,
    int AhlEligibilityAge,
    bool ChlToAhlRestrictionEnabled,
    bool OneNineteenYearOldChlExceptionEnabled,
    bool EuropeanAndCollegeProspectsCanPlayAhlAt18,
    int ElcSlideAgeCutoff,
    int ElcSlideNhlGameThreshold)
{
    public void Validate()
    {
        if (JuniorAgeCutoff <= 0 || AhlEligibilityAge <= 0 || ElcSlideAgeCutoff <= 0 || ElcSlideNhlGameThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(JuniorAgeCutoff), "Player assignment rule ages and thresholds must be positive.");
        }
    }
}
