using LegacyEngine.Integration;
using LegacyEngine.Rosters;

internal sealed class Alpha615AwardsRecordsTests
{
    public void MvpAwardGenerated()
    {
        var scenario = AwardScenario();
        var updated = new AwardService().EnsureAwards(scenario);

        Assert.True(updated.AwardHistory.Awards.Any(award => award.AwardType == AwardType.Mvp), "MVP award should be generated.");
    }

    public void RookieAwardGenerated()
    {
        var updated = new AwardService().EnsureAwards(AwardScenario());

        Assert.True(updated.AwardHistory.Awards.Any(award => award.AwardType == AwardType.RookieOfTheYear), "Rookie award should be generated.");
    }

    public void GoalieAwardGenerated()
    {
        var updated = new AwardService().EnsureAwards(AwardScenario());

        Assert.True(updated.AwardHistory.Awards.Any(award => award.AwardType == AwardType.BestGoalie), "Goalie award should be generated.");
    }

    public void CoachAndGmAwardsGenerated()
    {
        var updated = new AwardService().EnsureAwards(AwardScenario());

        Assert.True(updated.AwardHistory.Awards.Any(award => award.AwardType == AwardType.CoachOfTheYear), "Coach award placeholder should be generated.");
        Assert.True(updated.AwardHistory.Awards.Any(award => award.AwardType == AwardType.GmOfTheYear), "GM award placeholder should be generated.");
    }

    public void AwardHistoryStored()
    {
        var updated = new AwardService().EnsureAwards(AwardScenario());

        Assert.True(updated.AwardHistory.Awards.Count >= 4, "Award history should store generated winners.");
        updated.AwardHistory.Validate();
    }

    public void WinnerGetsTimelineEntry()
    {
        var updated = new AwardService().EnsureAwards(AwardScenario());
        var winner = updated.AwardHistory.Awards.First(award => award.AwardType == AwardType.Mvp).Winner;

        Assert.True(updated.CareerTimeline.ForPerson(winner.RecipientId).Any(entry => entry.EntryType == CareerTimelineEntryType.Award), "Award winner should receive career timeline entry.");
    }

    public void MediaArticleGeneratedForMajorAward()
    {
        var updated = new AwardService().EnsureAwards(AwardScenario());
        var feed = new MediaService().BuildFeed(updated);

        Assert.True(feed.Articles.Any(article => article.ArticleType == MediaArticleType.Award), "Major award should generate media article.");
    }

    public void RecordBookCreated()
    {
        var updated = new RecordService().EnsureRecordBook(AwardScenario());

        Assert.True(updated.RecordBook.Records.Count > 0, "Record book should be created from stats.");
    }

    public void RecordUpdatesAfterStats()
    {
        var updated = new RecordService().EnsureRecordBook(AwardScenario());

        Assert.True(updated.RecordBook.Records.Any(record => record.RecordType == RecordType.Points && record.Value >= 90), "Points record should reflect current stats.");
    }

    public void BrokenRecordCreatesHistoryAndMedia()
    {
        var scenario = AwardScenario();
        var previous = new RecordEntry(
            $"record:{scenario.LeagueProfile.Identity.LeagueId}:{RecordScope.SingleSeason}:{RecordType.Points}",
            RecordType.Points,
            RecordScope.SingleSeason,
            scenario.Season.Year - 1,
            scenario.CurrentDate.AddYears(-1),
            "old-holder",
            "Old Holder",
            "Player",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            40,
            "Old points mark.",
            false);
        scenario = scenario with { RecordBook = new RecordBook(new[] { previous }) };

        var updated = new RecordService().EnsureRecordBook(scenario);
        var feed = new MediaService().BuildFeed(updated);

        Assert.True(updated.RecordBook.Records.Any(record => record.RecordType == RecordType.Points && record.WasBrokenThisUpdate), "Broken record should be marked.");
        Assert.True(updated.CareerTimeline.Entries.Any(entry => entry.Title.Contains("record", StringComparison.OrdinalIgnoreCase)), "Broken record should create history.");
        Assert.True(feed.Articles.Any(article => article.ArticleType == MediaArticleType.Record), "Broken record should generate media article.");
    }

    public void PlayerDossierExposesAwardsAndRecords()
    {
        var scenario = new RecordService().EnsureRecordBook(new AwardService().EnsureAwards(AwardScenario()));
        var playerId = scenario.PlayerStats.OrderByDescending(stat => stat.Points).First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);

        Assert.True(dossier.Sections.Any(section => section.Title == "Awards & Records"), "Player dossier should expose awards and records.");
    }

    public void ReportsHistoryExposesAwardsAndRecords()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("new WorkspaceScreen(\"Awards\"", StringComparison.Ordinal), "Reports / History should expose Awards.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Record Book\"", StringComparison.Ordinal), "Reports / History should expose Record Book.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Team Records\"", StringComparison.Ordinal), "Reports / History should expose Team Records.");
        Assert.True(source.Contains("new WorkspaceScreen(\"League Records\"", StringComparison.Ordinal), "Reports / History should expose League Records.");
    }

    public void SaveLoadPreservesAwardsAndRecords()
    {
        var scenario = new RecordService().EnsureRecordBook(new AwardService().EnsureAwards(AwardScenario()));
        var budget = new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-alpha615-{Guid.NewGuid():N}.json");
        var service = new SaveGameService();

        var saved = service.SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = service.LoadFromFile(path, scenario.LeagueProfile.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.AwardHistory.Awards.Count, loaded.SaveGame!.ScenarioSnapshot.AwardHistory.Awards.Count);
        Assert.Equal(scenario.RecordBook.Records.Count, loaded.SaveGame.ScenarioSnapshot.RecordBook.Records.Count);
    }

    public void NoHallOfFameOrGodotDependency()
    {
        var root = FindRepositoryRoot();
        var awardSource = File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "AwardService.cs"));
        var recordSource = File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "RecordService.cs"));

        Assert.False(awardSource.Contains("HallOfFame", StringComparison.OrdinalIgnoreCase), "Awards v1 should not add Hall of Fame.");
        Assert.False(recordSource.Contains("HallOfFame", StringComparison.OrdinalIgnoreCase), "Records v1 should not add Hall of Fame.");
        Assert.False(awardSource.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Awards v1 should not reference Godot.");
        Assert.False(recordSource.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Records v1 should not reference Godot.");
    }

    private static NewGmScenarioSnapshot AwardScenario()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var roster = scenario.AlphaSnapshot.Roster.Players.Take(5).ToArray();
        var people = scenario.AlphaSnapshot.People;
        var skaters = roster.Take(4).ToArray();
        var goalie = scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.Position == RosterPosition.Goalie) ?? roster.Last();
        var stats = skaters.Select((player, index) => new PlayerSeasonStatLine(
            player.PersonId,
            people.First(person => person.PersonId == player.PersonId).Identity.DisplayName,
            60,
            index == 0 ? 45 : 10 + index,
            index == 0 ? 48 : 20 - index,
            index == 0 ? 22 : 4,
            12)).ToArray();
        var goalieStats = new[]
        {
            new GoalieSeasonStatLine(
                goalie.PersonId,
                people.First(person => person.PersonId == goalie.PersonId).Identity.DisplayName,
                42,
                29,
                13,
                92,
                1180,
                5)
        };
        var standings = new StandingsTable(scenario.Season.LeagueId, new[]
        {
            new TeamStanding(scenario.Organization.OrganizationId, scenario.Organization.Name, 68, 43, 20, 5, 91, 242, 191),
            new TeamStanding("org-test-rival", "Rival Club", 68, 32, 29, 7, 71, 210, 205)
        });

        return scenario with
        {
            CurrentDraftClassProfile = scenario.CurrentDraftClassProfile,
            PlayerStats = stats,
            GoalieStats = goalieStats,
            Standings = standings,
            TeamStats = new[]
            {
                new TeamSeasonStatLine(scenario.Organization.OrganizationId, scenario.Organization.Name, 68, 242, 191)
            }
        };
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
