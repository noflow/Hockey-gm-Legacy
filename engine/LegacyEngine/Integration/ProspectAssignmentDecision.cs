namespace LegacyEngine.Integration;

public sealed record ProspectAssignmentDecision(
    string ProspectPersonId,
    ProspectDecisionType DecisionType,
    DateOnly DecisionDate,
    string Notes = "")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId))
        {
            throw new ArgumentException("Prospect assignment decision requires a prospect person id.", nameof(ProspectPersonId));
        }
    }
}
