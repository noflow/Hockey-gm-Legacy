using LegacyEngine.Contracts;

namespace LegacyEngine.Integration;

public sealed record FreeAgentContractAsk(
    ContractType ContractType,
    decimal AnnualAmount,
    string Currency,
    int TermYears,
    string Notes)
{
    public void Validate()
    {
        if (AnnualAmount < 0 || TermYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AnnualAmount), "Free agent contract ask must have a non-negative amount and positive term.");
        }

        if (string.IsNullOrWhiteSpace(Currency) || string.IsNullOrWhiteSpace(Notes))
        {
            throw new ArgumentException("Free agent contract ask requires currency and notes.");
        }
    }
}
