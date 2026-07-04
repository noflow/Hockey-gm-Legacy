namespace LegacyEngine.RuleEngine;

public sealed class DraftRuleValidator
{
    private readonly Rulebook _rulebook;

    public DraftRuleValidator(Rulebook rulebook)
    {
        _rulebook = rulebook;
    }

    public RuleValidationResult Validate(DraftValidationRequest request)
    {
        var rules = _rulebook.DraftRules;
        if (rules is null)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.MissingRulebookSection,
                "Rulebook is missing required section 'draft_rules'.",
                details: new Dictionary<string, object?> { ["section"] = "draft_rules" });
        }

        if (!rules.DraftEnabled)
        {
            return RuleValidationResult.Failure(RuleErrorCodes.DraftDisabled, "Draft is disabled for this league.");
        }

        if (!request.IsPlayerDraftEligible)
        {
            return RuleValidationResult.Failure(RuleErrorCodes.PlayerNotDraftEligible, "Player is not draft eligible.");
        }

        if (request.Round < 1 || request.Round > rules.Rounds)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.InvalidDraftRound,
                "Draft round is not valid for this league.",
                details: new Dictionary<string, object?> { ["round"] = request.Round, ["rounds"] = rules.Rounds });
        }

        return RuleValidationResult.Valid("Draft action is legal.");
    }
}
