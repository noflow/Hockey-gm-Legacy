using System.Text.Json.Serialization;

namespace LegacyEngine.RuleEngine;

public sealed class Rulebook
{
    [JsonPropertyName("rulebook_id")]
    public string RulebookId { get; init; } = string.Empty;

    [JsonPropertyName("league_type")]
    public string LeagueType { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("roster_rules")]
    public RosterRules? RosterRules { get; init; }

    [JsonPropertyName("eligibility_rules")]
    public EligibilityRules? EligibilityRules { get; init; }

    [JsonPropertyName("contract_rules")]
    public ContractRules? ContractRules { get; init; }

    [JsonPropertyName("draft_rules")]
    public DraftRules? DraftRules { get; init; }

    [JsonPropertyName("playoff_rules")]
    public PlayoffRules? PlayoffRules { get; init; }

    [JsonPropertyName("budget_rules")]
    public BudgetRules? BudgetRules { get; init; }

    [JsonPropertyName("season_rules")]
    public SeasonRules? SeasonRules { get; init; }
}

public sealed class RosterRules
{
    [JsonPropertyName("min_roster")]
    public int MinRoster { get; init; }

    [JsonPropertyName("max_roster")]
    public int MaxRoster { get; init; }

    [JsonPropertyName("active_roster")]
    public int ActiveRoster { get; init; }

    [JsonPropertyName("goalies_required")]
    public int GoaliesRequired { get; init; }

    [JsonPropertyName("overage_slots")]
    public int OverageSlots { get; init; }

    [JsonPropertyName("import_slots")]
    public int ImportSlots { get; init; }

    [JsonPropertyName("injured_reserve_enabled")]
    public bool InjuredReserveEnabled { get; init; }

    [JsonPropertyName("reserve_list_enabled")]
    public bool ReserveListEnabled { get; init; }
}

public sealed class EligibilityRules
{
    [JsonPropertyName("min_age")]
    public int MinAge { get; init; }

    [JsonPropertyName("max_age")]
    public int MaxAge { get; init; }
}

public sealed class ContractRules
{
    [JsonPropertyName("allowed_contract_types")]
    public IReadOnlyList<string> AllowedContractTypes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("salary_cap_enabled")]
    public bool SalaryCapEnabled { get; init; }

    [JsonPropertyName("salary_cap_amount")]
    public decimal? SalaryCapAmount { get; init; }

    [JsonPropertyName("junior_stipends_enabled")]
    public bool JuniorStipendsEnabled { get; init; }

    [JsonPropertyName("education_packages_enabled")]
    public bool EducationPackagesEnabled { get; init; }

    [JsonPropertyName("housing_support_enabled")]
    public bool HousingSupportEnabled { get; init; }

    [JsonPropertyName("no_trade_clauses_enabled")]
    public bool NoTradeClausesEnabled { get; init; }

    [JsonPropertyName("no_move_clauses_enabled")]
    public bool NoMoveClausesEnabled { get; init; }

    [JsonPropertyName("arbitration_enabled")]
    public bool ArbitrationEnabled { get; init; }

    [JsonPropertyName("offer_sheets_enabled")]
    public bool OfferSheetsEnabled { get; init; }
}

public sealed class DraftRules
{
    [JsonPropertyName("draft_enabled")]
    public bool DraftEnabled { get; init; }

    [JsonPropertyName("rounds")]
    public int Rounds { get; init; }

    [JsonPropertyName("draft_order")]
    public string DraftOrder { get; init; } = string.Empty;
}

public sealed class PlayoffRules
{
    [JsonPropertyName("teams_qualify")]
    public int TeamsQualify { get; init; }

    [JsonPropertyName("series_format")]
    public IReadOnlyList<int> SeriesFormat { get; init; } = Array.Empty<int>();

    [JsonPropertyName("reseed_each_round")]
    public bool ReseedEachRound { get; init; }
}

public sealed class BudgetRules
{
    [JsonPropertyName("owner_budget_enabled")]
    public bool OwnerBudgetEnabled { get; init; }

    [JsonPropertyName("hard_salary_cap_enabled")]
    public bool HardSalaryCapEnabled { get; init; }

    [JsonPropertyName("hard_salary_cap_amount")]
    public decimal? HardSalaryCapAmount { get; init; }
}

// Optional season timing. When present, the Season engine derives its calendar from
// these values instead of its neutral defaults, so league dates live in rulebook
// data rather than in engine code. Offsets are whole days from the season start.
public sealed class SeasonRules
{
    [JsonPropertyName("season_start_month")]
    public int SeasonStartMonth { get; init; }

    [JsonPropertyName("season_start_day")]
    public int SeasonStartDay { get; init; }

    [JsonPropertyName("training_camp_offset_days")]
    public int TrainingCampOffsetDays { get; init; }

    [JsonPropertyName("season_begin_offset_days")]
    public int SeasonBeginOffsetDays { get; init; }

    [JsonPropertyName("trade_deadline_offset_days")]
    public int TradeDeadlineOffsetDays { get; init; }

    [JsonPropertyName("playoffs_begin_offset_days")]
    public int PlayoffsBeginOffsetDays { get; init; }

    [JsonPropertyName("championship_offset_days")]
    public int ChampionshipOffsetDays { get; init; }

    [JsonPropertyName("awards_offset_days")]
    public int AwardsOffsetDays { get; init; }

    [JsonPropertyName("recruiting_open_offset_days")]
    public int RecruitingOpenOffsetDays { get; init; }

    [JsonPropertyName("recruiting_close_offset_days")]
    public int RecruitingCloseOffsetDays { get; init; }

    [JsonPropertyName("draft_lottery_offset_days")]
    public int DraftLotteryOffsetDays { get; init; }

    [JsonPropertyName("draft_offset_days")]
    public int DraftOffsetDays { get; init; }

    [JsonPropertyName("free_agency_open_offset_days")]
    public int FreeAgencyOpenOffsetDays { get; init; }

    [JsonPropertyName("free_agency_close_offset_days")]
    public int FreeAgencyCloseOffsetDays { get; init; }
}
