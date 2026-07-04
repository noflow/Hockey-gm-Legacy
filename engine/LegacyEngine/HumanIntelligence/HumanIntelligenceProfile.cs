namespace LegacyEngine.HumanIntelligence;

public sealed record HumanIntelligenceProfile(
    string PersonId,
    int Ambition,
    int Loyalty,
    int RiskTolerance,
    int PressureHandling,
    int Professionalism,
    int Communication)
{
    public static HumanIntelligenceProfile Balanced(string personId) =>
        new(personId, 50, 50, 50, 50, 50, 50);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Human intelligence profile person id is required.", nameof(PersonId));
        }

        HumanDecisionContext.ValidateScore(Ambition, nameof(Ambition));
        HumanDecisionContext.ValidateScore(Loyalty, nameof(Loyalty));
        HumanDecisionContext.ValidateScore(RiskTolerance, nameof(RiskTolerance));
        HumanDecisionContext.ValidateScore(PressureHandling, nameof(PressureHandling));
        HumanDecisionContext.ValidateScore(Professionalism, nameof(Professionalism));
        HumanDecisionContext.ValidateScore(Communication, nameof(Communication));
    }
}
