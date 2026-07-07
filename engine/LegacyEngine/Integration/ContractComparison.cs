namespace LegacyEngine.Integration;

public sealed record ContractComparison(
    decimal CurrentAnnualCost,
    decimal OfferAnnualCost,
    decimal AskAnnualCost,
    decimal BudgetRemainingBefore,
    decimal BudgetRemainingAfter,
    string CurrentContractSummary,
    string RoleRequested,
    string RoleOffered,
    int TermRequestedYears,
    int TermOfferedYears,
    string LikelyReaction)
{
    public decimal AnnualCostDifference => OfferAnnualCost - CurrentAnnualCost;

    public void Validate()
    {
        if (CurrentAnnualCost < 0 || OfferAnnualCost < 0 || AskAnnualCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OfferAnnualCost), "Contract comparison costs cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(CurrentContractSummary)
            || string.IsNullOrWhiteSpace(RoleRequested)
            || string.IsNullOrWhiteSpace(RoleOffered)
            || string.IsNullOrWhiteSpace(LikelyReaction))
        {
            throw new ArgumentException("Contract comparison requires summaries and reaction.");
        }

        if (TermRequestedYears <= 0 || TermOfferedYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TermOfferedYears), "Contract comparison terms must be positive.");
        }
    }
}
