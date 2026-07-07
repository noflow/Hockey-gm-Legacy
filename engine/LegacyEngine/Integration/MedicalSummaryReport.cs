namespace LegacyEngine.Integration;

public sealed record MedicalSummaryReport(
    DateOnly CreatedOn,
    int GamesLostToInjury,
    string MostSignificantInjury,
    int ActiveInjuries,
    int ReturningSoon,
    int HighRiskPlayers,
    int ConditioningAssignments,
    string MedicalDepartmentGrade,
    string MedicalBudgetImpact,
    IReadOnlyList<string> PlayerNotes)
{
    public void Validate()
    {
        if (GamesLostToInjury < 0 || ActiveInjuries < 0 || ReturningSoon < 0 || HighRiskPlayers < 0 || ConditioningAssignments < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(GamesLostToInjury), "Medical summary counts cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(MostSignificantInjury)
            || string.IsNullOrWhiteSpace(MedicalDepartmentGrade)
            || string.IsNullOrWhiteSpace(MedicalBudgetImpact))
        {
            throw new ArgumentException("Medical summary requires display text.");
        }
    }
}
