namespace LegacyEngine.HumanIntelligence;

public sealed record HumanDecisionOption(
    string OptionId,
    string Title,
    string Description,
    IReadOnlyList<HumanDecisionFactor> Factors)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OptionId))
        {
            throw new ArgumentException("Decision option id is required.", nameof(OptionId));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Decision option title is required.", nameof(Title));
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Decision option description is required.", nameof(Description));
        }

        if (Factors.Count == 0)
        {
            throw new ArgumentException("Decision option must include at least one factor.", nameof(Factors));
        }

        foreach (var factor in Factors)
        {
            factor.Validate();
        }
    }
}
