namespace LegacyEngine.Integration;

public sealed record SalaryCapSnapshot(
    SalaryCapProfile Profile,
    SalaryCapSpace Space,
    decimal CurrentCapHit,
    decimal AvailableCapSpace,
    decimal CommittedFutureSalary,
    decimal ExpiringSalary,
    decimal DeadCapPlaceholder,
    int ContractCount,
    IReadOnlyList<SalaryCapContractCommitment> ContractCommitments,
    SalaryCapStatus Status,
    IReadOnlyList<string> Warnings)
{
    public decimal CapUsed => Space.CapUsed;

    public decimal CapRemaining => Space.CapRemaining;

    public decimal CapPercentage => Space.CapPercentage;

    public bool IsEnabled => Profile.IsEnabled;

    public void Validate()
    {
        Profile.Validate();
        Space.Validate();
        if (CurrentCapHit < 0 || AvailableCapSpace < 0 || CommittedFutureSalary < 0 || ExpiringSalary < 0 || DeadCapPlaceholder < 0 || ContractCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CurrentCapHit), "Salary cap snapshot values cannot be negative.");
        }

        foreach (var commitment in ContractCommitments)
        {
            commitment.Validate();
        }
    }
}
