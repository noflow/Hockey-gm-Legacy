namespace LegacyEngine.Integration;

public sealed record TradeStaffReaction(
    string HeadCoach,
    string Scout,
    string Owner,
    string AssistantGm,
    string PlayerReaction,
    IReadOnlyList<string> RelationshipImpacts)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HeadCoach)
            || string.IsNullOrWhiteSpace(Scout)
            || string.IsNullOrWhiteSpace(Owner)
            || string.IsNullOrWhiteSpace(AssistantGm)
            || string.IsNullOrWhiteSpace(PlayerReaction))
        {
            throw new ArgumentException("Trade reactions require readable staff and player notes.");
        }
    }
}
