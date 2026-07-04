namespace LegacyEngine.Injuries;

public sealed record InjuryRiskProfile(
    string PersonId,
    int BaseRisk,
    int RecurrenceRisk,
    int LongTermImpact)
{
    public int DevelopmentPenalty => Math.Clamp((RecurrenceRisk / 3) + (LongTermImpact / 2), 0, 100);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        ValidateScore(BaseRisk, nameof(BaseRisk));
        ValidateScore(RecurrenceRisk, nameof(RecurrenceRisk));
        ValidateScore(LongTermImpact, nameof(LongTermImpact));
    }

    internal static int ClampScore(int value) => Math.Clamp(value, 0, 100);

    internal static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Injury risk values must be between 0 and 100.");
        }
    }
}
