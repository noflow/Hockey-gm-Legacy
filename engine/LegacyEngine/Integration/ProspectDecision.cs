namespace LegacyEngine.Integration;

public sealed record ProspectDecision(
    string ProspectPersonId,
    ProspectDecisionType DecisionType,
    DateOnly DecisionDate)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId))
        {
            throw new ArgumentException("Prospect person id is required.", nameof(ProspectPersonId));
        }
    }
}
