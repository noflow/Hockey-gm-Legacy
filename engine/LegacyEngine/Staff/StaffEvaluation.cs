namespace LegacyEngine.Staff;

/// <summary>
/// The output of evaluating a staff member: an overall score, a recommendation, and
/// narrative strengths, weaknesses, and development suggestions. It is a read-only
/// report and does not itself change employment or trigger firing.
/// </summary>
public sealed record StaffEvaluation(
    string PersonId,
    string OrganizationId,
    StaffRole Role,
    DateOnly EvaluatedOn,
    int OverallEvaluation,
    StaffRecommendation Recommendation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> DevelopmentSuggestions,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (OverallEvaluation is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(OverallEvaluation), "Overall evaluation must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Evaluation summary is required.", nameof(Summary));
        }
    }
}
