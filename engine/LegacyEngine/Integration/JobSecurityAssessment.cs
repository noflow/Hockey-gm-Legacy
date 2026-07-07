namespace LegacyEngine.Integration;

public sealed record JobSecurityAssessment(
    JobSecurityLevel Level,
    int Score,
    string Explanation,
    IReadOnlyList<string> Reasons)
{
    public void Validate()
    {
        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Job security score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Explanation) || Reasons.Count == 0 || Reasons.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Job security assessment requires explanation and reasons.");
        }
    }
}
