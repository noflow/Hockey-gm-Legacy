using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record TrainingCampEvaluation(
    string EvaluationId,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int CampScore,
    string Readiness,
    string DevelopmentUpside,
    string CoachNote,
    string ScoutNote,
    string RiskNote,
    string Recommendation,
    DateOnly CreatedOn)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EvaluationId))
        {
            throw new ArgumentException("Training camp evaluation id is required.", nameof(EvaluationId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Training camp evaluation person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            throw new ArgumentException("Training camp evaluation player name is required.", nameof(PlayerName));
        }

        if (CampScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(CampScore), "Camp score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Readiness)
            || string.IsNullOrWhiteSpace(DevelopmentUpside)
            || string.IsNullOrWhiteSpace(CoachNote)
            || string.IsNullOrWhiteSpace(ScoutNote)
            || string.IsNullOrWhiteSpace(RiskNote)
            || string.IsNullOrWhiteSpace(Recommendation))
        {
            throw new ArgumentException("Training camp evaluation notes are required.");
        }
    }
}
