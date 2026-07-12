namespace LegacyEngine.RuleEngine;

public sealed class RosterRuleValidator
{
    private readonly Rulebook _rulebook;

    public RosterRuleValidator(Rulebook rulebook)
    {
        _rulebook = rulebook;
    }

    public RuleValidationResult Validate(RosterValidationRequest request)
    {
        var rules = _rulebook.RosterRules;
        if (rules is null)
        {
            return MissingSection("roster_rules");
        }

        if (request.TotalPlayers < rules.MinRoster)
        {
            return Failure(RuleErrorCodes.RosterTooSmall, "Roster does not meet the league minimum.", "total_players", request.TotalPlayers, "min_roster", rules.MinRoster);
        }

        if (request.TotalPlayers > rules.MaxRoster)
        {
            return Failure(RuleErrorCodes.RosterTooLarge, "Roster exceeds the league maximum.", "total_players", request.TotalPlayers, "max_roster", rules.MaxRoster);
        }

        if (request.ActivePlayers > rules.ActiveRoster)
        {
            return Failure(RuleErrorCodes.ActiveRosterTooLarge, "Active roster exceeds the league maximum.", "active_players", request.ActivePlayers, "active_roster", rules.ActiveRoster);
        }

        if (request.Goalies < rules.GoaliesRequired)
        {
            return Failure(RuleErrorCodes.NotEnoughGoalies, "Roster does not have enough goalies.", "goalies", request.Goalies, "goalies_required", rules.GoaliesRequired);
        }

        // A zero value means the rulebook does not apply an age-based overage
        // restriction. This is how NHL/AHL-style presets distinguish themselves
        // from junior rosters while preserving explicit limits in junior/custom data.
        if (!IsProfessionalLeague(_rulebook.LeagueType)
            && rules.OverageSlots > 0
            && request.OveragePlayers > rules.OverageSlots)
        {
            return Failure(RuleErrorCodes.TooManyOveragePlayers, "Roster has too many overage players.", "overage_players", request.OveragePlayers, "overage_slots", rules.OverageSlots);
        }

        if (request.ImportPlayers > rules.ImportSlots)
        {
            return Failure(RuleErrorCodes.TooManyImportPlayers, "Roster has too many import players.", "import_players", request.ImportPlayers, "import_slots", rules.ImportSlots);
        }

        return RuleValidationResult.Valid("Roster is legal.");
    }

    private static RuleValidationResult MissingSection(string section) =>
        RuleValidationResult.Failure(
            RuleErrorCodes.MissingRulebookSection,
            $"Rulebook is missing required section '{section}'.",
            details: new Dictionary<string, object?> { ["section"] = section });

    private static RuleValidationResult Failure(string code, string message, string keyA, object valueA, string keyB, object valueB) =>
        RuleValidationResult.Failure(code, message, details: new Dictionary<string, object?> { [keyA] = valueA, [keyB] = valueB });

    private static bool IsProfessionalLeague(string leagueType) =>
        leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase)
        || leagueType.Contains("ahl", StringComparison.OrdinalIgnoreCase);
}
