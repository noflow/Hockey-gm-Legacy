namespace LegacyEngine.Integration;

public sealed record PlayerHealthProfile(
    string PersonId,
    string PlayerName,
    string Position,
    HealthStatus CurrentHealth,
    int Durability,
    int Fatigue,
    int RecoveryRate,
    int InjuryRisk,
    int WearAndTear,
    int PreviousInjuryCount,
    int RecurringInjuryRisk,
    int MedicalConfidence,
    ConditioningStatus Conditioning,
    string Summary,
    IReadOnlyList<string> PreviousInjuries,
    IReadOnlyList<string> RecurringConcerns)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Position)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player health profile requires identity and summary text.");
        }

        ValidateScore(Durability, nameof(Durability));
        ValidateScore(Fatigue, nameof(Fatigue));
        ValidateScore(RecoveryRate, nameof(RecoveryRate));
        ValidateScore(InjuryRisk, nameof(InjuryRisk));
        ValidateScore(WearAndTear, nameof(WearAndTear));
        ValidateScore(RecurringInjuryRisk, nameof(RecurringInjuryRisk));
        ValidateScore(MedicalConfidence, nameof(MedicalConfidence));
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Health profile scores must be between 0 and 100.");
        }
    }
}
