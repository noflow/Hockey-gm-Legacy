namespace LegacyEngine.RuleEngine;

public static class RuleErrorCodes
{
    public const string Valid = "VALID";
    public const string MissingRulebookSection = "MISSING_RULEBOOK_SECTION";
    public const string InvalidRulebookField = "INVALID_RULEBOOK_FIELD";
    public const string RosterTooSmall = "ROSTER_TOO_SMALL";
    public const string RosterTooLarge = "ROSTER_TOO_LARGE";
    public const string ActiveRosterTooLarge = "ACTIVE_ROSTER_TOO_LARGE";
    public const string NotEnoughGoalies = "NOT_ENOUGH_GOALIES";
    public const string TooManyOveragePlayers = "TOO_MANY_OVERAGE_PLAYERS";
    public const string TooManyImportPlayers = "TOO_MANY_IMPORT_PLAYERS";
    public const string PlayerTooYoung = "PLAYER_TOO_YOUNG";
    public const string PlayerTooOld = "PLAYER_TOO_OLD";
    public const string ContractTypeNotAllowed = "CONTRACT_TYPE_NOT_ALLOWED";
    public const string ContractClauseNotAllowed = "CONTRACT_CLAUSE_NOT_ALLOWED";
    public const string SalaryCapExceeded = "SALARY_CAP_EXCEEDED";
    public const string BudgetExceeded = "BUDGET_EXCEEDED";
    public const string DraftDisabled = "DRAFT_DISABLED";
    public const string PlayerNotDraftEligible = "PLAYER_NOT_DRAFT_ELIGIBLE";
    public const string InvalidDraftRound = "INVALID_DRAFT_ROUND";
    public const string PlayoffTeamsInvalid = "PLAYOFF_TEAMS_INVALID";
    public const string UnknownRuleError = "UNKNOWN_RULE_ERROR";
}
