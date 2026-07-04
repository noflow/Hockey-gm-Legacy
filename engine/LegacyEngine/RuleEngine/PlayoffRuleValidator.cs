namespace LegacyEngine.RuleEngine;

public sealed class PlayoffRuleValidator
{
    private readonly Rulebook _rulebook;

    public PlayoffRuleValidator(Rulebook rulebook)
    {
        _rulebook = rulebook;
    }

    public RuleValidationResult Validate(PlayoffValidationRequest request)
    {
        var rules = _rulebook.PlayoffRules;
        if (rules is null)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.MissingRulebookSection,
                "Rulebook is missing required section 'playoff_rules'.",
                details: new Dictionary<string, object?> { ["section"] = "playoff_rules" });
        }

        if (request.TeamsQualify != rules.TeamsQualify || request.TeamsQualify <= 0 || request.TeamsQualify > request.LeagueTeams)
        {
            return RuleValidationResult.Failure(
                RuleErrorCodes.PlayoffTeamsInvalid,
                "Playoff qualification count is invalid for this league.",
                details: new Dictionary<string, object?>
                {
                    ["teams_qualify"] = request.TeamsQualify,
                    ["rulebook_teams_qualify"] = rules.TeamsQualify,
                    ["league_teams"] = request.LeagueTeams
                });
        }

        return RuleValidationResult.Valid("Playoff format is legal.");
    }
}
