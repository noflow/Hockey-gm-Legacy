using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

internal sealed class Alpha614MediaNewsTests
{
    public void MediaArticleGeneratedFromMajorTrade()
    {
        var scenario = Scenario();
        var trade = TradeTransaction(scenario);
        var feed = new MediaService().BuildFeed(scenario, new[] { trade });

        Assert.True(feed.Articles.Any(article => article.ArticleType == MediaArticleType.Trade && article.RelatedTransactionId == trade.TransactionId), "Major trade should generate media article.");
    }

    public void MediaArticleGeneratedFromDraftPick()
    {
        var baseScenario = Scenario();
        var scenario = baseScenario with
        {
            DraftPickHistory = new[]
            {
                new DraftPickHistory(
                    baseScenario.Season.Year,
                    1,
                    12,
                    "person-media-draft-pick",
                    "Mason Clark",
                    RosterPosition.Center,
                    "Brandon U18",
                    "Top-six center projection",
                    ScoutingConfidenceLevel.High,
                    "Strong viewings.",
                    "Drafted",
                    0,
                    0,
                    "Not a goalie.",
                    DraftPickOutcome.Developing,
                    "Too early to evaluate.")
            }
        };
        var feed = new MediaService().BuildFeed(scenario);

        Assert.True(feed.Articles.Any(article => article.ArticleType == MediaArticleType.Draft && article.PersonNames.Contains("Mason Clark")), "Draft pick should generate draft media article.");
    }

    public void MediaArticleGeneratedFromMilestone()
    {
        var scenario = Scenario();
        var player = scenario.AlphaSnapshot.Roster.Players.First();
        var name = scenario.AlphaSnapshot.People.First(person => person.PersonId == player.PersonId).Identity.DisplayName;
        scenario = scenario with
        {
            PlayerMilestones = scenario.PlayerMilestones.Concat(new[]
            {
                new PlayerMilestone("media-test-milestone", player.PersonId, name, PlayerMilestoneType.Games100, scenario.CurrentDate, scenario.Season.Year, $"{name} became a top story after reaching 100 games.", true)
            }).ToArray()
        };
        var feed = new MediaService().BuildFeed(scenario);

        Assert.True(feed.Articles.Any(article => article.ArticleType == MediaArticleType.Milestone && article.PersonIds.Contains(player.PersonId)), "Notable milestone should generate media article.");
    }

    public void RumorArticleGenerated()
    {
        var feed = new MediaService().BuildFeed(Scenario());

        Assert.True(feed.Articles.Any(article => article.ArticleType == MediaArticleType.Rumor && article.RumorConfidence != MediaRumorConfidence.None), "Media feed should include a simple rumor article.");
    }

    public void ArticleHasRequiredMetadata()
    {
        var scenario = Scenario();
        var article = new MediaService().BuildFeed(scenario, new[] { TradeTransaction(scenario) }).Articles.First();

        Assert.True(!string.IsNullOrWhiteSpace(article.Headline), "Article needs headline.");
        Assert.True(article.Date.Year > 2000, "Article needs date.");
        Assert.True(!string.IsNullOrWhiteSpace(article.Source.Name), "Article needs source.");
        Assert.True(article.Importance >= MediaImportance.Routine, "Article needs importance.");
        Assert.True(Enum.IsDefined(article.Tone), "Article needs tone.");
    }

    public void MediaFeedFiltersByTypeTeamAndPlayer()
    {
        var scenario = Scenario();
        var trade = TradeTransaction(scenario);
        var feed = new MediaService().BuildFeed(scenario, new[] { trade });

        Assert.True(feed.Query(articleType: MediaArticleType.Trade).All(article => article.ArticleType == MediaArticleType.Trade), "Type filter should work.");
        Assert.True(feed.Query(teamId: scenario.Organization.OrganizationId).Any(), "Team filter should find player-team items.");
        Assert.True(feed.Query(personId: trade.PersonId).Any(), "Player filter should find transaction person.");
    }

    public void DashboardAndDossierExposeMedia()
    {
        var scenario = Scenario();
        var playerId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);
        var desktop = AlphaDesktopSource();

        Assert.True(desktop.Contains("Top Headline", StringComparison.Ordinal), "Dashboard should expose top headline.");
        Assert.True(desktop.Contains("Media / News", StringComparison.Ordinal), "AlphaDesktop should expose Media / News screen.");
        Assert.True(dossier.Sections.Any(section => section.Title == "Media Coverage"), "Player dossier should expose related media.");
    }

    public void LeagueNewsAndMediaRemainSeparate()
    {
        var scenario = Scenario();
        var trade = TradeTransaction(scenario);
        var feed = new MediaService().BuildFeed(scenario, new[] { trade });

        Assert.True(feed.Articles.Any(article => article.RelatedTransactionId == trade.TransactionId), "Media can link to league transaction.");
        Assert.True(trade.GetType() == typeof(LeagueTransaction), "League News remains LeagueTransaction wire.");
        Assert.True(feed.Articles.All(article => article.GetType() == typeof(MediaArticle)), "Media feed remains article layer.");
    }

    public void SaveLoadPreservesMediaFeed()
    {
        var baseScenario = Scenario();
        var scenario = new MediaService().EnsureMediaFeed(baseScenario, new[] { TradeTransaction(baseScenario) });
        var budget = new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var service = new SaveGameService();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-alpha614-{Guid.NewGuid():N}.json");

        var saved = service.SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = service.LoadFromFile(path, scenario.LeagueProfile.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.MediaFeed.Articles.Count, loaded.SaveGame!.ScenarioSnapshot.MediaFeed.Articles.Count);
    }

    public void NoRealBrandsSocialMediaOrGodot()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "MediaService.cs"));
        var feed = new MediaService().BuildFeed(Scenario());

        Assert.False(feed.Sources.Any(source => source.Name.Contains("ESPN", StringComparison.OrdinalIgnoreCase)
            || source.Name.Contains("TSN", StringComparison.OrdinalIgnoreCase)
            || source.Name.Contains("Sportsnet", StringComparison.OrdinalIgnoreCase)), "Media sources should be fictional.");
        Assert.False(source.Contains("SocialMedia", StringComparison.OrdinalIgnoreCase), "Media v1 should not add social media engine.");
        Assert.False(source.Contains("PressConference", StringComparison.OrdinalIgnoreCase), "Media v1 should not add press conferences.");
        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Media v1 should not reference Godot.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

    private static LeagueTransaction TradeTransaction(NewGmScenarioSnapshot scenario)
    {
        var player = scenario.AlphaSnapshot.Roster.Players.First();
        var name = scenario.AlphaSnapshot.People.First(person => person.PersonId == player.PersonId).Identity.DisplayName;
        return new LeagueTransaction(
            "media-test-trade",
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 13, 0, 0, TimeSpan.Zero),
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            player.PersonId,
            name,
            LeagueTransactionType.TradeCompleted,
            LeagueNewsCategory.League,
            $"{scenario.Organization.Name} completed a notable trade involving {name}.");
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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
