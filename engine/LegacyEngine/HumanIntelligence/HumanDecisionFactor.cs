namespace LegacyEngine.HumanIntelligence;

public sealed record HumanDecisionFactor(
    HumanDecisionFactorType FactorType,
    int Score,
    HumanDecisionWeight Weight,
    string Description)
{
    public void Validate()
    {
        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Decision factor score must be between 0 and 100.");
        }

        Weight.Validate();

        if (Weight.FactorType != FactorType)
        {
            throw new ArgumentException("Decision factor weight type must match factor type.", nameof(Weight));
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Decision factor description is required.", nameof(Description));
        }
    }
}
