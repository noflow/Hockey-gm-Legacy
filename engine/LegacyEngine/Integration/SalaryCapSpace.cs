namespace LegacyEngine.Integration;

public sealed record SalaryCapSpace(
    decimal CapAmount,
    decimal CapUsed,
    decimal CapRemaining,
    decimal SalaryFloor,
    decimal FloorRemaining,
    decimal CapPercentage)
{
    public void Validate()
    {
        if (CapAmount < 0 || CapUsed < 0 || SalaryFloor < 0 || FloorRemaining < 0 || CapPercentage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CapAmount), "Salary cap space values cannot be negative.");
        }
    }
}
