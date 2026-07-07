using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class LeagueAiService
{
    private readonly TradeStrategyService _tradeStrategy = new();

    public LeagueAiReport BuildReport(NewGmScenarioSnapshot scenario, BudgetSnapshot? budget = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        budget ??= new BudgetOverviewService().Build(scenario, RuleEngine.RulebookPresets.CreateJuniorMajor());
        var profiles = SeasonFrameworkService.LeagueTeams(scenario)
            .Select(team => BuildOrganizationProfile(scenario, team.OrganizationId, team.TeamName, budget))
            .ToArray();
        var news = BuildLeagueNews(scenario, profiles);
        var history = BuildHistoryNotes(scenario, profiles);
        var report = new LeagueAiReport(profiles, news, history);
        report.Validate();
        return report;
    }

    public OrganizationLeagueProfile BuildOrganizationProfile(NewGmScenarioSnapshot scenario, string organizationId, string teamName, BudgetSnapshot? budget = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(teamName))
        {
            throw new ArgumentException("Organization identity is required.");
        }

        budget ??= new BudgetOverviewService().Build(scenario, RuleEngine.RulebookPresets.CreateJuniorMajor());
        var needs = _tradeStrategy.BuildTeamNeedsProfile(scenario, organizationId, teamName);
        var identity = IdentityFor(scenario, organizationId, teamName, needs);
        var gm = GmFor(scenario, organizationId, teamName, identity, needs.Direction);
        var owner = OwnerInfluenceFor(scenario, organizationId, identity, needs.Direction, budget);
        var strategy = StrategyFor(identity, needs.Direction, owner);
        var draft = DraftStyleFor(identity, gm, strategy);
        var scouting = ScoutingFor(identity, draft, budget, organizationId == scenario.Organization.OrganizationId);
        var behavior = BehaviorFor(identity, gm, owner, strategy, needs, budget, organizationId == scenario.Organization.OrganizationId);
        var currentNeeds = MonthlyNeeds(scenario, organizationId, needs, identity, budget);
        var budgetStyle = BudgetStyleFor(identity, owner, budget, organizationId == scenario.Organization.OrganizationId);
        var developmentGrade = DevelopmentGradeFor(scenario, organizationId, identity);
        var direction = RecentDirectionFor(teamName, identity, strategy, needs);
        var summary = $"{teamName}: {Readable(identity)} with a {Readable(gm)} GM. Strategy: {Readable(strategy)}. Current needs: {string.Join(", ", currentNeeds.Take(3).Select(need => Readable(need.Need)))}.";
        var profile = new OrganizationLeagueProfile(
            organizationId,
            teamName,
            identity,
            gm,
            owner,
            strategy,
            currentNeeds,
            budgetStyle,
            draft,
            scouting,
            developmentGrade,
            needs,
            behavior,
            direction,
            summary);
        profile.Validate();
        return profile;
    }

    public NewGmScenarioSnapshot RecordIdentityHistory(NewGmScenarioSnapshot scenario, OrganizationLeagueProfile profile)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        var identityEntry = new CareerTimelineEntry(
            $"career:organization-identity:{profile.OrganizationId}:{scenario.Season.Year}",
            CareerTimelineEntryType.OrganizationIdentityChanged,
            scenario.CurrentDate,
            scenario.Season.Year,
            null,
            profile.OrganizationId,
            profile.TeamName,
            $"{profile.TeamName} identity: {Readable(profile.Identity)}",
            profile.Summary,
            null,
            profile.OrganizationId == scenario.Organization.OrganizationId ? HistoryImportance.Important : HistoryImportance.Normal);
        var strategyEntry = new CareerTimelineEntry(
            $"career:organization-strategy:{profile.OrganizationId}:{scenario.Season.Year}",
            CareerTimelineEntryType.OrganizationStrategyChanged,
            scenario.CurrentDate,
            scenario.Season.Year,
            null,
            profile.OrganizationId,
            profile.TeamName,
            $"{profile.TeamName} strategy: {Readable(profile.CurrentStrategy)}",
            profile.RecentDirection,
            null,
            HistoryImportance.Normal);
        return scenario with { CareerTimeline = scenario.CareerTimeline.Add(identityEntry).Add(strategyEntry) };
    }

    private static IReadOnlyList<TeamNeed> MonthlyNeeds(NewGmScenarioSnapshot scenario, string organizationId, TeamNeedsProfile needs, LeagueTeamIdentity identity, BudgetSnapshot budget)
    {
        var list = needs.Needs.ToList();
        if (identity == LeagueTeamIdentity.GoaltendingOrganization && list.All(item => item.Need != PositionNeed.StartingGoalie))
        {
            list.Add(new TeamNeed(PositionNeed.StartingGoalie, TradePriority.Medium, "Organization identity places extra value on goaltending depth."));
        }

        if (identity is LeagueTeamIdentity.DevelopmentOrganization or LeagueTeamIdentity.RebuildingOrganization && list.All(item => item.Need != PositionNeed.ProspectDepth))
        {
            list.Add(new TeamNeed(PositionNeed.ProspectDepth, TradePriority.High, "Long-term plan requires more prospect depth."));
        }

        if (organizationId == scenario.Organization.OrganizationId && budget.Status == BudgetStatus.OverBudget && list.All(item => item.Need != PositionNeed.BudgetRelief))
        {
            list.Add(new TeamNeed(PositionNeed.BudgetRelief, TradePriority.Urgent, "Owner budget pressure is shaping monthly needs."));
        }

        foreach (var need in list)
        {
            need.Validate();
        }

        return list
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Need)
            .Take(6)
            .ToArray();
    }

    private static LeagueTeamIdentity IdentityFor(NewGmScenarioSnapshot scenario, string organizationId, string teamName, TeamNeedsProfile needs)
    {
        if (organizationId == scenario.Organization.OrganizationId)
        {
            var culture = scenario.Organization.Culture;
            if (culture.DevelopmentFocus >= 65)
            {
                return LeagueTeamIdentity.DevelopmentOrganization;
            }

            if (culture.FinancialDiscipline >= 65)
            {
                return LeagueTeamIdentity.BudgetOrganization;
            }

            if (culture.WinningPressure >= 68)
            {
                return LeagueTeamIdentity.ChampionshipOrganization;
            }
        }

        if (needs.Direction == TeamDirection.Rebuilder)
        {
            return LeagueTeamIdentity.RebuildingOrganization;
        }

        if (needs.Direction == TeamDirection.WinNow)
        {
            return LeagueTeamIdentity.ChampionshipOrganization;
        }

        var identitySeed = StableHash($"{organizationId}:{teamName}:identity") % 8;
        return identitySeed switch
        {
            0 => LeagueTeamIdentity.DefensiveOrganization,
            1 => LeagueTeamIdentity.HighSkillOrganization,
            2 => LeagueTeamIdentity.PhysicalOrganization,
            3 => LeagueTeamIdentity.GoaltendingOrganization,
            4 => LeagueTeamIdentity.EuropeanPipeline,
            5 => LeagueTeamIdentity.NorthAmericanPipeline,
            6 => LeagueTeamIdentity.VeteranOrganization,
            _ => LeagueTeamIdentity.DevelopmentOrganization
        };
    }

    private static LeagueGmPersonality GmFor(NewGmScenarioSnapshot scenario, string organizationId, string teamName, LeagueTeamIdentity identity, TeamDirection direction)
    {
        if (direction == TeamDirection.WinNow)
        {
            return LeagueGmPersonality.WinNow;
        }

        if (direction is TeamDirection.Rebuilder or TeamDirection.ProspectBuild)
        {
            return identity == LeagueTeamIdentity.RebuildingOrganization ? LeagueGmPersonality.DraftPickCollector : LeagueGmPersonality.ProspectHoarder;
        }

        if (organizationId == scenario.Organization.OrganizationId && scenario.AlphaSnapshot.Owner.Budget.Total < 1_000_000m)
        {
            return LeagueGmPersonality.BudgetFocused;
        }

        return identity switch
        {
            LeagueTeamIdentity.VeteranOrganization => LeagueGmPersonality.VeteranBuilder,
            LeagueTeamIdentity.BudgetOrganization => LeagueGmPersonality.BudgetFocused,
            LeagueTeamIdentity.ChampionshipOrganization => LeagueGmPersonality.AggressiveTrader,
            LeagueTeamIdentity.DevelopmentOrganization => LeagueGmPersonality.PatientBuilder,
            LeagueTeamIdentity.HighSkillOrganization => LeagueGmPersonality.RiskTaker,
            _ => (StableHash($"{organizationId}:{teamName}:gm") % 4) switch
            {
                0 => LeagueGmPersonality.Conservative,
                1 => LeagueGmPersonality.AggressiveTrader,
                2 => LeagueGmPersonality.DraftPickCollector,
                _ => LeagueGmPersonality.PatientBuilder
            }
        };
    }

    private static OwnerInfluencePhilosophy OwnerInfluenceFor(NewGmScenarioSnapshot scenario, string organizationId, LeagueTeamIdentity identity, TeamDirection direction, BudgetSnapshot budget)
    {
        if (organizationId == scenario.Organization.OrganizationId)
        {
            var owner = new OwnerOfficeService().BuildSummary(scenario, budget);
            if (owner.OwnerOfficeBudgetPressure())
            {
                return OwnerInfluencePhilosophy.BudgetDiscipline;
            }

            if (owner.Personality.PersonalityType is OwnerPersonalityType.PatientBuilder or OwnerPersonalityType.ProspectLover)
            {
                return OwnerInfluencePhilosophy.ProspectPatience;
            }
        }

        if (direction == TeamDirection.WinNow || identity == LeagueTeamIdentity.ChampionshipOrganization)
        {
            return OwnerInfluencePhilosophy.WantsPlayoffs;
        }

        if (direction is TeamDirection.Rebuilder or TeamDirection.ProspectBuild || identity == LeagueTeamIdentity.RebuildingOrganization)
        {
            return OwnerInfluencePhilosophy.WantsRebuild;
        }

        return identity switch
        {
            LeagueTeamIdentity.BudgetOrganization => OwnerInfluencePhilosophy.BudgetDiscipline,
            LeagueTeamIdentity.DevelopmentOrganization => OwnerInfluencePhilosophy.ProspectPatience,
            LeagueTeamIdentity.ChampionshipOrganization => OwnerInfluencePhilosophy.SpendForContention,
            _ => OwnerInfluencePhilosophy.CommunityStability
        };
    }

    private static OrganizationStrategyStage StrategyFor(LeagueTeamIdentity identity, TeamDirection direction, OwnerInfluencePhilosophy owner) =>
        direction switch
        {
            TeamDirection.WinNow => OrganizationStrategyStage.ChampionshipWindow,
            TeamDirection.Rebuilder => OrganizationStrategyStage.FiveYearRebuild,
            TeamDirection.ProspectBuild => OrganizationStrategyStage.ProspectAccumulation,
            TeamDirection.BudgetReset => OrganizationStrategyStage.BudgetRecovery,
            _ => owner == OwnerInfluencePhilosophy.WantsRebuild || identity == LeagueTeamIdentity.RebuildingOrganization
                ? OrganizationStrategyStage.ProspectAccumulation
                : identity == LeagueTeamIdentity.DevelopmentOrganization
                    ? OrganizationStrategyStage.StableDevelopment
                    : OrganizationStrategyStage.CompetitiveRetool
        };

    private static TeamBuildingPhilosophy DraftStyleFor(LeagueTeamIdentity identity, LeagueGmPersonality gm, OrganizationStrategyStage strategy) =>
        identity switch
        {
            LeagueTeamIdentity.EuropeanPipeline => TeamBuildingPhilosophy.EuropeanScouting,
            LeagueTeamIdentity.NorthAmericanPipeline => TeamBuildingPhilosophy.LocalTalent,
            LeagueTeamIdentity.VeteranOrganization => TeamBuildingPhilosophy.VeteransFirst,
            LeagueTeamIdentity.ChampionshipOrganization when gm == LeagueGmPersonality.AggressiveTrader => TeamBuildingPhilosophy.BuildThroughTrades,
            LeagueTeamIdentity.BudgetOrganization => TeamBuildingPhilosophy.DraftAndDevelop,
            _ when strategy is OrganizationStrategyStage.FiveYearRebuild or OrganizationStrategyStage.ProspectAccumulation => TeamBuildingPhilosophy.YouthFirst,
            _ => TeamBuildingPhilosophy.Balanced
        };

    private static ScoutingPhilosophy ScoutingFor(LeagueTeamIdentity identity, TeamBuildingPhilosophy draft, BudgetSnapshot budget, bool playerTeam)
    {
        if (playerTeam && budget.ScoutingBudget < 30_000m)
        {
            return ScoutingPhilosophy.LocalTalent;
        }

        return identity switch
        {
            LeagueTeamIdentity.PhysicalOrganization => ScoutingPhilosophy.Size,
            LeagueTeamIdentity.HighSkillOrganization => ScoutingPhilosophy.Skill,
            LeagueTeamIdentity.GoaltendingOrganization => ScoutingPhilosophy.Goalies,
            LeagueTeamIdentity.DefensiveOrganization => ScoutingPhilosophy.Defense,
            LeagueTeamIdentity.EuropeanPipeline => ScoutingPhilosophy.Europe,
            LeagueTeamIdentity.NorthAmericanPipeline => ScoutingPhilosophy.Canada,
            LeagueTeamIdentity.DevelopmentOrganization => ScoutingPhilosophy.Character,
            _ when draft == TeamBuildingPhilosophy.LocalTalent => ScoutingPhilosophy.Canada,
            _ => ScoutingPhilosophy.Balanced
        };
    }

    private static LeagueTeamBehavior BehaviorFor(LeagueTeamIdentity identity, LeagueGmPersonality gm, OwnerInfluencePhilosophy owner, OrganizationStrategyStage strategy, TeamNeedsProfile needs, BudgetSnapshot budget, bool playerTeam)
    {
        var freeAgency = strategy switch
        {
            OrganizationStrategyStage.ChampionshipWindow => "Contender behavior: willing to spend for short-term fit and veteran certainty.",
            OrganizationStrategyStage.FiveYearRebuild or OrganizationStrategyStage.ProspectAccumulation => "Rebuilder behavior: avoids expensive veterans and values flexible youth paths.",
            OrganizationStrategyStage.BudgetRecovery => "Budget behavior: waits for late-market value and avoids bidding wars.",
            _ => "Balanced behavior: selective offers only when role, budget, and fit line up."
        };
        if (playerTeam && budget.Status == BudgetStatus.OverBudget)
        {
            freeAgency = "Budget behavior: must stay quiet unless a move relieves spending.";
        }

        var trade = gm switch
        {
            LeagueGmPersonality.AggressiveTrader or LeagueGmPersonality.RiskTaker => "Trade behavior: explores multi-asset frameworks and accepts risk for a clear need.",
            LeagueGmPersonality.ProspectHoarder or LeagueGmPersonality.DraftPickCollector => "Trade behavior: overvalues picks and prospect rights; reluctant to move young players.",
            LeagueGmPersonality.VeteranBuilder or LeagueGmPersonality.WinNow => "Trade behavior: will pay more for veterans who fit the current window.",
            LeagueGmPersonality.BudgetFocused => "Trade behavior: avoids expensive contracts and prefers budget relief.",
            _ => "Trade behavior: conservative and need-driven."
        };

        var draft = identity switch
        {
            LeagueTeamIdentity.GoaltendingOrganization => "Draft behavior: pushes goalies and safe defensive structure higher.",
            LeagueTeamIdentity.HighSkillOrganization => "Draft behavior: values puck skill and upside even with risk.",
            LeagueTeamIdentity.PhysicalOrganization => "Draft behavior: favors size, compete, and difficult matchups.",
            LeagueTeamIdentity.EuropeanPipeline => "Draft behavior: trusts European viewings and import pathways.",
            _ => "Draft behavior: balances projection, character, and role clarity."
        };

        var behavior = new LeagueTeamBehavior(
            draft,
            trade,
            freeAgency,
            $"Scouting behavior: prioritizes {Readable(ScoutingFor(identity, DraftStyleFor(identity, gm, strategy), budget, playerTeam)).ToLowerInvariant()} and reports that match the club identity.",
            identity is LeagueTeamIdentity.DevelopmentOrganization or LeagueTeamIdentity.RebuildingOrganization
                ? "Development behavior: gives young players more runway and protects confidence."
                : "Development behavior: role is earned quickly; winning context matters.",
            owner == OwnerInfluencePhilosophy.BudgetDiscipline
                ? "Staff hiring behavior: salary fit and chemistry risk matter before reputation."
                : "Staff hiring behavior: targets staff who reinforce the current hockey identity.");
        behavior.Validate();
        return behavior;
    }

    private static string BudgetStyleFor(LeagueTeamIdentity identity, OwnerInfluencePhilosophy owner, BudgetSnapshot budget, bool playerTeam)
    {
        if (playerTeam)
        {
            return budget.Status == BudgetStatus.OverBudget
                ? "Over budget: owner expects discipline before aggressive additions."
                : $"{budget.Status}: {budget.RemainingBudget:C0} remaining.";
        }

        return owner switch
        {
            OwnerInfluencePhilosophy.BudgetDiscipline => "Conservative spending; avoids expensive mistakes.",
            OwnerInfluencePhilosophy.SpendForContention => "Willing to spend when the contention case is clear.",
            OwnerInfluencePhilosophy.ProspectPatience => "Spends on scouting/development more than quick fixes.",
            _ when identity == LeagueTeamIdentity.BudgetOrganization => "Budget-first identity; value contracts preferred.",
            _ => "Balanced spending posture."
        };
    }

    private static string DevelopmentGradeFor(NewGmScenarioSnapshot scenario, string organizationId, LeagueTeamIdentity identity)
    {
        if (organizationId == scenario.Organization.OrganizationId)
        {
            var plans = scenario.DevelopmentPlans.Count + scenario.DevelopmentReviews.Count;
            return plans >= 8 ? "A-" : plans >= 4 ? "B" : "C+";
        }

        return identity switch
        {
            LeagueTeamIdentity.DevelopmentOrganization => "A-",
            LeagueTeamIdentity.RebuildingOrganization or LeagueTeamIdentity.EuropeanPipeline => "B+",
            LeagueTeamIdentity.VeteranOrganization => "C+",
            _ => "B"
        };
    }

    private static string RecentDirectionFor(string teamName, LeagueTeamIdentity identity, OrganizationStrategyStage strategy, TeamNeedsProfile needs) =>
        $"{teamName} is leaning into {Readable(identity).ToLowerInvariant()} traits. Current strategy is {Readable(strategy).ToLowerInvariant()}, and the front office is prioritizing {string.Join(", ", needs.Needs.Take(2).Select(need => Readable(need.Need).ToLowerInvariant()))}.";

    private IReadOnlyList<LeagueTransaction> BuildLeagueNews(NewGmScenarioSnapshot scenario, IReadOnlyList<OrganizationLeagueProfile> profiles)
    {
        var monthKey = scenario.CurrentDate.Year * 100 + scenario.CurrentDate.Month;
        return profiles
            .Where(profile => profile.OrganizationId != scenario.Organization.OrganizationId)
            .Where(profile => StableHash($"{profile.OrganizationId}:{monthKey}:league-ai-news") % 3 == 0)
            .OrderBy(profile => profile.TeamName, StringComparer.Ordinal)
            .Take(3)
            .Select(profile => new LeagueTransaction(
                $"league-ai-news:{profile.OrganizationId}:{monthKey}",
                new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 11, 0, 0, TimeSpan.Zero),
                profile.OrganizationId,
                profile.TeamName,
                null,
                Readable(profile.CurrentStrategy),
                LeagueTransactionType.TeamIdentityUpdate,
                LeagueNewsCategory.League,
                $"{profile.TeamName} direction: {profile.RecentDirection}"))
            .ToArray();
    }

    private static IReadOnlyList<string> BuildHistoryNotes(NewGmScenarioSnapshot scenario, IReadOnlyList<OrganizationLeagueProfile> profiles) =>
        profiles
            .OrderBy(profile => profile.TeamName, StringComparer.Ordinal)
            .Select(profile => $"{scenario.Season.Year}: {profile.TeamName} identified as {Readable(profile.Identity)} with {Readable(profile.CurrentStrategy)} strategy.")
            .ToArray();

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in text)
            {
                hash = hash * 31 + character;
            }

            return Math.Abs(hash);
        }
    }
}

internal static class OwnerOfficeSummaryLeagueAiExtensions
{
    public static bool OwnerOfficeBudgetPressure(this OwnerOfficeSummary summary) =>
        summary.Decisions.Any(decision => decision.DecisionType is OwnerDecisionType.FreezeHiring or OwnerDecisionType.ReduceBudget)
        || summary.Confidence.Pressure >= 70;
}
