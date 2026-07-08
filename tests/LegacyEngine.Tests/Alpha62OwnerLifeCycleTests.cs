using LegacyEngine.Integration;
using LegacyEngine.Owners;
using LegacyEngine.RuleEngine;

internal sealed class Alpha62OwnerLifeCycleTests
{
    public void OwnerLifeStageGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OwnerCareerState is not null, "Owner life-cycle state should be generated.");
        Assert.True(scenario.OwnerCareerSummary is not null, "Owner career summary should be generated.");
        Assert.True(scenario.OwnerCareerState!.LifeStage is OwnerLifeStage.NewOwner
            or OwnerLifeStage.EstablishedOwner
            or OwnerLifeStage.PatientBuilder
            or OwnerLifeStage.PressureOwner
            or OwnerLifeStage.ChampionshipOwner
            or OwnerLifeStage.DecliningInterest
            or OwnerLifeStage.TransitionPlanning
            or OwnerLifeStage.FormerOwner, "Owner should receive a valid life stage.");
    }

    public void ExpectationHistoryGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OwnerExpectationHistory.Count > 0, "Owner expectation history should be stored.");
        Assert.True(scenario.OwnerExpectationHistory.All(item => !string.IsNullOrWhiteSpace(item.OwnerReaction)), "Expectation history should include owner reactions.");
        Assert.True(scenario.OwnerExpectationHistory.Any(item => item.Result is OwnerExpectationResult.NotStarted or OwnerExpectationResult.OnTrack or OwnerExpectationResult.Mixed or OwnerExpectationResult.Met or OwnerExpectationResult.Missed), "Expectation history should record results.");
    }

    public void ConfidenceHistoryGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OwnerConfidenceHistory.Count > 0, "Owner confidence history should be stored.");
        Assert.True(scenario.OwnerConfidenceHistory.All(item => item.Confidence is >= 0 and <= 100), "Confidence should stay in range.");
        Assert.True(scenario.OwnerConfidenceHistory.All(item => item.BudgetSupport is >= 0 and <= 100), "Budget support should stay in range.");
    }

    public void MeetingAndLetterHistoryGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OwnerMeetingHistory.Count > 0, "Owner meeting history should be stored.");
        Assert.True(scenario.OwnerMeetingHistory.All(item => !string.IsNullOrWhiteSpace(item.OwnerMessage)), "Owner meetings should include owner messages.");
        Assert.True(scenario.OwnerLetters.Count > 0, "Owner letters should be stored as permanent history.");
        Assert.True(scenario.OwnerLetters.All(item => !string.IsNullOrWhiteSpace(item.Subject) && !string.IsNullOrWhiteSpace(item.Body)), "Owner letters should be readable.");
    }

    public void JobSecurityHistoryGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OwnerJobSecurityHistory.Count > 0, "Owner job-security history should be stored.");
        Assert.True(scenario.OwnerJobSecurityHistory.All(item => item.Score is >= 0 and <= 100), "Job-security score should stay in range.");
        Assert.True(scenario.OwnerJobSecurityHistory.All(item => !string.IsNullOrWhiteSpace(item.Reason)), "Job-security history should include reasons.");
    }

    public void OwnerLegacyAndOrganizationEraGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OwnerLegacyProfile is not null, "Owner legacy profile should be generated.");
        Assert.True(scenario.OwnerLegacyProfile!.LegacySummary.Contains(scenario.AlphaSnapshot.Owner.Name, StringComparison.Ordinal), "Owner legacy should name the owner.");
        Assert.True(scenario.OwnerCareerSummary!.OrganizationHistorySummary.Contains("tenure", StringComparison.OrdinalIgnoreCase), "Owner summary should describe ownership tenure.");
        Assert.True(!string.IsNullOrWhiteSpace(scenario.OwnerCareerSummary.BudgetRelationship), "Owner summary should describe budget relationship.");
        Assert.True(!string.IsNullOrWhiteSpace(scenario.OwnerCareerSummary.PersonalityEvolution), "Owner summary should describe personality evolution.");
    }

    public void MilestonesUpdateHistoryAndLeagueNews()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var notable = scenario.OwnerMilestones.FirstOrDefault(item => item.IsNotable);

        Assert.True(scenario.OwnerMilestones.Count > 0, "Owner milestones should be stored.");
        Assert.True(notable is not null, "At least one notable owner milestone should exist.");
        Assert.True(scenario.CareerTimeline.Entries.Any(item => item.EntryId.Contains("owner-lifecycle", StringComparison.Ordinal)), "Owner milestones should update career timeline.");
        Assert.True(scenario.OwnerLifeCycleNews.Any(item => item.TransactionType == LeagueTransactionType.OwnerMilestone), "Notable owner milestones should create league history/news.");
    }

    public void ActionCenterItemsGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var items = new OwnerLifeCycleService().BuildActionItems(scenario);

        Assert.True(items.Count > 0, "Owner life-cycle should create Action Center items.");
        Assert.True(items.All(item => item.Category == ActionCenterCategory.Owner), "Owner life-cycle Action Center items should be owner categorized.");
    }

    public void ReportsIncludeOwnerLifeCycleHighlights()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var highlights = new OwnerLifeCycleService().BuildReportHighlights(scenario);

        Assert.True(highlights.Any(line => line.Contains("Owner life stage", StringComparison.OrdinalIgnoreCase)), "Owner reports should include life stage.");
        Assert.True(highlights.Any(line => line.Contains("Budget relationship", StringComparison.OrdinalIgnoreCase)), "Owner reports should include budget relationship.");
        Assert.True(highlights.Any(line => line.Contains("Legacy", StringComparison.OrdinalIgnoreCase)), "Owner reports should include owner legacy.");
    }

    public void BudgetRelationshipReflectsBudgetPressure()
    {
        var result = CreateScenario();
        var scenario = result.ScenarioSnapshot;
        var lowBudgetOwner = scenario.AlphaSnapshot.Owner with
        {
            Budget = new OwnerBudget(1m, 1m, 1m, 1m, 1m)
        };
        var lowBudgetScenario = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { Owner = lowBudgetOwner }
        };
        var updated = new OwnerLifeCycleService().EnsureLifeCycle(lowBudgetScenario, result.Registry);

        Assert.True(updated.OwnerCareerSummary!.BudgetRelationship.Contains("overspending", StringComparison.OrdinalIgnoreCase)
            || updated.OwnerCareerSummary.BudgetRelationship.Contains("budget pressure", StringComparison.OrdinalIgnoreCase)
            || updated.OwnerCareerSummary.BudgetRelationship.Contains("restrict", StringComparison.OrdinalIgnoreCase), "Over-budget owner relationship should warn about budget pressure.");
    }

    public void SaveLoadPreservesOwnerLifeCycle()
    {
        var service = new SaveGameService();
        var scenario = CreateScenario().ScenarioSnapshot;
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha62-{Guid.NewGuid():N}.json");
        var saved = service.SaveCareer(
            scenario,
            Array.Empty<InboxMessage>(),
            scenario.OwnerLifeCycleNews,
            new Dictionary<string, ActionCenterStatus>(),
            budget,
            path,
            "Alpha 6.2 Test Save");

        Assert.True(saved.Success, saved.Message);
        var loaded = service.LoadFromFile(path, RulebookPresets.CreateJuniorMajor());
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.OwnerCareerSummary is not null, "Loaded save should preserve owner career summary.");
        Assert.Equal(scenario.OwnerExpectationHistory.Count, loaded.SaveGame.ScenarioSnapshot.OwnerExpectationHistory.Count);
        Assert.Equal(scenario.OwnerMilestones.Count, loaded.SaveGame.ScenarioSnapshot.OwnerMilestones.Count);
    }

    public void AlphaDesktopExposesOwnerLifeCycleUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Owner Life Cycle", StringComparison.Ordinal), "Owner screen should expose owner life-cycle summary.");
        Assert.True(source.Contains("Owner History", StringComparison.Ordinal), "Reports should expose owner history.");
        Assert.True(source.Contains("Owner Letters", StringComparison.Ordinal), "Reports should expose owner letters.");
        Assert.True(source.Contains("Job Security History", StringComparison.Ordinal), "Reports should expose job-security history.");
        Assert.True(source.Contains("Expectation Results", StringComparison.Ordinal), "Reports should expose expectation results.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*OwnerLifeCycle*.cs", SearchOption.TopDirectoryOnly)
                .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
                .Select(File.ReadAllText));

        Assert.False(text.Contains("OwnerReplacementService", StringComparison.Ordinal), "Alpha 6.2 should not build owner replacement.");
        Assert.False(text.Contains("BoardOfDirectors", StringComparison.Ordinal), "Alpha 6.2 should not build board of directors logic.");
        Assert.False(text.Contains("MediaPressureEngine", StringComparison.Ordinal), "Alpha 6.2 should not build media pressure engine.");
        Assert.False(text.Contains("JobOfferService", StringComparison.Ordinal), "Alpha 6.2 should not build job offers.");
        Assert.False(text.Contains("ApplyFiring", StringComparison.Ordinal), "Alpha 6.2 should not build actual firing.");
        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 6.2 should not add Godot.");
        Assert.False(text.Contains("BasicGameSimulator", StringComparison.Ordinal), "Alpha 6.2 should not change game simulation.");
    }

    private static NewGmScenarioResult CreateScenario() =>
        NewGmScenarioBootstrapper.CreateScenario(rulebook: RulebookPresets.CreateJuniorMajor());

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
