namespace LegacyEngine.Contracts;

public sealed record ContractMoney(
    decimal SalaryOrStipend,
    decimal SigningBonus = 0,
    string Currency = "USD")
{
    public void Validate()
    {
        if (SalaryOrStipend < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SalaryOrStipend), "Salary or stipend cannot be negative.");
        }

        if (SigningBonus < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SigningBonus), "Signing bonus cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new ArgumentException("Currency is required.", nameof(Currency));
        }
    }
}
