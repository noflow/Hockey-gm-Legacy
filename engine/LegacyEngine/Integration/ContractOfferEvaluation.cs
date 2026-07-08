using LegacyEngine.Contracts;

namespace LegacyEngine.Integration;

public sealed record ContractOfferEvaluation(
    string EvaluationId,
    ContractAsk Ask,
    ContractOfferBuildRequest OfferRequest,
    ContractTerm Term,
    decimal TotalCost,
    decimal AnnualCost,
    decimal BudgetRemainingBefore,
    decimal BudgetRemainingAfter,
    string RiskWarning,
    int DecisionScore,
    ContractLikelihood Likelihood,
    ContractOfferDecision Decision,
    ContractDecisionExplanation Explanation,
    ContractComparison Comparison)
{
    public decimal CapHit { get; init; }

    public decimal CapRemainingBefore { get; init; }

    public decimal CapRemainingAfter { get; init; }

    public string CapWarning { get; init; } = "Salary cap not enabled for this rulebook.";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EvaluationId) || string.IsNullOrWhiteSpace(RiskWarning) || string.IsNullOrWhiteSpace(CapWarning))
        {
            throw new ArgumentException("Contract offer evaluation requires id and risk warning.");
        }

        Ask.Validate();
        OfferRequest.Validate();
        Term.Validate();
        Explanation.Validate();
        Comparison.Validate();
        if (TotalCost < 0 || AnnualCost < 0 || CapHit < 0 || CapRemainingBefore < 0 || CapRemainingAfter < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalCost), "Contract offer costs cannot be negative.");
        }

        if (DecisionScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(DecisionScore), "Contract decision score must be between 0 and 100.");
        }
    }
}
