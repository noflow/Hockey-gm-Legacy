namespace LegacyEngine.Integration;

public sealed record OwnerDecision(
    OwnerDecisionType DecisionType,
    string Reason,
    string Impact,
    bool RequiresGmAttention)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(Impact))
        {
            throw new ArgumentException("Owner decision requires reason and impact.");
        }
    }
}
