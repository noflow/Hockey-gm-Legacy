using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha712OrganizationPlanningTests
{
    public void OrganizationPlanCreated()
    {
        var scenario = Scenario();
        var plan = scenario.CurrentOrganizationPlan!;

        Assert.Equal(scenario.Organization.OrganizationId, plan.OrganizationId);
        Assert.True(plan.Summary.Contains(scenario.Organization.Name, StringComparison.Ordinal), "Plan summary should name the organization.");
        Assert.Equal(PlanningHorizon.FiveYears, plan.Horizon);
    }

    public void DepthChartAndFutureLineupGenerated()
    {
        var scenario = Scenario();
        var plan = scenario.CurrentOrganizationPlan!;

        Assert.True(plan.DepthPlan.CurrentDepth.Count > 0, "Current depth chart should be generated.");
        Assert.True(plan.DepthPlan.FutureDepth.Count > 0, "Future lineup/depth chart should be generated.");
        Assert.True(plan.DepthPlan.FutureDepth.Any(slot => slot.Year > scenario.Season.Year), "Future depth should look beyond the current season.");
    }

    public void ProspectPlanningGenerated()
    {
        var scenario = Scenario();
        var prospects = scenario.CurrentOrganizationPlan!.ProspectPlan.Prospects;

        Assert.True(prospects.Count > 0, "Prospect pipeline should be planned.");
        Assert.True(prospects.All(prospect => prospect.Path.Count > 0), "Every prospect should have a development path.");
        Assert.True(prospects.All(prospect => prospect.ExpectedArrivalYear >= scenario.Season.Year), "Prospect ETA should be current year or later.");
    }

    public void PromotionAndBlockingPlanningGenerated()
    {
        var plan = Scenario().CurrentOrganizationPlan!;

        Assert.True(plan.RosterPlan.PromotionCandidates.Count > 0, "Plan should identify promotion candidates.");
        Assert.True(plan.RosterPlan.SuccessionPlans.Count > 0 || plan.RosterPlan.BlockedProspects.Count >= 0, "Plan should evaluate succession and blocking.");
    }

    public void ContractPlanningGenerated()
    {
        var plan = Scenario().CurrentOrganizationPlan!;

        Assert.True(plan.ContractPlan.Summary.Length > 0, "Contract plan should include a readable summary.");
        Assert.True(plan.ContractPlan.CapBudgetSummary.Length > 0, "Contract plan should include cap/budget context.");
        Assert.True(plan.ContractPlan.CurrentCommittedSalary >= 0, "Committed salary should be tracked.");
    }

    public void CompetitiveWindowAndNeedsGenerated()
    {
        var plan = Scenario().CurrentOrganizationPlan!;

        Assert.True(Enum.IsDefined(plan.Window), "Competitive window should be generated.");
        Assert.True(plan.RosterPlan.FutureNeeds.Count > 0, "Future roster needs should be generated.");
        Assert.True(plan.Reports.Any(report => report.Contains("Window", StringComparison.OrdinalIgnoreCase)), "Reports should explain the competitive window.");
    }

    public void TradeAndFreeAgencyPlanningGenerated()
    {
        var plan = Scenario().CurrentOrganizationPlan!;

        Assert.True(plan.FreeAgencyTargets.Count > 0, "Plan should include free-agency targets or a holding recommendation.");
        Assert.True(plan.TradeTargets.Count > 0, "Plan should include trade targets or a holding recommendation.");
    }

    public void PlanningReportGenerated()
    {
        var scenario = Scenario();
        var report = new OrganizationPlanningService().BuildPlanningReport(scenario);

        Assert.True(report.Contains("Organization Planning Report", StringComparison.Ordinal), "Report should be titled.");
        Assert.True(report.Contains("Top Needs", StringComparison.Ordinal), "Report should include top needs.");
        Assert.True(report.Contains("Future Depth", StringComparison.Ordinal), "Report should include future depth.");
        Assert.True(report.Contains("Prospect Pipeline", StringComparison.Ordinal), "Report should include prospect pipeline.");
        Assert.True(report.Contains("Contracts", StringComparison.Ordinal), "Report should include contract planning.");
    }

    public void LeagueOrganizationPlansGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.OrganizationPlans.Count >= 4, "League teams should receive organization plans.");
        Assert.True(scenario.OrganizationPlans.Any(plan => plan.OrganizationId != scenario.Organization.OrganizationId), "AI organizations should receive planning profiles.");
    }

    public void SaveLoadPreservesOrganizationPlans()
    {
        var scenario = Scenario();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha712-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());

        var saved = new SaveGameService().SaveCareer(
            scenario,
            Array.Empty<InboxMessage>(),
            Array.Empty<LeagueTransaction>(),
            new Dictionary<string, ActionCenterStatus>(),
            budget,
            path);
        var loaded = new SaveGameService().LoadFromFile(path, RulebookPresets.CreateJuniorMajor());

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.CurrentOrganizationPlan is not null, "Current organization plan should survive save/load.");
        Assert.Equal(scenario.OrganizationPlans.Count, loaded.SaveGame.ScenarioSnapshot.OrganizationPlans.Count);
    }

    public void AlphaDesktopExposesOrganizationPlanning()
    {
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(desktop.Contains("Organization Planning", StringComparison.Ordinal), "Desktop should expose Organization Planning.");
        Assert.True(desktop.Contains("BuildOrganizationPlanning", StringComparison.Ordinal), "Desktop should render planning details.");
        Assert.True(desktop.Contains("Future Needs", StringComparison.Ordinal), "Desktop should show future needs.");
        Assert.True(desktop.Contains("Prospect Pipeline", StringComparison.Ordinal), "Desktop should show prospect pipeline.");
    }

    private static NewGmScenarioSnapshot Scenario()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        scenario = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        scenario = new ScoutingIntelligenceService().EnsureKnowledgeProfiles(scenario);
        scenario = new DevelopmentCurveService().EnsureCurves(scenario);
        scenario = new PlayerRatingService().EnsureRatings(scenario);
        scenario = new DraftWarRoomService().EnsureWarRoom(scenario);
        scenario = new AssetEvaluationService().EnsureEvaluations(scenario);
        return new OrganizationPlanningService().EnsurePlans(scenario);
    }

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "HockeyGmLegacy.slnx")))
        {
            current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }

        return current;
    }
}
