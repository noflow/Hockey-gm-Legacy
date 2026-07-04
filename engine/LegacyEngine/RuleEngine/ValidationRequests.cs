namespace LegacyEngine.RuleEngine;

public sealed record RosterValidationRequest(
    int TotalPlayers,
    int ActivePlayers,
    int Goalies,
    int OveragePlayers,
    int ImportPlayers);

public sealed record EligibilityValidationRequest(int Age);

public sealed record ContractValidationRequest(
    string ContractType,
    IReadOnlyCollection<string>? Clauses = null,
    decimal? TeamPayrollAfterSigning = null);

public sealed record DraftValidationRequest(
    int Round,
    bool IsPlayerDraftEligible = true);

public sealed record PlayoffValidationRequest(
    int TeamsQualify,
    int LeagueTeams);

public sealed record BudgetValidationRequest(
    decimal RequestedAmount,
    decimal AvailableBudget,
    decimal? TeamPayroll = null);
