using LegacyEngine.Integration;

internal sealed class Alpha613LivingStoryEngineTests
{
    public void PlayerStoriesGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.Stories.Any(story => story.SubjectKind == "Player"), "Player stories should be generated.");
        Assert.True(scenario.Stories.Where(story => story.SubjectKind == "Player").All(story => story.StoryType != StoryType.Unknown), "Player stories should have known types.");
    }

    public void GmAndOrganizationStoriesGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.Stories.Any(story => story.SubjectKind == "GM"), "GM story should be generated.");
        Assert.True(scenario.Stories.Any(story => story.SubjectKind == "Organization"), "Organization story should be generated.");
        Assert.True(scenario.Stories.Any(story => story.SubjectKind == "Owner"), "Owner story should be generated.");
    }

    public void StoryProgressionRecordsEvent()
    {
        var service = new StoryService();
        var story = Scenario().Stories.First();
        var storyEvent = new StoryEvent(
            "story-event:test-progress",
            story.LastUpdated.AddDays(1),
            "Camp breakthrough",
            "The staff connected several camp notes into one visible story update.",
            null,
            story.SubjectKind == "Player" ? story.SubjectId : null,
            story.OrganizationId,
            StoryImportance.Major);

        var progressed = service.ProgressStory(story, storyEvent, "Rising momentum", StoryStatus.Rising, 12, "Story momentum improved after a meaningful staff note.");

        Assert.True(progressed.CurrentArc.Progress > story.CurrentArc.Progress, "Story progress should increase.");
        Assert.True(progressed.CurrentArc.Events.Any(item => item.StoryEventId == storyEvent.StoryEventId), "Story event should be recorded.");
        Assert.True(progressed.Summary.KeyMoments.Any(moment => moment.Contains("Camp breakthrough", StringComparison.Ordinal)), "Story key moments should include progress event.");
    }

    public void StorySummariesAreReadable()
    {
        var scenario = Scenario();

        Assert.True(scenario.Stories.All(story => !string.IsNullOrWhiteSpace(story.Summary.Headline)), "Stories should have headlines.");
        Assert.True(scenario.Stories.All(story => !string.IsNullOrWhiteSpace(story.Summary.ShortSummary)), "Stories should have readable summaries.");
        Assert.True(scenario.Stories.All(story => story.Summary.KeyMoments.Count > 0), "Stories should include key moments.");
    }

    public void LeagueNewsReferencesStories()
    {
        var news = new StoryService().BuildLeagueNews(Scenario(), maxItems: 3);

        Assert.True(news.Count > 0 && news.Count <= 3, "Story league news should be present and limited.");
        Assert.True(news.All(item => item.TransactionType == LeagueTransactionType.StoryUpdate), "Story news should use story update type.");
        Assert.True(news.All(item => !string.IsNullOrWhiteSpace(item.PersonName) && !string.IsNullOrWhiteSpace(item.Description)), "Story news should be readable.");
    }

    public void PlayerDossierIncludesStorySection()
    {
        var scenario = Scenario();
        var playerId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);

        Assert.True(dossier.Sections.Any(section => section.Title == "Stories"), "Player dossier should include story section.");
        Assert.True(dossier.Sections.Single(section => section.Title == "Stories").Lines.Any(line => line.Contains("Current Story", StringComparison.Ordinal)), "Story section should show current story.");
    }

    public void OrganizationReportsAndActionCenterIncludeStories()
    {
        var scenario = Scenario();
        var storyItems = new StoryService().BuildExecutiveReportItems(scenario);
        var actions = new StoryService().BuildActionCenterItems(scenario);
        var desktop = AlphaDesktopSource();
        var reportSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "ExecutiveReportService.cs"));

        Assert.True(storyItems.ContainsKey("Top Story"), "Executive report story items should include top story.");
        Assert.True(reportSource.Contains("Living Stories", StringComparison.Ordinal), "Executive reports should include Living Stories section.");
        Assert.True(actions.Any(item => item.Category == ActionCenterCategory.Storyline), "Story action items should use Storyline category.");
        Assert.True(desktop.Contains("Current Organization Story", StringComparison.Ordinal), "Organization Command Center should expose current organization story.");
    }

    public void MonthlyReportsReferenceStories()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var result = new MonthlyGmSummaryService().Generate(scenarioResult.Registry, scenarioResult.ScenarioSnapshot);

        Assert.True(result.Summary.Sections.Any(section => section.Title == "Storylines"), "Monthly summary should include storylines section.");
        Assert.True(result.Summary.ExecutiveNarrative.Contains("Top story", StringComparison.Ordinal), "Monthly narrative should reference top story.");
    }

    public void SaveLoadPreservesStories()
    {
        var scenario = Scenario();
        var budget = new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var service = new SaveGameService();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-alpha613-{Guid.NewGuid():N}.json");

        var saved = service.SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = service.LoadFromFile(path, scenario.LeagueProfile.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.Stories.Count, loaded.SaveGame!.ScenarioSnapshot.Stories.Count);
        Assert.Equal(scenario.Stories.First().StoryId, loaded.SaveGame.ScenarioSnapshot.Stories.First().StoryId);
    }

    public void NoForbiddenSystemsAdded()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "StoryService.cs"));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Story engine should not reference Godot.");
        Assert.False(source.Contains("MediaEngine", StringComparison.OrdinalIgnoreCase), "Story engine should not add a media engine.");
        Assert.False(source.Contains("SocialMedia", StringComparison.OrdinalIgnoreCase), "Story engine should not add social media.");
        Assert.False(source.Contains("Article", StringComparison.OrdinalIgnoreCase), "Story engine should not add article generation.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        new StoryService().EnsureStories(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);

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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
