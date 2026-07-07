namespace LegacyEngine.Integration;

public sealed record StaffPerformanceReview(
    string PersonId,
    string StaffName,
    DateOnly ReviewDate,
    StaffPerformanceOutcome Outcome,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Concerns,
    string Recommendation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(StaffName)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(Recommendation))
        {
            throw new ArgumentException("Staff performance review requires identity and recommendation text.");
        }
    }
}
