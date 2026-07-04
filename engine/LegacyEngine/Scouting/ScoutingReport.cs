namespace LegacyEngine.Scouting;

public sealed record ScoutingReport(
    string ReportId,
    string PlayerId,
    string ScoutId,
    string AssignmentId,
    DateOnly CreatedOn,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Observations,
    IReadOnlyList<string> Opinions,
    IReadOnlyList<string> Unknowns,
    ScoutingConfidenceLevel Confidence,
    ScoutedRatingRange CurrentAbilityEstimate,
    ScoutedRatingRange PotentialEstimate,
    ScoutingRecommendation Recommendation,
    IReadOnlyDictionary<string, object?> Details)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReportId))
        {
            throw new ArgumentException("Report id is required.", nameof(ReportId));
        }

        if (string.IsNullOrWhiteSpace(PlayerId))
        {
            throw new ArgumentException("Player id is required.", nameof(PlayerId));
        }

        if (string.IsNullOrWhiteSpace(ScoutId))
        {
            throw new ArgumentException("Scout id is required.", nameof(ScoutId));
        }

        if (string.IsNullOrWhiteSpace(AssignmentId))
        {
            throw new ArgumentException("Assignment id is required.", nameof(AssignmentId));
        }

        CurrentAbilityEstimate.Validate();
        PotentialEstimate.Validate();

        if (Facts.Count == 0 || Observations.Count == 0 || Opinions.Count == 0 || Unknowns.Count == 0)
        {
            throw new ArgumentException("Scouting reports must include facts, observations, opinions, and unknowns.");
        }
    }
}
