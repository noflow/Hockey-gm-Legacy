namespace LegacyEngine.Integration;

public sealed record DevelopmentCoachProfile(
    string CoachPersonId,
    string CoachName,
    IReadOnlyList<DevelopmentCoachSpecialty> Specialties,
    int FitScore,
    string FitSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CoachPersonId))
        {
            throw new ArgumentException("Development coach id is required.", nameof(CoachPersonId));
        }

        if (string.IsNullOrWhiteSpace(CoachName))
        {
            throw new ArgumentException("Development coach name is required.", nameof(CoachName));
        }

        if (Specialties.Count == 0)
        {
            throw new ArgumentException("Development coach must have at least one specialty.", nameof(Specialties));
        }

        if (FitScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(FitScore), "Coach fit score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(FitSummary))
        {
            throw new ArgumentException("Development coach fit summary is required.", nameof(FitSummary));
        }
    }
}
