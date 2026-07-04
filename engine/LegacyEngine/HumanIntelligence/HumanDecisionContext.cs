namespace LegacyEngine.HumanIntelligence;

public sealed record HumanDecisionContext(
    string ContextId,
    string ActorPersonId,
    DateOnly DecisionDate,
    int Urgency,
    int Pressure,
    int Risk,
    int Reward,
    int Uncertainty,
    int OrganizationFit,
    int Trust = 50,
    int Respect = 50,
    int Confidence = 50,
    int Loyalty = 50)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ContextId))
        {
            throw new ArgumentException("Decision context id is required.", nameof(ContextId));
        }

        if (string.IsNullOrWhiteSpace(ActorPersonId))
        {
            throw new ArgumentException("Decision actor person id is required.", nameof(ActorPersonId));
        }

        ValidateScore(Urgency, nameof(Urgency));
        ValidateScore(Pressure, nameof(Pressure));
        ValidateScore(Risk, nameof(Risk));
        ValidateScore(Reward, nameof(Reward));
        ValidateScore(Uncertainty, nameof(Uncertainty));
        ValidateScore(OrganizationFit, nameof(OrganizationFit));
        ValidateScore(Trust, nameof(Trust));
        ValidateScore(Respect, nameof(Respect));
        ValidateScore(Confidence, nameof(Confidence));
        ValidateScore(Loyalty, nameof(Loyalty));
    }

    internal static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Decision scores must be between 0 and 100.");
        }
    }
}
