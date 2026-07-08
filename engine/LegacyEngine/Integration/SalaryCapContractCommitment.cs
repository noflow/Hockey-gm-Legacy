namespace LegacyEngine.Integration;

public sealed record SalaryCapContractCommitment(
    string ContractId,
    string PersonId,
    string PersonName,
    decimal CapHit,
    int YearsRemaining,
    DateOnly ExpiresOn,
    decimal TotalRemainingValue)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ContractId) || string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PersonName))
        {
            throw new ArgumentException("Salary cap commitment requires contract and person identity.");
        }

        if (CapHit < 0 || YearsRemaining < 0 || TotalRemainingValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CapHit), "Salary cap commitment values cannot be negative.");
        }
    }
}
