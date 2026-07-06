namespace LegacyEngine.Integration;

public sealed record StaffSalary(decimal AnnualAmount, string Currency = "CAD")
{
    public void Validate()
    {
        if (AnnualAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AnnualAmount), "Staff salary cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new ArgumentException("Staff salary currency is required.", nameof(Currency));
        }
    }
}
