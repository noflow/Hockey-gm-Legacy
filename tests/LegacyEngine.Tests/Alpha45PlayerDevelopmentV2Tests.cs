using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha45PlayerDevelopmentV2Tests
{
    public void DevelopmentPlansCreatedForPlayers()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.DevelopmentPlans.Count == scenario.AlphaSnapshot.DevelopmentProfiles.Count, "Every tracked development profile should receive a plan.");
        Assert.True(scenario.DevelopmentPlans.All(plan => plan.FocusAreas.Count > 0), "Plans should include focus areas.");
    }

    public void CoachSpecialtiesGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var coach = new DevelopmentPlanningService().BuildCoachProfile(scenario);

        Assert.True(coach.Specialties.Count > 0, "Development coach should have specialties.");
        Assert.True(coach.FitScore >= 0, "Development coach fit should be calculated.");
    }

    public void IceTimeAffectsGrowth()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var service = new DevelopmentPlanningService();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;

        var scratchPlan = service.SetPlan(seed.Registry, seed.ScenarioSnapshot, personId, new[] { DevelopmentPlanFocus.Balanced }, DevelopmentIceTimeRole.HealthyScratch, "Scratch test.").ScenarioSnapshot;
        var scratch = service.ApplyMonthlyProgress(seed.Registry, scratchPlan, personId, randomModifier: 0);

        seed = NewGmScenarioBootstrapper.CreateScenario();
        personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var topSixPlan = service.SetPlan(seed.Registry, seed.ScenarioSnapshot, personId, new[] { DevelopmentPlanFocus.Balanced }, DevelopmentIceTimeRole.TopSix, "Top-six test.").ScenarioSnapshot;
        var topSix = service.ApplyMonthlyProgress(seed.Registry, topSixPlan, personId, randomModifier: 0);

        Assert.True(topSix.Progress.Outcome <= scratch.Progress.Outcome || topSix.Plan.Confidence >= scratch.Plan.Confidence, "Better ice time should improve or preserve confidence/growth outcome.");
    }

    public void ConfidenceChanges()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var result = new DevelopmentPlanningService().ApplyMonthlyProgress(seed.Registry, seed.ScenarioSnapshot, personId, randomModifier: 5);

        Assert.True(result.Progress.ConfidenceChange != 0 || result.Plan.Confidence != seed.ScenarioSnapshot.DevelopmentPlans[0].Confidence, "Monthly progress should be able to change confidence.");
    }

    public void MoraleChanges()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var service = new DevelopmentPlanningService();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var plan = service.SetPlan(seed.Registry, seed.ScenarioSnapshot, personId, new[] { DevelopmentPlanFocus.Balanced }, DevelopmentIceTimeRole.HealthyScratch, "Bench test.").ScenarioSnapshot;
        var result = service.ApplyMonthlyProgress(seed.Registry, plan, personId, randomModifier: -5);

        Assert.True(result.Plan.Morale <= plan.DevelopmentPlans[0].Morale, "Poor role or regression should not improve morale.");
    }

    public void BreakoutGenerated()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var result = new DevelopmentPlanningService().ApplyMonthlyProgress(seed.Registry, seed.ScenarioSnapshot, personId, randomModifier: 20);

        Assert.True(result.Progress.Outcome is DevelopmentOutcomeType.Breakout or DevelopmentOutcomeType.Progress, "Strong inputs should produce progress or breakout.");
    }

    public void RegressionGenerated()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var service = new DevelopmentPlanningService();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var plan = service.SetPlan(seed.Registry, seed.ScenarioSnapshot, personId, new[] { DevelopmentPlanFocus.Balanced }, DevelopmentIceTimeRole.HealthyScratch, "Regression setup.").ScenarioSnapshot;
        var result = service.ApplyMonthlyProgress(seed.Registry, plan, personId, randomModifier: -20);

        Assert.True(result.Progress.Outcome is DevelopmentOutcomeType.Regression or DevelopmentOutcomeType.Plateau, "Poor inputs should create plateau or regression risk.");
    }

    public void CoachRecommendationsGenerated()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var result = new DevelopmentPlanningService().ApplyMonthlyProgress(seed.Registry, seed.ScenarioSnapshot, personId, randomModifier: 5);

        Assert.True(result.Recommendations.Count > 0, "Development progress should return coach recommendations.");
        Assert.True(!string.IsNullOrWhiteSpace(result.Recommendations[0].RecommendedAction), "Recommendation should explain an action.");
    }

    public void YearlyReviewGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var personId = scenario.DevelopmentPlans[0].PersonId;
        var review = new DevelopmentPlanningService().GenerateYearlyReview(scenario, personId);

        Assert.True(review.ImprovedThemes.Count > 0, "Yearly review should include improved themes.");
        Assert.True(!string.IsNullOrWhiteSpace(review.FutureProjection), "Yearly review should include future projection.");
    }

    public void PlayerDossierIncludesDevelopmentPlan()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var personId = scenario.DevelopmentPlans[0].PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, personId);
        var text = string.Join("\n", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Development plan:", StringComparison.Ordinal), "Dossier should show development plan.");
        Assert.True(text.Contains("Morale:", StringComparison.Ordinal), "Dossier should show morale.");
        Assert.False(text.Contains("Potential =", StringComparison.Ordinal), "Dossier must not expose hidden potential.");
    }

    public void CareerTimelineUpdated()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var service = new DevelopmentPlanningService();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var result = service.SetPlan(seed.Registry, seed.ScenarioSnapshot, personId, new[] { DevelopmentPlanFocus.Skating }, DevelopmentIceTimeRole.MiddleSix, "Timeline test.");

        Assert.True(result.ScenarioSnapshot.CareerTimeline.ForPerson(personId).Any(entry => entry.Title.Contains("Development plan", StringComparison.OrdinalIgnoreCase)), "Development changes should create career timeline context.");
    }

    public void DashboardActionCenterIncludesDevelopment()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var service = new DevelopmentPlanningService();
        var personId = seed.ScenarioSnapshot.DevelopmentPlans[0].PersonId;
        var result = service.ApplyMonthlyProgress(seed.Registry, seed.ScenarioSnapshot, personId, randomModifier: 20);
        var budget = new BudgetOverviewService().Build(result.ScenarioSnapshot, seed.Registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());
        var readiness = new SeasonReadinessService().Evaluate(seed.Registry, result.ScenarioSnapshot);
        var items = new ActionCenterService().BuildItems(result.ScenarioSnapshot, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.PlayerDevelopment), "Action Center should surface development recommendations.");
    }

    public void AlphaDesktopExposesDevelopmentV2Ui()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Development Plan", StringComparison.Ordinal), "AlphaDesktop should expose development plan section.");
        Assert.True(source.Contains("Balanced Plan", StringComparison.Ordinal), "AlphaDesktop should expose plan action.");
        Assert.True(source.Contains("Increase Ice Time", StringComparison.Ordinal), "AlphaDesktop should expose ice-time action.");
        Assert.True(source.Contains("Yearly Review", StringComparison.Ordinal), "AlphaDesktop should expose yearly development review.");
    }

    public void Alpha45HasNoHiddenRatingsGodotOrGameSimulationChanges()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Development*.cs", SearchOption.TopDirectoryOnly)
            .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 4.5 should not add Godot.");
        Assert.False(text.Contains("BasicGameSimulator", StringComparison.Ordinal), "Alpha 4.5 should not change game simulation.");
        Assert.False(text.Contains("VisibleRating", StringComparison.Ordinal), "Alpha 4.5 should not expose hidden ratings.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
