namespace LegacyEngine.Integration;

public sealed record DevelopmentReview(
    string ReviewId,
    string PersonId,
    string PlayerName,
    int SeasonYear,
    DateOnly ReviewDate,
    IReadOnlyList<string> ImprovedThemes,
    IReadOnlyList<string> RegressionThemes,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string CoachComment,
    string ScoutComment,
    string GmComment,
    string FutureProjection)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReviewId))
        {
            throw new ArgumentException("Development review id is required.", nameof(ReviewId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Development review person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(CoachComment)
            || string.IsNullOrWhiteSpace(ScoutComment) || string.IsNullOrWhiteSpace(FutureProjection))
        {
            throw new ArgumentException("Development review requires player name, comments, and projection.");
        }
    }
}
