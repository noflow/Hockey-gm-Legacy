using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

internal sealed class Alpha59LeagueAiV2Tests
{
    public void OrganizationAiProfilesGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.OrganizationAiProfiles.Count >= 6, "Scenario should generate organization AI profiles for league teams.");
        Assert.True(scenario.OrganizationAiProfiles.All(profile => profile.Personality != OrganizationAiPersonality.Unknown), "Every team should have a distinct AI personality.");
        Assert.True(scenario.OrganizationAiProfiles.Select(profile => profile.Personality).Distinct().Count() >= 3, "League should include multiple AI personalities.");
    }

    public void TeamNeedsGeneratedFromRosterAndStrategy()
    {
        var profile = Scenario().OrganizationAiProfiles.First();

        Assert.True(profile.CurrentNeeds.Count > 0, "AI profile should include team needs.");
        Assert.True(profile.CurrentNeeds.All(need => !string.IsNullOrWhiteSpace(need.Reason) && !string.IsNullOrWhiteSpace(need.Urgency)), "Needs should explain reason and urgency.");
        Assert.True(profile.CurrentNeeds.All(need => Enum.IsDefined(need.SuggestedAssetType)), "Needs should include suggested asset targets.");
    }

    public void RebuildingTeamValuesPicksAndProspects()
    {
        var service = new OrganizationAiService();
        var profile = ForcedProfile(OrganizationStrategyPhase.Rebuilding, OrganizationAiPersonality.PatientRebuilder);

        var future = service.EvaluateDecision(profile, new AiDecisionContext(AiDecisionCategory.Trade, new[] { AiAssetType.DraftPick, AiAssetType.ProspectRights }, BudgetImpact: 0m));
        var veteran = service.EvaluateDecision(profile, new AiDecisionContext(AiDecisionCategory.Trade, new[] { AiAssetType.VeteranHelpNow }, BudgetImpact: 90_000m, Age: 22));

        Assert.True(future.Score > veteran.Score, "Rebuilding teams should prefer picks/prospects over expensive veteran help.");
        Assert.True(future.Reasons.Any(reason => reason.Contains("future assets", StringComparison.OrdinalIgnoreCase) || reason.Contains("picks", StringComparison.OrdinalIgnoreCase)), "Decision should explain future-asset preference.");
    }

    public void ContenderValuesVeteransAndHelpNow()
    {
        var service = new OrganizationAiService();
        var profile = ForcedProfile(OrganizationStrategyPhase.Contending, OrganizationAiPersonality.WinNow, needs: new[]
        {
            new TeamNeedProfile(TeamNeedType.VeteranLeadership, TradePriority.High, "Room needs a help-now veteran.", "Before playoffs", AiAssetType.VeteranHelpNow),
            new TeamNeedProfile(TeamNeedType.Scoring, TradePriority.Medium, "Lineup needs more offense.", "Before deadline", AiAssetType.ScoringForward)
        });

        var helpNow = service.EvaluateDecision(profile, new AiDecisionContext(AiDecisionCategory.Trade, new[] { AiAssetType.VeteranHelpNow, AiAssetType.RosterPlayer }, BudgetImpact: 35_000m, Age: 22));
        var future = service.EvaluateDecision(profile, new AiDecisionContext(AiDecisionCategory.Trade, new[] { AiAssetType.DraftPick }, BudgetImpact: 0m));

        Assert.True(helpNow.Score > future.Score, "Contenders should value immediate lineup help.");
    }

    public void BudgetTeamAvoidsExpensiveContracts()
    {
        var service = new OrganizationAiService();
        var profile = ForcedProfile(OrganizationStrategyPhase.BudgetReset, OrganizationAiPersonality.BudgetConscious);

        var expensive = service.EvaluateFreeAgencyDecision(profile, 160_000m, 22, RosterPosition.Center, immediateHelp: true);
        var cheap = service.EvaluateFreeAgencyDecision(profile, 15_000m, 18, RosterPosition.Center, immediateHelp: false);

        Assert.True(cheap.Score > expensive.Score, "Budget teams should avoid expensive commitments.");
        Assert.True(expensive.Reasons.Any(reason => reason.Contains("budget", StringComparison.OrdinalIgnoreCase)), "Decision should mention budget pressure.");
    }

    public void DraftBehaviorChangesByStrategy()
    {
        var scenario = Scenario();
        var goalie = scenario.AlphaSnapshot.DraftBoard.Entries.First(entry => entry.Bio?.Position == RosterPosition.Goalie);
        var service = new OrganizationAiService();
        var goalieTeam = ForcedProfile(OrganizationStrategyPhase.Developing, OrganizationAiPersonality.GoalieFocused);
        var conservative = ForcedProfile(OrganizationStrategyPhase.Retooling, OrganizationAiPersonality.Conservative, needs: new[]
        {
            new TeamNeedProfile(TeamNeedType.VeteranLeadership, TradePriority.Medium, "Roster wants an older voice.", "Before camp", AiAssetType.VeteranHelpNow)
        });
        var classProfile = scenario.CurrentDraftClassProfile! with { Theme = DraftClassTheme.StrongGoalieClass };

        var goalieResult = service.EvaluateDraftDecision(goalieTeam, goalie, classProfile);
        var conservativeResult = service.EvaluateDraftDecision(conservative, goalie, classProfile);

        Assert.True(goalieResult.Score > conservativeResult.Score, "Goalie-focused teams should value goalies more in a strong goalie class.");
    }

    public void FreeAgencyBehaviorChangesByPersonality()
    {
        var service = new OrganizationAiService();
        var spender = ForcedProfile(OrganizationStrategyPhase.Contending, OrganizationAiPersonality.BigSpender);
        var budget = ForcedProfile(OrganizationStrategyPhase.BudgetReset, OrganizationAiPersonality.BudgetConscious);

        var spenderResult = service.EvaluateFreeAgencyDecision(spender, 125_000m, 21, RosterPosition.LeftWing, immediateHelp: true);
        var budgetResult = service.EvaluateFreeAgencyDecision(budget, 125_000m, 21, RosterPosition.LeftWing, immediateHelp: true);

        Assert.True(spenderResult.Score > budgetResult.Score, "Big spenders should be more comfortable with premium free-agent asks.");
    }

    public void StaffHiringBehaviorChangesByOrganizationIdentity()
    {
        var service = new OrganizationAiService();
        var development = ForcedProfile(OrganizationStrategyPhase.Developing, OrganizationAiPersonality.DraftAndDevelop);
        var budget = ForcedProfile(OrganizationStrategyPhase.BudgetReset, OrganizationAiPersonality.BudgetConscious);

        var developmentCoach = service.EvaluateStaffDecision(development, StaffRole.DevelopmentCoach, 60_000m);
        var expensiveBudgetHire = service.EvaluateStaffDecision(budget, StaffRole.DevelopmentCoach, 110_000m);

        Assert.True(developmentCoach.Score > expensiveBudgetHire.Score, "Development teams should value development coaches more than budget teams value expensive staff.");
    }

    public void StrategyEvolvesAfterPoorSeasonAndAgingRoster()
    {
        var service = new OrganizationAiService();
        var profile = ForcedProfile(OrganizationStrategyPhase.Contending, OrganizationAiPersonality.VeteranBuilder);

        var retool = service.EvolveStrategy(profile, new DateOnly(2027, 5, 1), pointsPercentage: 0.50, averageRosterAge: 24, consecutiveLosingSeasons: 0, prospectPoolStrength: 45, budgetPressure: 0m);
        var rebuild = service.EvolveStrategy(profile, new DateOnly(2027, 5, 2), pointsPercentage: 0.31, averageRosterAge: 20, consecutiveLosingSeasons: 2, prospectPoolStrength: 35, budgetPressure: 0m);

        Assert.Equal(OrganizationStrategyPhase.Retooling, retool.Strategy.Phase);
        Assert.Equal(OrganizationStrategyPhase.Rebuilding, rebuild.Strategy.Phase);
    }

    public void StrategyChangesRecordedInHistory()
    {
        var scenario = Scenario();
        var service = new OrganizationAiService();
        var changed = service.EvolveStrategy(scenario.OrganizationAiProfiles.First(), scenario.CurrentDate.AddDays(1), 0.32, 20, 2, 30, 0m);

        var updated = service.RecordStrategyHistory(scenario, changed);

        Assert.True(changed.StrategyHistory.Any(change => change.ToPhase == OrganizationStrategyPhase.Rebuilding), "Strategy change should be stored on the profile.");
        Assert.True(updated.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.OrganizationStrategyChanged && entry.OrganizationId == changed.OrganizationId), "Strategy change should be recorded in organization history.");
    }

    public void LeagueNewsReceivesStrategyUpdates()
    {
        var scenario = Scenario();
        var news = new LeagueAiService().BuildReport(scenario).LeagueNews;

        Assert.True(news.Count <= 3, "Strategic league news should stay limited.");
        Assert.True(news.All(item => item.TransactionType == LeagueTransactionType.TeamIdentityUpdate), "Strategy updates should use league identity update news.");
        Assert.True(news.All(item => item.Description.Contains("prioritizing", StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains("expected", StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains("pursuing", StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains("development", StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains("selective", StringComparison.OrdinalIgnoreCase)), "League news should describe strategic direction.");
    }

    public void AlphaDesktopExposesAiStrategyAndTeamNeeds()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("League AI filters", StringComparison.Ordinal), "Desktop should expose league AI filter placeholders.");
        Assert.True(source.Contains("AI personality", StringComparison.Ordinal), "Desktop should show AI personality.");
        Assert.True(source.Contains("Strategy phase", StringComparison.Ordinal), "Desktop should show strategy phase.");
        Assert.True(source.Contains("Current AI needs", StringComparison.Ordinal), "Desktop should show AI need profiles.");
        Assert.True(source.Contains("Other team AI strategy", StringComparison.Ordinal), "Trade UI should show other-team strategy.");
    }

    public void TradeEvaluationIncludesStrategyNeedExplanation()
    {
        var scenario = Scenario();
        var service = new TradeService();
        var other = scenario.LeagueProfile.Teams.First(team => team.OrganizationId != scenario.Organization.OrganizationId);
        var offer = new TradeOffer(
            "trade-alpha59-test",
            scenario.CurrentDate,
            other.OrganizationId,
            other.TeamName,
            TradeOfferStatus.Proposed,
            new[] { service.CreateDraftPickAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, 2, scenario.Season.Year + 1) },
            new[] { service.CreateFutureConsiderationAsset(scenario, TradeSide.OtherOrganization, other.OrganizationId, other.TeamName) });

        var evaluation = service.EvaluateTrade(scenario, offer);

        Assert.True(evaluation.Reasons.Any(reason => reason.Contains("AI strategy", StringComparison.Ordinal)), "Trade reasons should mention AI strategy.");
        Assert.True(evaluation.Reasons.Any(reason => reason.Contains("Top needs", StringComparison.Ordinal)), "Trade reasons should mention top needs.");
    }

    public void SaveLoadPreservesAiProfiles()
    {
        var scenario = Scenario();
        var budget = new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var service = new SaveGameService();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-alpha59-{Guid.NewGuid():N}.json");

        var saved = service.SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = service.LoadFromFile(path, RulebookPresets.CreateJuniorMajor());

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.OrganizationAiProfiles.Count, loaded.SaveGame!.ScenarioSnapshot.OrganizationAiProfiles.Count);
        Assert.Equal(scenario.OrganizationAiProfiles.First().Personality, loaded.SaveGame.ScenarioSnapshot.OrganizationAiProfiles.First().Personality);
    }

    public void NoHiddenRatingsOrForbiddenSystemsAdded()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "OrganizationAiService.cs"));

        Assert.False(source.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase), "League AI should not expose hidden current ability.");
        Assert.False(source.Contains("Potential =", StringComparison.OrdinalIgnoreCase), "League AI should not expose hidden potential ratings.");
        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "League AI should not reference Godot.");
        Assert.False(source.Contains("MediaEngine", StringComparison.OrdinalIgnoreCase), "League AI should not build a media engine.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        new OrganizationAiService().EnsureProfiles(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);

    private static OrganizationAiProfile ForcedProfile(
        OrganizationStrategyPhase phase,
        OrganizationAiPersonality personality,
        IReadOnlyList<TeamNeedProfile>? needs = null)
    {
        var baseProfile = Scenario().OrganizationAiProfiles.First();
        var nextNeeds = needs ?? new[]
        {
            new TeamNeedProfile(TeamNeedType.DraftPicks, TradePriority.High, "Future asset base needs help.", "Next draft cycle", AiAssetType.DraftPick),
            new TeamNeedProfile(TeamNeedType.Prospects, TradePriority.High, "Pipeline depth is thin.", "This season", AiAssetType.ProspectRights),
            new TeamNeedProfile(TeamNeedType.TopSixForward, TradePriority.Medium, "Lineup needs more scoring.", "Before deadline", AiAssetType.ScoringForward)
        };
        var strategy = baseProfile.Strategy with
        {
            Phase = phase,
            RiskTolerance = personality is OrganizationAiPersonality.RiskTaker or OrganizationAiPersonality.BigSpender ? 78 : 45,
            Summary = $"Forced {phase} test strategy."
        };

        var profile = baseProfile with
        {
            Personality = personality,
            Strategy = strategy,
            CurrentNeeds = nextNeeds,
            Summary = $"Forced {personality} {phase} test profile."
        };
        profile.Validate();
        return profile;
    }

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HockeyGmLegacy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
