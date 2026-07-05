namespace LegacyEngine.Integration;

public sealed record TrainingCamp(
    string CampId,
    string OrganizationId,
    DateOnly OpenedOn,
    IReadOnlyList<TrainingCampPlayer> Players,
    IReadOnlyList<TrainingCampEvaluation> Evaluations,
    TrainingCampSummary? Summary = null,
    DateOnly? CompletedOn = null)
{
    public bool IsCompleted => CompletedOn is not null;

    public TrainingCampPlayer? FindPlayer(string personId) =>
        Players.SingleOrDefault(player => player.PersonId == personId);

    public TrainingCampEvaluation? FindEvaluation(string personId) =>
        Evaluations.SingleOrDefault(evaluation => evaluation.PersonId == personId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CampId))
        {
            throw new ArgumentException("Training camp id is required.", nameof(CampId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Training camp organization id is required.", nameof(OrganizationId));
        }

        foreach (var player in Players)
        {
            player.Validate();
        }

        foreach (var evaluation in Evaluations)
        {
            evaluation.Validate();
        }

        if (Players.Select(player => player.PersonId).Distinct(StringComparer.Ordinal).Count() != Players.Count)
        {
            throw new ArgumentException("Training camp cannot contain duplicate players.", nameof(Players));
        }

        Summary?.Validate();
    }
}
