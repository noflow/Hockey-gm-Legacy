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
        BaseRulebook("nhl_style_default", "nhl_style", true, 7, activeRoster: 23, maxRoster: 23);

    public static Rulebook CreateAhlStyle() =>
        BaseRulebook("ahl_style_default", "ahl_style", false, 0, CreateAhlAffiliateRules(), activeRoster: 23, maxRoster: 28);

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
        AffiliateRules? affiliateRules = null,
        int activeRoster = 26,
        int maxRoster = 26) =>
        new()
        {
            RulebookId = rulebookId,
            LeagueType = leagueType,
            Version = "1.0",
            RosterRules = new RosterRules
            {
                MinRoster = activeRoster,
                MaxRoster = maxRoster,
                ActiveRoster = activeRoster,
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
                SalaryCapEnabled = IsProfessionalLeague(leagueType),
                SalaryCapAmount = SalaryCapAmountFor(leagueType),
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
                HardSalaryCapEnabled = IsProfessionalLeague(leagueType),
                HardSalaryCapAmount = SalaryCapAmountFor(leagueType)
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
            StaffRules = CreateJuniorStaffRules(),
            AffiliateRules = affiliateRules,
            PlayerAssignmentRules = CreatePlayerAssignmentRules(leagueType),
            SalaryCapRules = CreateSalaryCapRules(leagueType, activeRoster),
            WaiverRules = CreateWaiverRules(leagueType),
            FreeAgentRightsRules = CreateFreeAgentRightsRules(leagueType)
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

    private static StaffRules CreateJuniorStaffRules() =>
        new()
        {
            PositionLimits = new[]
            {
                Limit("GeneralManager", "Executive", 1, 1),
                Limit("AssistantGM", "Executive", 1, 1),
                Limit("HeadCoach", "Coaching", 1, 1),
                Limit("AssistantCoach", "Coaching", 2, 2),
                Limit("DevelopmentCoach", "Coaching", 1, 1),
                Limit("HeadScout", "Scouting", 1, 1),
                Limit("Scout", "Scouting", 3, 3),
                Limit("HeadAthleticTherapist", "Medical", 1, 1),
                Limit("TeamDoctor", "Medical", 1, 1),
                Limit("HeadEquipmentManager", "Equipment", 1, 1)
            }
        };

    private static StaffPositionLimit Limit(string role, string department, int minimum, int maximum) =>
        new()
        {
            Role = role,
            Department = department,
            Minimum = minimum,
            Maximum = maximum
        };

    private static PlayerAssignmentRules CreatePlayerAssignmentRules(string leagueType) =>
        new()
        {
            JuniorAgeCutoff = 19,
            AhlEligibilityAge = 20,
            ChlToAhlRestrictionEnabled = leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase),
            OneNineteenYearOldChlExceptionEnabled = false,
            EuropeanAndCollegeProspectsCanPlayAhlAt18 = true,
            ElcSlideAgeCutoff = 19,
            ElcSlideNhlGameThreshold = 10
        };

    private static SalaryCapRules CreateSalaryCapRules(string leagueType, int activeRoster) =>
        new()
        {
            SalaryCapEnabled = IsProfessionalLeague(leagueType),
            CapAmount = SalaryCapAmountFor(leagueType),
            SalaryFloor = SalaryFloorFor(leagueType),
            MaximumRosterSize = activeRoster,
            MaximumContracts = IsProfessionalLeague(leagueType) ? 50 : null,
            MaximumRetainedSalaryPlaceholder = 0m,
            OffseasonCapRulesPlaceholder = IsProfessionalLeague(leagueType)
                ? "Offseason cap cushion is a placeholder in Alpha 5.6; hard cap validation is used for explicit moves."
                : "Junior leagues use operating budgets instead of a salary cap."
        };

    private static WaiverRules CreateWaiverRules(string leagueType) =>
        new()
        {
            WaiversEnabled = IsProfessionalLeague(leagueType),
            ClaimWindowHours = IsProfessionalLeague(leagueType) ? 24 : 0,
            WaiverOrder = IsProfessionalLeague(leagueType) ? "reverse_standings" : "disabled",
            ExemptAgeCutoff = 21,
            ExemptProfessionalSeasons = 3,
            ExemptGamesPlayed = 80,
            AllowCancelBeforeClaimWindow = true
        };

    private static FreeAgentRightsRules CreateFreeAgentRightsRules(string leagueType) =>
        new()
        {
            RfaUfaSystemEnabled = IsProfessionalLeague(leagueType),
            UfaAge = leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase) ? 27 : 26,
            UfaAccruedSeasonsThreshold = leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase) ? 7 : 6,
            QualifyingOfferRequired = IsProfessionalLeague(leagueType),
            QualifyingOfferDeadlineDaysAfterExpiry = IsProfessionalLeague(leagueType) ? 7 : 0,
            QualifyingOfferSalaryMultiplier = 1.05m,
            MinimumQualifyingOffer = leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase) ? 775_000m : 80_000m,
            RightsExpiryRule = IsProfessionalLeague(leagueType) ? "qualify_by_deadline_or_release" : "disabled",
            ContractTenderWindowDays = IsProfessionalLeague(leagueType) ? 30 : 0
        };

    private static bool IsProfessionalLeague(string leagueType) =>
        leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase)
        || leagueType.Contains("ahl", StringComparison.OrdinalIgnoreCase);

    private static decimal? SalaryCapAmountFor(string leagueType)
    {
        if (leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase))
        {
            return 88_000_000m;
        }

        if (leagueType.Contains("ahl", StringComparison.OrdinalIgnoreCase))
        {
            return 6_000_000m;
        }

        return null;
    }

    private static decimal? SalaryFloorFor(string leagueType)
    {
        if (leagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase))
        {
            return 65_000_000m;
        }

        if (leagueType.Contains("ahl", StringComparison.OrdinalIgnoreCase))
        {
            return 2_000_000m;
        }

        return null;
    }
}
