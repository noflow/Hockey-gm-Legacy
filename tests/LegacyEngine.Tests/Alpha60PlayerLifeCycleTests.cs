using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha60PlayerLifeCycleTests
{
    public void LifeStageGeneration()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerCareerStates.Count > 0, "Player life-cycle states should be generated.");
        Assert.True(scenario.PlayerCareerStates.Any(state => state.LifeStage is PlayerLifeStage.Junior or PlayerLifeStage.Prospect or PlayerLifeStage.DevelopingProfessional or PlayerLifeStage.NhlRegular or PlayerLifeStage.Prime or PlayerLifeStage.Veteran), "Players should receive recognizable life stages.");
    }

    public void CareerProgressionGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerCareerStates.All(state => !string.IsNullOrWhiteSpace(state.Summary)), "Career states should explain the current phase.");
        Assert.True(scenario.PlayerCareerSummaries.Any(summary => summary.CareerPhase != default || !string.IsNullOrWhiteSpace(summary.CareerSummaryText)), "Career summaries should include progression context.");
    }

    public void MilestonesGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerMilestones.Count > 0, "Life-cycle service should track milestones.");
        Assert.True(scenario.PlayerMilestones.Any(milestone => milestone.MilestoneType is PlayerMilestoneType.Drafted or PlayerMilestoneType.SignedFirstContract), "Draft or first-contract milestones should be created from existing career data.");
    }

    public void ReputationGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerCareerStates.All(state => !string.IsNullOrWhiteSpace(state.Reputation.Summary)), "Player reputation should include a readable summary.");
        Assert.True(scenario.PlayerCareerStates.Any(state => state.Reputation.Category is PlayerReputationCategory.Prospect or PlayerReputationCategory.Reliable or PlayerReputationCategory.VeteranLeader or PlayerReputationCategory.DecliningVeteran), "Players should receive reputation categories.");
    }

    public void LegacyScoreGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerCareerStates.All(state => state.LegacyScore >= 0), "Legacy score should be non-negative.");
        Assert.True(scenario.PlayerCareerSummaries.Any(summary => summary.LegacyScore > 0), "At least one tracked player should have a non-zero legacy score.");
    }

    public void CareerStoryGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerCareerSummaries.All(summary => summary.CareerStory.Count > 0), "Every career summary should include story lines.");
        Assert.True(scenario.PlayerCareerSummaries.Any(summary => summary.CareerStory.Any(line => line.Contains("Current story", StringComparison.Ordinal))), "Career story should include a current-story line.");
    }

    public void TimelineUpdatedFromMilestones()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var milestone = scenario.PlayerMilestones.First();

        Assert.True(scenario.CareerTimeline.ForPerson(milestone.PersonId).Any(entry => entry.EntryId.Contains(milestone.MilestoneId, StringComparison.Ordinal)), "Milestones should become career timeline entries.");
    }

    public void AchievementsGenerated()
    {
        var created = CreateScenario();
        var scenario = created.ScenarioSnapshot;
        var firstPlayer = scenario.AlphaSnapshot.Roster.Players.First();
        var firstName = scenario.AlphaSnapshot.People.First(person => person.PersonId == firstPlayer.PersonId).Identity.DisplayName;
        var baseStats = scenario.PlayerStats.Count == 0
            ? new[] { new PlayerSeasonStatLine(firstPlayer.PersonId, firstName) }
            : scenario.PlayerStats;
        var statLines = baseStats
            .Select(stat => stat.PersonId == firstPlayer.PersonId ? stat.ApplyGame(3, 2, 4) : stat)
            .ToArray();
        scenario = new PlayerLifeCycleService().EnsureLifeCycle(scenario with { PlayerStats = statLines }, created.Registry);

        Assert.True(scenario.PlayerAchievements.Any(achievement => achievement.PersonId == firstPlayer.PersonId), "Life-cycle service should track achievements when career context warrants it.");
    }

    public void StaffInfluenceGenerated()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerCareerSummaries.Any(summary => summary.InfluentialStaff.Count > 0), "Player stories should include staff influence.");
    }

    public void ReportsIncludeLifeCycleHighlights()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var highlights = new PlayerLifeCycleService().BuildMonthlyHighlights(scenario);

        Assert.True(highlights.Count > 0, "Life-cycle service should produce report highlights.");
    }

    public void LeagueNewsGeneratedForNotableStories()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerLifeCycleNews.Count > 0, "Notable player milestones should create limited league news.");
        Assert.True(scenario.PlayerLifeCycleNews.All(news => news.TransactionType == LeagueTransactionType.PlayerMilestone), "Life-cycle news should be classified as player milestone news.");
    }

    public void PlayerDossierIncludesCareerLifeCycle()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var personId = scenario.PlayerCareerSummaries.First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, personId);
        var text = string.Join("\n", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Life stage:", StringComparison.Ordinal), "Dossier should show life stage.");
        Assert.True(text.Contains("Legacy score:", StringComparison.Ordinal), "Dossier should show legacy score.");
        Assert.True(text.Contains("Career story:", StringComparison.Ordinal), "Dossier should show career story.");
        Assert.False(text.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase), "Dossier must not expose hidden current ability.");
        Assert.False(text.Contains("Potential =", StringComparison.OrdinalIgnoreCase), "Dossier must not expose hidden potential.");
    }

    public void ActionCenterIncludesCareerStories()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var summary = scenario.PlayerCareerSummaries.First() with { CareerPhase = PlayerCareerPhase.Breakout };
        scenario = scenario with
        {
            PlayerCareerSummaries = scenario.PlayerCareerSummaries
                .Select(item => item.PersonId == summary.PersonId ? summary : item)
                .ToArray()
        };

        var items = new PlayerLifeCycleService().BuildActionItems(scenario);

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.PlayerDevelopment), "Career stories should produce Action Center items when meaningful.");
    }

    public void HistoryStoresLifeCycleRecords()
    {
        var scenario = CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerCareerStates.Count > 0, "Scenario history should store career states.");
        Assert.True(scenario.PlayerMilestones.Count > 0, "Scenario history should store milestones.");
        Assert.True(scenario.PlayerCareerSummaries.Count > 0, "Scenario history should store career summaries.");
    }

    public void AlphaDesktopExposesLifeCycleUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Career Milestones", StringComparison.Ordinal), "AlphaDesktop should expose career milestone history.");
        Assert.True(source.Contains("Player Stories", StringComparison.Ordinal), "AlphaDesktop should expose player stories.");
        Assert.True(source.Contains("BuildPlayerStoriesReport", StringComparison.Ordinal), "AlphaDesktop should build player story reports.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*LifeCycle*.cs", SearchOption.TopDirectoryOnly)
                .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
                .Select(File.ReadAllText));

        Assert.False(text.Contains("JerseyRetirement", StringComparison.Ordinal), "Alpha 6.0 should not build jersey retirement.");
        Assert.False(text.Contains("AwardsVoting", StringComparison.Ordinal), "Alpha 6.0 should not build awards voting.");
        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 6.0 should not add Godot.");
        Assert.False(text.Contains("BasicGameSimulator", StringComparison.Ordinal), "Alpha 6.0 should not change game simulation.");
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
