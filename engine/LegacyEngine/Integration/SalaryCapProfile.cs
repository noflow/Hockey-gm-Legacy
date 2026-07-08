namespace LegacyEngine.Integration;

public sealed record SalaryCapProfile(
    bool IsEnabled,
    decimal CapAmount,
    decimal SalaryFloor,
    int MaximumRosterSize,
    int MaximumContracts,
    decimal MaximumRetainedSalaryPlaceholder,
    string OffseasonCapRulesPlaceholder)
{
    public void Validate()
    {
        if (CapAmount < 0 || SalaryFloor < 0 || MaximumRosterSize < 0 || MaximumContracts < 0 || MaximumRetainedSalaryPlaceholder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CapAmount), "Salary cap profile values cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(OffseasonCapRulesPlaceholder))
        {
            throw new ArgumentException("Offseason cap placeholder text is required.", nameof(OffseasonCapRulesPlaceholder));
        }
    }
}
