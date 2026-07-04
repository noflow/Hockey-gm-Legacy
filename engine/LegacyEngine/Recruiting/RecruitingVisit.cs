namespace LegacyEngine.Recruiting;

public sealed record RecruitingVisit(
    string VisitId,
    string OrganizationId,
    DateOnly Date,
    int FitScore,
    string Notes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(VisitId))
        {
            throw new ArgumentException("Visit id is required.", nameof(VisitId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (FitScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(FitScore), "Visit fit score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Notes))
        {
            throw new ArgumentException("Visit notes are required.", nameof(Notes));
        }
    }
}
