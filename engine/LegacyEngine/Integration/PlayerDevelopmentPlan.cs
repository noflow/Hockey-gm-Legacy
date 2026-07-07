namespace LegacyEngine.Integration;

public sealed record PlayerDevelopmentPlan(
    string PersonId,
    IReadOnlyList<DevelopmentPlanFocus> FocusAreas,
    DevelopmentIceTimeRole IceTimeRole,
    int Confidence,
    DevelopmentMorale Morale,
    string CoachPersonId,
    DateOnly LastReviewed,
    string CoachComment,
    string GmComment)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Development plan person id is required.", nameof(PersonId));
        }

        if (FocusAreas.Count == 0)
        {
            throw new ArgumentException("Development plan must include at least one focus area.", nameof(FocusAreas));
        }

        if (FocusAreas.Distinct().Count() != FocusAreas.Count)
        {
            throw new ArgumentException("Development focus areas must be unique.", nameof(FocusAreas));
        }

        if (Confidence is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Confidence), "Development confidence must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(CoachPersonId))
        {
            throw new ArgumentException("Development coach person id is required.", nameof(CoachPersonId));
        }

        if (string.IsNullOrWhiteSpace(CoachComment))
        {
            throw new ArgumentException("Development coach comment is required.", nameof(CoachComment));
        }

        if (GmComment is null)
        {
            throw new ArgumentException("Development GM comment cannot be null.", nameof(GmComment));
        }
    }
}
