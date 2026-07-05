namespace LegacyEngine.Staff;

/// <summary>
/// A recorded performance review for a staff member. Reviews accumulate on the
/// <see cref="StaffMember"/> and inform, but do not by themselves drive, evaluations.
/// </summary>
public sealed record StaffPerformance(
    string ReviewId,
    string PersonId,
    string OrganizationId,
    DateOnly ReviewDate,
    int Rating,
    string Summary,
    IReadOnlyDictionary<string, object?> Metrics)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReviewId))
        {
            throw new ArgumentException("Review id is required.", nameof(ReviewId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Performance review summary is required.", nameof(Summary));
        }

        if (Rating is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Rating), "Performance rating must be between 0 and 100.");
        }

        if (Metrics is null)
        {
            throw new ArgumentNullException(nameof(Metrics), "Performance metrics dictionary is required.");
        }
    }
}
