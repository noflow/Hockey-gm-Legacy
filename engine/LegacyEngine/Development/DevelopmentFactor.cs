namespace LegacyEngine.Development;

public sealed record DevelopmentFactor(
    int Age,
    DateOnly UpdateDate,
    int? IceTimeScore = null,
    int? FacilityBonus = null,
    int? CoachingBonus = null,
    int? InjuryPenalty = null,
    int RandomModifier = 0)
{
    public void Validate()
    {
        if (Age <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Age must be positive.");
        }

        ValidateOptional(IceTimeScore, nameof(IceTimeScore));
        ValidateOptional(FacilityBonus, nameof(FacilityBonus));
        ValidateOptional(CoachingBonus, nameof(CoachingBonus));
        ValidateOptional(InjuryPenalty, nameof(InjuryPenalty));

        if (RandomModifier is < -20 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(RandomModifier), "Random modifier must be between -20 and 20.");
        }
    }

    private static void ValidateOptional(int? value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Development factor values must be between 0 and 100.");
        }
    }
}
