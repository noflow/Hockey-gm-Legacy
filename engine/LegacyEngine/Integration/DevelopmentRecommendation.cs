namespace LegacyEngine.Integration;

public sealed record DevelopmentRecommendation(
    string RecommendationId,
    string PersonId,
    string PlayerName,
    DevelopmentRecommendationType RecommendationType,
    DateOnly CreatedOn,
    string Reason,
    string RecommendedAction,
    bool IsActive = true)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecommendationId))
        {
            throw new ArgumentException("Development recommendation id is required.", nameof(RecommendationId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Development recommendation person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(RecommendedAction))
        {
            throw new ArgumentException("Development recommendation requires player name, reason, and action.");
        }
    }
}
