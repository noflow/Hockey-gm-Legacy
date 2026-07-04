namespace LegacyEngine.RuleEngine;

public sealed class ContractRuleValidator
{
    private static readonly IReadOnlyDictionary<string, Func<ContractRules, bool>> ClauseRules =
        new Dictionary<string, Func<ContractRules, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["junior_stipend"] = rules => rules.JuniorStipendsEnabled,
            ["education_package"] = rules => rules.EducationPackagesEnabled,
            ["housing_support"] = rules => rules.HousingSupportEnabled,
            ["no_trade_clause"] = rules => rules.NoTradeClausesEnabled,
            ["no_move_clause"] = rules => rules.NoMoveClausesEnabled,
            ["arbitration"] = rules => rules.ArbitrationEnabled,
            ["offer_sheet"] = rules => rules.OfferSheetsEnabled
        };

    private readonly Rulebook _rulebook;

    public ContractRuleValidator(Rulebook rulebook)
    {
        _rulebook = rulebook;
    }

    public RuleValidationResult Validate(ContractValidationRequest request)
    {
        var rules = _rulebook.ContractRules;
        if (rules is null)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.MissingRulebookSection,
                "Rulebook is missing required section 'contract_rules'.",
                details: new Dictionary<string, object?> { ["section"] = "contract_rules" });
        }

        if (!rules.AllowedContractTypes.Contains(request.ContractType, StringComparer.OrdinalIgnoreCase))
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.ContractTypeNotAllowed,
                "Contract type is not allowed in this league.",
                details: new Dictionary<string, object?> { ["contract_type"] = request.ContractType });
        }

        foreach (var clause in request.Clauses ?? Array.Empty<string>())
        {
            if (!ClauseRules.TryGetValue(clause, out var isAllowed) || !isAllowed(rules))
            {
                return RuleValidationResult.Failure(
                    RuleErrorCodes.ContractClauseNotAllowed,
                    "Contract clause is not allowed in this league.",
                    details: new Dictionary<string, object?> { ["clause"] = clause });
            }
        }

        if (rules.SalaryCapEnabled && request.TeamPayrollAfterSigning > rules.SalaryCapAmount)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.SalaryCapExceeded,
                "Contract would exceed the league salary cap.",
                details: new Dictionary<string, object?>
                {
                    ["team_payroll_after_signing"] = request.TeamPayrollAfterSigning,
                    ["salary_cap_amount"] = rules.SalaryCapAmount
                });
        }

        return RuleValidationResult.Valid("Contract is legal.");
    }
}
