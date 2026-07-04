namespace LegacyEngine.RuleEngine;

public sealed class BudgetRuleValidator
{
    private readonly Rulebook _rulebook;

    public BudgetRuleValidator(Rulebook rulebook)
    {
        _rulebook = rulebook;
    }

    public RuleValidationResult Validate(BudgetValidationRequest request)
    {
        var rules = _rulebook.BudgetRules;
        if (rules is null)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.MissingRulebookSection,
                "Rulebook is missing required section 'budget_rules'.",
                details: new Dictionary<string, object?> { ["section"] = "budget_rules" });
        }

        if (rules.OwnerBudgetEnabled && request.RequestedAmount > request.AvailableBudget)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.BudgetExceeded,
                "Action exceeds available owner budget.",
                details: new Dictionary<string, object?>
                {
                    ["requested_amount"] = request.RequestedAmount,
                    ["available_budget"] = request.AvailableBudget
                });
        }

        if (rules.HardSalaryCapEnabled && request.TeamPayroll > rules.HardSalaryCapAmount)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.SalaryCapExceeded,
                "Team payroll exceeds the league hard salary cap.",
                details: new Dictionary<string, object?>
                {
                    ["team_payroll"] = request.TeamPayroll,
                    ["hard_salary_cap_amount"] = rules.HardSalaryCapAmount
                });
        }

        return RuleValidationResult.Valid("Budget action is legal.");
    }
}
