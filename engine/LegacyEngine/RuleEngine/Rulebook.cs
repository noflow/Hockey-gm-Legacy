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

    [JsonPropertyName("staff_rules")]
    public StaffRules? StaffRules { get; init; }

    [JsonPropertyName("affiliate_rules")]
    public AffiliateRules? AffiliateRules { get; init; }

    [JsonPropertyName("player_assignment_rules")]
    public PlayerAssignmentRules? PlayerAssignmentRules { get; init; }

    [JsonPropertyName("salary_cap_rules")]
    public SalaryCapRules? SalaryCapRules { get; init; }

    [JsonPropertyName("waiver_rules")]
    public WaiverRules? WaiverRules { get; init; }

    [JsonPropertyName("free_agent_rights_rules")]
    public FreeAgentRightsRules? FreeAgentRightsRules { get; init; }

    [JsonPropertyName("arbitration_rules")]
    public ArbitrationRules? ArbitrationRules { get; init; }

    [JsonPropertyName("buyout_rules")]
    public BuyoutRules? BuyoutRules { get; init; }
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

public sealed class StaffRules
{
    [JsonPropertyName("position_limits")]
    public IReadOnlyList<StaffPositionLimit> PositionLimits { get; init; } = Array.Empty<StaffPositionLimit>();
}

public sealed class StaffPositionLimit
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; init; } = string.Empty;

    [JsonPropertyName("minimum")]
    public int Minimum { get; init; }

    [JsonPropertyName("maximum")]
    public int Maximum { get; init; }
}

public sealed class AffiliateRules
{
    [JsonPropertyName("affiliate_enabled")]
    public bool AffiliateEnabled { get; init; }

    [JsonPropertyName("parent_organization_id")]
    public string? ParentOrganizationId { get; init; }

    [JsonPropertyName("affiliate_organization_id")]
    public string? AffiliateOrganizationId { get; init; }

    [JsonPropertyName("receives_non_nhl_ready_drafted_prospects")]
    public bool ReceivesNonNhlReadyDraftedProspects { get; init; }

    [JsonPropertyName("allowed_acquisition_sources")]
    public IReadOnlyList<string> AllowedAcquisitionSources { get; init; } = Array.Empty<string>();

    [JsonPropertyName("gm_responsibilities")]
    public IReadOnlyList<string> GmResponsibilities { get; init; } = Array.Empty<string>();
}

public sealed class PlayerAssignmentRules
{
    [JsonPropertyName("junior_age_cutoff")]
    public int JuniorAgeCutoff { get; init; } = 19;

    [JsonPropertyName("ahl_eligibility_age")]
    public int AhlEligibilityAge { get; init; } = 20;

    [JsonPropertyName("chl_to_ahl_restriction_enabled")]
    public bool ChlToAhlRestrictionEnabled { get; init; } = true;

    [JsonPropertyName("one_19_year_old_chl_exception_enabled")]
    public bool OneNineteenYearOldChlExceptionEnabled { get; init; }

    [JsonPropertyName("european_and_college_prospects_can_play_ahl_at_18")]
    public bool EuropeanAndCollegeProspectsCanPlayAhlAt18 { get; init; } = true;

    [JsonPropertyName("elc_slide_age_cutoff")]
    public int ElcSlideAgeCutoff { get; init; } = 19;

    [JsonPropertyName("elc_slide_nhl_game_threshold")]
    public int ElcSlideNhlGameThreshold { get; init; } = 10;
}

public sealed class SalaryCapRules
{
    [JsonPropertyName("salary_cap_enabled")]
    public bool SalaryCapEnabled { get; init; }

    [JsonPropertyName("cap_amount")]
    public decimal? CapAmount { get; init; }

    [JsonPropertyName("salary_floor")]
    public decimal? SalaryFloor { get; init; }

    [JsonPropertyName("maximum_roster_size")]
    public int? MaximumRosterSize { get; init; }

    [JsonPropertyName("maximum_contracts")]
    public int? MaximumContracts { get; init; }

    [JsonPropertyName("maximum_retained_salary_placeholder")]
    public decimal? MaximumRetainedSalaryPlaceholder { get; init; }

    [JsonPropertyName("offseason_cap_rules_placeholder")]
    public string? OffseasonCapRulesPlaceholder { get; init; }
}

public sealed class WaiverRules
{
    [JsonPropertyName("waivers_enabled")]
    public bool WaiversEnabled { get; init; }

    [JsonPropertyName("claim_window_hours")]
    public int ClaimWindowHours { get; init; } = 24;

    [JsonPropertyName("waiver_order")]
    public string WaiverOrder { get; init; } = "reverse_standings";

    [JsonPropertyName("exempt_age_cutoff")]
    public int ExemptAgeCutoff { get; init; } = 21;

    [JsonPropertyName("exempt_professional_seasons")]
    public int ExemptProfessionalSeasons { get; init; } = 3;

    [JsonPropertyName("exempt_games_played")]
    public int ExemptGamesPlayed { get; init; } = 80;

    [JsonPropertyName("allow_cancel_before_claim_window")]
    public bool AllowCancelBeforeClaimWindow { get; init; } = true;
}

public sealed class FreeAgentRightsRules
{
    [JsonPropertyName("rfa_ufa_system_enabled")]
    public bool RfaUfaSystemEnabled { get; init; }

    [JsonPropertyName("ufa_age")]
    public int UfaAge { get; init; } = 27;

    [JsonPropertyName("ufa_accrued_seasons_threshold")]
    public int UfaAccruedSeasonsThreshold { get; init; } = 7;

    [JsonPropertyName("qualifying_offer_required")]
    public bool QualifyingOfferRequired { get; init; } = true;

    [JsonPropertyName("qualifying_offer_deadline_days_after_expiry")]
    public int QualifyingOfferDeadlineDaysAfterExpiry { get; init; } = 7;

    [JsonPropertyName("qualifying_offer_salary_multiplier")]
    public decimal QualifyingOfferSalaryMultiplier { get; init; } = 1.05m;

    [JsonPropertyName("minimum_qualifying_offer")]
    public decimal MinimumQualifyingOffer { get; init; } = 775_000m;

    [JsonPropertyName("rights_expiry_rule")]
    public string RightsExpiryRule { get; init; } = "qualify_by_deadline_or_release";

    [JsonPropertyName("contract_tender_window_days")]
    public int ContractTenderWindowDays { get; init; } = 30;
}

public sealed class ArbitrationRules
{
    [JsonPropertyName("arbitration_enabled")]
    public bool ArbitrationEnabled { get; init; }

    [JsonPropertyName("eligibility_age")]
    public int EligibilityAge { get; init; } = 22;

    [JsonPropertyName("accrued_seasons_threshold")]
    public int AccruedSeasonsThreshold { get; init; } = 4;

    [JsonPropertyName("filing_window_days_after_qualifying_offer")]
    public int FilingWindowDaysAfterQualifyingOffer { get; init; } = 7;

    [JsonPropertyName("hearing_start_days_after_filing")]
    public int HearingStartDaysAfterFiling { get; init; } = 14;

    [JsonPropertyName("hearing_end_days_after_filing")]
    public int HearingEndDaysAfterFiling { get; init; } = 28;

    [JsonPropertyName("walk_away_allowed")]
    public bool WalkAwayAllowed { get; init; } = true;

    [JsonPropertyName("minimum_award")]
    public decimal MinimumAward { get; init; } = 775_000m;

    [JsonPropertyName("maximum_award")]
    public decimal MaximumAward { get; init; } = 8_000_000m;
}

public sealed class BuyoutRules
{
    [JsonPropertyName("buyouts_enabled")]
    public bool BuyoutsEnabled { get; init; }

    [JsonPropertyName("buyout_window_start_offset_days")]
    public int BuyoutWindowStartOffsetDays { get; init; }

    [JsonPropertyName("buyout_window_end_offset_days")]
    public int BuyoutWindowEndOffsetDays { get; init; }

    [JsonPropertyName("buyout_cost_percentage")]
    public decimal BuyoutCostPercentage { get; init; } = 0.6667m;

    [JsonPropertyName("penalty_years_multiplier")]
    public int PenaltyYearsMultiplier { get; init; } = 2;

    [JsonPropertyName("age_based_cost_rule_placeholder")]
    public string? AgeBasedCostRulePlaceholder { get; init; }

    [JsonPropertyName("cap_penalty_enabled")]
    public bool CapPenaltyEnabled { get; init; } = true;

    [JsonPropertyName("minimum_contract_remaining_years")]
    public int MinimumContractRemainingYears { get; init; } = 1;
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
