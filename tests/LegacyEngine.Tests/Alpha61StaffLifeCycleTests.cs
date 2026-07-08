using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

internal sealed class Alpha61StaffLifeCycleTests
{
    public void LifeStagesGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerStates.Count > 0, "Staff life-cycle states should be generated.");
        Assert.True(scenario.StaffCareerStates.Any(state => state.LifeStage is StaffLifeStage.Assistant or StaffLifeStage.Established or StaffLifeStage.Respected or StaffLifeStage.Veteran), "Staff should receive recognizable life stages.");
    }

    public void CareerHistoryGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerSummaries.All(summary => summary.Organizations.Count > 0), "Staff summaries should track organizations.");
        Assert.True(scenario.StaffCareerSummaries.All(summary => summary.Roles.Count > 0), "Staff summaries should track roles.");
        Assert.True(scenario.StaffCareerSummaries.All(summary => summary.SalaryHistory.Count > 0), "Staff summaries should track salary history.");
    }

    public void ReputationGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerStates.All(state => !string.IsNullOrWhiteSpace(state.Reputation.Summary)), "Staff reputation should include a readable summary.");
        Assert.True(scenario.StaffCareerStates.Any(state => state.Reputation.Category is StaffReputationCategory.Promising or StaffReputationCategory.Respected or StaffReputationCategory.Elite), "Staff should receive reputation categories.");
    }

    public void ScoutingCareersGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var scout = scenario.StaffCareerSummaries.FirstOrDefault(summary => summary.Department == StaffDepartment.Scouting);

        Assert.True(scout is not null, "Scenario should include scouting staff careers.");
        Assert.True(scout!.PlayersDiscovered.Count > 0, "Scout careers should track players discovered or recommended.");
        Assert.True(scout.PersonalLegacy.Contains("prospect", StringComparison.OrdinalIgnoreCase), "Scout legacy should mention prospect discovery.");
    }

    public void CoachingCareersGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var coach = scenario.StaffCareerSummaries.FirstOrDefault(summary => summary.Department == StaffDepartment.Coaching);

        Assert.True(coach is not null, "Scenario should include coaching staff careers.");
        Assert.True(coach!.PlayersDeveloped.Count > 0, "Coach careers should track players developed.");
        Assert.True(coach.PersonalLegacy.Contains("developed", StringComparison.OrdinalIgnoreCase), "Coach legacy should mention player development.");
    }

    public void CoachingTreeGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerSummaries.Any(summary => summary.CoachingTree.Count > 0), "Staff life-cycle should track coaching tree links.");
    }

    public void PromotionRecommendationGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerSummaries.Any(summary => !summary.PromotionReadiness.StartsWith("No immediate", StringComparison.OrdinalIgnoreCase)), "At least one staff member should have a promotion or succession recommendation.");
    }

    public void StaffMovementIncluded()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerSummaries.Any(summary => summary.CareerStory.Any(line => line.Contains("organization", StringComparison.OrdinalIgnoreCase))), "Staff career stories should include organization history.");
    }

    public void RelationshipsIncluded()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerSummaries.All(summary => summary.Relationships.Count > 0), "Staff summaries should include relationship context.");
    }

    public void PlayerMentorshipIncluded()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerSummaries.Any(summary => summary.PlayersDeveloped.Count > 0 || summary.PlayersDiscovered.Count > 0), "Staff life-cycle should connect staff to player mentorship, development, or discovery.");
    }

    public void TimelineUpdated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var milestone = scenario.StaffMilestones.First();

        Assert.True(scenario.CareerTimeline.ForPerson(milestone.PersonId).Any(entry => entry.EntryId.Contains(milestone.MilestoneId, StringComparison.Ordinal)), "Staff milestones should become career timeline entries.");
    }

    public void HistoryStored()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerStates.Count > 0, "Scenario should store staff career states.");
        Assert.True(scenario.StaffCareerSummaries.Count > 0, "Scenario should store staff career summaries.");
        Assert.True(scenario.StaffMilestones.Count > 0, "Scenario should store staff milestones.");
    }

    public void ReportsGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var highlights = new StaffLifeCycleService().BuildReportHighlights(scenario);

        Assert.True(highlights.Any(line => line.Contains("Top scout", StringComparison.OrdinalIgnoreCase)), "Staff reports should include top scout.");
        Assert.True(highlights.Any(line => line.Contains("Promotion", StringComparison.OrdinalIgnoreCase)), "Staff reports should include promotion candidates.");
    }

    public void ActionCenterItemsGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var items = new StaffLifeCycleService().BuildActionItems(scenario);

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Staff), "Staff life-cycle should create staff Action Center items.");
    }

    public void SaveLoadPreservesStaffLifeCycle()
    {
        var service = new SaveGameService();
        var scenario = CreateScenario().ScenarioSnapshot;
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha61-{Guid.NewGuid():N}.json");
        var saved = service.SaveCareer(
            scenario,
            Array.Empty<InboxMessage>(),
            scenario.StaffLifeCycleNews,
            new Dictionary<string, ActionCenterStatus>(),
            budget,
            path,
            "Alpha 6.1 Test Save");

        Assert.True(saved.Success, saved.Message);
        var loaded = service.LoadFromFile(path, RulebookPresets.CreateJuniorMajor());
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.StaffCareerSummaries.Count, loaded.SaveGame!.ScenarioSnapshot.StaffCareerSummaries.Count);
        Assert.Equal(scenario.StaffMilestones.Count, loaded.SaveGame.ScenarioSnapshot.StaffMilestones.Count);
    }

    public void AlphaDesktopExposesStaffLifeCycleUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Staff Careers", StringComparison.Ordinal), "AlphaDesktop should expose staff career history.");
        Assert.True(source.Contains("Coaching Trees", StringComparison.Ordinal), "AlphaDesktop should expose coaching trees.");
        Assert.True(source.Contains("Scout History", StringComparison.Ordinal), "AlphaDesktop should expose scout history.");
        Assert.True(source.Contains("Development Staff History", StringComparison.Ordinal), "AlphaDesktop should expose development staff history.");
        Assert.True(source.Contains("Life stage:", StringComparison.Ordinal), "Staff profile should include life stage.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*StaffLifeCycle*.cs", SearchOption.TopDirectoryOnly)
                .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
                .Select(File.ReadAllText));

        Assert.False(text.Contains("HallOfFameService", StringComparison.Ordinal), "Alpha 6.1 should not build Hall of Fame logic.");
        Assert.False(text.Contains("AwardsVoting", StringComparison.Ordinal), "Alpha 6.1 should not build awards voting.");
        Assert.False(text.Contains("RetirementDecision", StringComparison.Ordinal), "Alpha 6.1 should not build retirement decisions.");
        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 6.1 should not add Godot.");
        Assert.False(text.Contains("BasicGameSimulator", StringComparison.Ordinal), "Alpha 6.1 should not change game simulation.");
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
