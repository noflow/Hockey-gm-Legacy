namespace LegacyEngine.HumanIntelligence;

public sealed record HumanDecisionReason(
    string OptionId,
    HumanDecisionFactorType FactorType,
    decimal Contribution,
    string Text)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OptionId))
        {
            throw new ArgumentException("Decision reason option id is required.", nameof(OptionId));
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            throw new ArgumentException("Decision reason text is required.", nameof(Text));
        }
    }
}
