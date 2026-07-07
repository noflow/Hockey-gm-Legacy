namespace LegacyEngine.Integration;

public sealed record OwnerPerformanceReview(
    string ReviewId,
    int SeasonYear,
    OwnerPerformanceGrade OverallGrade,
    IReadOnlyDictionary<string, OwnerPerformanceGrade> CategoryGrades,
    string Narrative,
    string Recommendation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReviewId)
            || string.IsNullOrWhiteSpace(Narrative)
            || string.IsNullOrWhiteSpace(Recommendation))
        {
            throw new ArgumentException("Owner performance review requires id, narrative, and recommendation.");
        }

        if (SeasonYear < 1 || CategoryGrades.Count == 0)
        {
            throw new ArgumentException("Owner performance review requires season year and category grades.");
        }
    }
}
