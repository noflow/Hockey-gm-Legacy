namespace LegacyEngine.Integration;

public sealed record HockeyOperationsBudget(
    decimal TotalBudget,
    decimal GmSalary,
    decimal CoachingSalaries,
    decimal ScoutingSalaries,
    decimal MedicalTrainingSalaries,
    decimal StaffTotal,
    decimal StaffReleaseObligations,
    decimal PlayerContractTotal,
    decimal UsedBudget,
    decimal RemainingBudget,
    BudgetStatus Status,
    IReadOnlyList<string> Warnings)
{
    public decimal OverUnderBudget => RemainingBudget;

    public void Validate()
    {
        if (TotalBudget < 0 || GmSalary < 0 || CoachingSalaries < 0 || ScoutingSalaries < 0 || MedicalTrainingSalaries < 0 || StaffTotal < 0 || StaffReleaseObligations < 0 || PlayerContractTotal < 0 || UsedBudget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalBudget), "Hockey operations budget values cannot be negative.");
        }
    }
}
