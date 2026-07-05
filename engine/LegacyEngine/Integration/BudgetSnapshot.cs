namespace LegacyEngine.Integration;

public sealed record BudgetSnapshot(
    decimal TotalBudget,
    decimal UsedBudget,
    decimal RemainingBudget,
    decimal PlayerContractsTotal,
    decimal StaffContractsTotal,
    decimal ScoutingBudget,
    decimal MedicalAndStaffOperationsBudget,
    BudgetStatus Status,
    string OwnerBudgetConfidence)
{
    public decimal OverUnderBudget => RemainingBudget;

    public void Validate()
    {
        if (TotalBudget < 0 || UsedBudget < 0 || PlayerContractsTotal < 0 || StaffContractsTotal < 0 || ScoutingBudget < 0 || MedicalAndStaffOperationsBudget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalBudget), "Budget values cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(OwnerBudgetConfidence))
        {
            throw new ArgumentException("Owner budget confidence text is required.", nameof(OwnerBudgetConfidence));
        }
    }
}
