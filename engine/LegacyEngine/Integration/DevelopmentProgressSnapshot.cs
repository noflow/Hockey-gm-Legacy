namespace LegacyEngine.Integration;

public sealed record DevelopmentProgressSnapshot(
    string PersonId,
    DateOnly Date,
    DevelopmentOutcomeType Outcome,
    IReadOnlyList<string> ImprovedThemes,
    IReadOnlyList<string> RegressionThemes,
    int ConfidenceChange,
    DevelopmentMorale Morale,
    string Summary,
    string CoachComment,
    string ScoutComment)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Progress person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(Summary) || string.IsNullOrWhiteSpace(CoachComment) || string.IsNullOrWhiteSpace(ScoutComment))
        {
            throw new ArgumentException("Development progress requires summary, coach comment, and scout comment.");
        }
    }
}
