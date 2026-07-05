namespace LegacyEngine.Integration;

public sealed record TrainingCampDecision(
    string PersonId,
    TrainingCampDecisionType DecisionType,
    DateOnly DecisionDate,
    string Reason = "")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Training camp decision person id is required.", nameof(PersonId));
        }
    }
}
