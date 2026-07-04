namespace LegacyEngine.HumanIntelligence;

public sealed record HumanDecisionWeight(
    HumanDecisionFactorType FactorType,
    decimal Value)
{
    public void Validate()
    {
        if (Value <= 0 || Value > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(Value), "Decision factor weight must be greater than 0 and no more than 10.");
        }
    }
}
