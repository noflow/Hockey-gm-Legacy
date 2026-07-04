namespace LegacyEngine.RuleEngine;

public sealed class EligibilityRuleValidator
{
    private readonly Rulebook _rulebook;

    public EligibilityRuleValidator(Rulebook rulebook)
    {
        _rulebook = rulebook;
    }

    public RuleValidationResult Validate(EligibilityValidationRequest request)
    {
        var rules = _rulebook.EligibilityRules;
        if (rules is null)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.MissingRulebookSection,
                "Rulebook is missing required section 'eligibility_rules'.",
                details: new Dictionary<string, object?> { ["section"] = "eligibility_rules" });
        }

        if (request.Age < rules.MinAge)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.PlayerTooYoung,
                "Player is too young for this league.",
                details: new Dictionary<string, object?> { ["age"] = request.Age, ["min_age"] = rules.MinAge });
        }

        if (request.Age > rules.MaxAge)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.PlayerTooOld,
                "Player is too old for this league.",
                details: new Dictionary<string, object?> { ["age"] = request.Age, ["max_age"] = rules.MaxAge });
        }

        return RuleValidationResult.Valid("Player is eligible.");
    }
}
