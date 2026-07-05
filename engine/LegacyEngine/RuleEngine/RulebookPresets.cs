namespace LegacyEngine.RuleEngine;

public static class RulebookPresets
{
    public static Rulebook Create(DraftLeaguePreset preset, int? customRounds = null) =>
        preset switch
        {
            DraftLeaguePreset.JuniorMajor => CreateJuniorMajor(customRounds),
            DraftLeaguePreset.NhlStyle => CreateNhlStyle(),
            DraftLeaguePreset.AhlStyle => CreateAhlStyle(),
            DraftLeaguePreset.Custom => CreateCustom(customRounds ?? 15),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported draft league preset.")
        };

    public static Rulebook CreateJuniorMajor(int? rounds = null)
    {
        var draftRounds = rounds ?? 15;
        if (draftRounds is < 8 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(rounds), "Junior Major drafts must be configurable from 8 to 15 rounds.");
        }

        return BaseRulebook("junior_major_default", "junior", true, draftRounds);
    }

    public static Rulebook CreateNhlStyle() =>
        BaseRulebook("nhl_style_default", "nhl_style", true, 7);

    public static Rulebook CreateAhlStyle() =>
        BaseRulebook("ahl_style_default", "ahl_style", false, 0, CreateAhlAffiliateRules());

    private static Rulebook CreateCustom(int rounds)
    {
        var rulebook = CreateJuniorMajor(rounds);
        return BaseRulebook($"custom_draft_{rounds}", "custom", true, rulebook.DraftRules!.Rounds);
    }

    private static Rulebook BaseRulebook(
        string rulebookId,
        string leagueType,
        bool draftEnabled,
        int rounds,
        AffiliateRules? affiliateRules = null) =>
        new()
        {
            RulebookId = rulebookId,
            LeagueType = leagueType,
            Version = "1.0",
            RosterRules = new RosterRules
            {
                MinRoster = 18,
                MaxRoster = 25,
                ActiveRoster = 20,
                GoaliesRequired = 2,
                OverageSlots = 3,
                ImportSlots = 2,
                InjuredReserveEnabled = true,
                ReserveListEnabled = true
            },
            EligibilityRules = new EligibilityRules { MinAge = 15, MaxAge = 20 },
            ContractRules = new ContractRules
            {
                AllowedContractTypes = new[]
                {
                    "junior_player_agreement",
                    "staff_contract",
                    "gm_contract",
                    "scout_contract",
                    "coach_contract"
                },
                SalaryCapEnabled = false,
                JuniorStipendsEnabled = true,
                EducationPackagesEnabled = true,
                HousingSupportEnabled = true,
                NoTradeClausesEnabled = false,
                NoMoveClausesEnabled = false,
                ArbitrationEnabled = false,
                OfferSheetsEnabled = false
            },
            DraftRules = new DraftRules
            {
                DraftEnabled = draftEnabled,
                Rounds = rounds,
                DraftOrder = draftEnabled ? "reverse_standings" : "disabled"
            },
            PlayoffRules = new PlayoffRules
            {
                TeamsQualify = 8,
                SeriesFormat = new[] { 7, 7, 7 },
                ReseedEachRound = true
            },
            BudgetRules = new BudgetRules
            {
                OwnerBudgetEnabled = true,
                HardSalaryCapEnabled = false
            },
            SeasonRules = new SeasonRules
            {
                SeasonStartMonth = 9,
                SeasonStartDay = 1,
                TrainingCampOffsetDays = 0,
                SeasonBeginOffsetDays = 21,
                TradeDeadlineOffsetDays = 140,
                PlayoffsBeginOffsetDays = 210,
                ChampionshipOffsetDays = 250,
                AwardsOffsetDays = 258,
                RecruitingOpenOffsetDays = 265,
                RecruitingCloseOffsetDays = 290,
                DraftLotteryOffsetDays = 292,
                DraftOffsetDays = 300,
                FreeAgencyOpenOffsetDays = 310,
                FreeAgencyCloseOffsetDays = 330
            },
            AffiliateRules = affiliateRules
        };

    private static AffiliateRules CreateAhlAffiliateRules() =>
        new()
        {
            AffiliateEnabled = true,
            ReceivesNonNhlReadyDraftedProspects = true,
            AllowedAcquisitionSources = new[]
            {
                "AssignedFromParentClub",
                "LoanedFromParentClub",
                "TwoWayContract",
                "AhlContract",
                "Tryout",
                "FreeAgentSigning"
            },
            GmResponsibilities = new[]
            {
                "Development",
                "IceTime",
                "RosterBalance",
                "CallUpsSendDowns",
                "Morale",
                "VeteranLeadership"
            }
        };
}
