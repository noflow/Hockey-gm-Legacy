using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.World;

internal sealed class Alpha69PlayoffsChampionshipTests
{
    public void SafeDefaultPlayoffFormatIsTopEightBestOfSeven()
    {
        var format = new PlayoffService().FormatFromRulebook(null);

        Assert.Equal(PlayoffFormatType.Top8, format.FormatType);
        Assert.Equal(8, format.TeamsQualify);
        Assert.Equal(7, format.BestOf);
        Assert.True(format.IsEnabled, "Safe default playoff format should be enabled.");
    }

    public void BracketSeedsGeneratedFromStandings()
    {
        var prepared = PreparedPlayoffScenario();
        var result = new PlayoffService().EnsureBracket(prepared.Registry, prepared.Scenario);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Bracket is not null, "Bracket should be created.");
        Assert.Equal(prepared.Scenario.Organization.OrganizationId, result.Bracket!.Seeds.First().OrganizationId);
        Assert.True(result.Bracket.Seeds.Count is 2 or 4 or 8 or 16, "Seeds should be trimmed to a bracket-safe count.");
        Assert.True(result.Bracket.Rounds.First().Series.Count > 0, "First round should include series.");
    }

    public void PlayerTeamQualificationCreatesInboxMessage()
    {
        var prepared = PreparedPlayoffScenario();
        var result = new PlayoffService().EnsureBracket(prepared.Registry, prepared.Scenario);

        Assert.True(result.InboxItems.Any(item => item.Title.Contains("Playoff berth", StringComparison.Ordinal)), "Player team qualification should create an inbox item.");
        Assert.True(result.LeagueNews.Any(item => item.Description.Contains("qualified", StringComparison.OrdinalIgnoreCase)), "League news should announce the bracket.");
    }

    public void PlayoffGameUsesSimulationAndSeparateStats()
    {
        var prepared = PreparedPlayoffScenario();
        var scenario = prepared.Scenario with
        {
            PlayerStats = prepared.Scenario.PlayerStats.Select(stat => stat with { GamesPlayed = 12, Goals = 2, Assists = 3 }).ToArray()
        };

        var result = new PlayoffService().SimulateNextGame(prepared.Registry, scenario);

        Assert.True(result.Success, result.Message);
        Assert.True(result.GameRecaps.Count == 1, "Simulating a playoff game should create one recap.");
        Assert.True(result.ScenarioSnapshot.Playoffs.PlayoffGameRecaps.Count == 1, "Playoff recap should be stored separately.");
        Assert.True(result.ScenarioSnapshot.Playoffs.PlayoffSkaterStats.Any(stat => stat.GamesPlayed > 0), "Playoff skater stats should update.");
        Assert.True(result.ScenarioSnapshot.PlayerStats.All(stat => stat.GamesPlayed == 12), "Regular-season player stats should remain unchanged.");
    }

    public void SeriesCanAdvanceAndChampionIsRecorded()
    {
        var prepared = PreparedPlayoffScenario();
        var result = new PlayoffService().SimulateFullPlayoffs(prepared.Registry, prepared.Scenario);

        Assert.True(result.Success, result.Message);
        Assert.Equal(PlayoffStatus.Completed, result.ScenarioSnapshot.Playoffs.Bracket!.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ScenarioSnapshot.Playoffs.Bracket.ChampionTeamName), "Champion should be recorded.");
        Assert.True(result.ScenarioSnapshot.Playoffs.Bracket.Results.Count > 0, "Completed series should be recorded.");
        Assert.True(result.ScenarioSnapshot.OrganizationSeasonHistory.Any(history => history.SeasonYear == result.ScenarioSnapshot.Season.Year), "Organization playoff history should be recorded.");
    }

    public void PlayerTimelineRecordsPlayoffDebut()
    {
        var prepared = PreparedPlayoffScenario();
        var result = new PlayoffService().SimulateNextGame(prepared.Registry, prepared.Scenario);

        Assert.True(result.ScenarioSnapshot.CareerTimeline.Entries.Any(entry => entry.Title.Contains("Playoff debut", StringComparison.Ordinal)), "Player playoff debut should be tracked in career timeline.");
    }

    public void DailyPipelineCreatesPlayoffBracketAfterRegularSeason()
    {
        var prepared = PreparedPlayoffScenario();
        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(prepared.Registry, prepared.Scenario);

        Assert.True(result.ScenarioSnapshot.Playoffs.Bracket is not null, "Daily pipeline should create bracket after regular-season schedule is complete.");
        Assert.True(result.ScenarioSnapshot.Playoffs.PlayoffGameRecaps.Count > 0, "Daily pipeline should simulate a playoff game.");
        Assert.True(result.ScenarioSnapshot.Playoffs.PlayoffLeagueNews.Any(item => item.Description.Contains("qualified", StringComparison.OrdinalIgnoreCase)), "Daily pipeline should store playoff league news for the desktop feed.");
    }

    public void SaveLoadPreservesPlayoffState()
    {
        var prepared = PreparedPlayoffScenario();
        var playoff = new PlayoffService().SimulateNextGame(prepared.Registry, prepared.Scenario).ScenarioSnapshot;
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha69-{Guid.NewGuid():N}.json");
        var save = new SaveGameService().SaveCareer(
            playoff,
            Array.Empty<InboxMessage>(),
            playoff.Playoffs.PlayoffLeagueNews,
            new Dictionary<string, ActionCenterStatus>(),
            new BudgetOverviewService().Build(playoff, prepared.Registry.Rulebook ?? RulebookPresets.CreateJuniorMajor()),
            path,
            "Alpha 6.9 Playoff Save");

        Assert.True(save.Success, save.Message);
        var load = new SaveGameService().LoadFromFile(path, prepared.Registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());

        Assert.True(load.Success, load.Message);
        Assert.True(load.SaveGame!.ScenarioSnapshot.Playoffs.Bracket is not null, "Loaded save should include playoff bracket.");
        Assert.Equal(playoff.Playoffs.PlayoffGameRecaps.Count, load.SaveGame.ScenarioSnapshot.Playoffs.PlayoffGameRecaps.Count);
    }

    public void AlphaDesktopExposesPlayoffViews()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("new WorkspaceScreen(\"Playoffs\", CreateTextScreen(\"Playoffs\"))", StringComparison.Ordinal), "Season workspace should expose Playoffs.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Playoff Archive\", CreateTextScreen(\"Playoff Archive\"))", StringComparison.Ordinal), "Reports should expose playoff archive.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Champions\", CreateTextScreen(\"Champions\"))", StringComparison.Ordinal), "Reports should expose champions.");
        Assert.True(source.Contains("PlayoffDashboardSummary", StringComparison.Ordinal), "Dashboard should expose playoff status.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join(Environment.NewLine, new[]
        {
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "PlayoffModels.cs")),
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "PlayoffService.cs"))
        });

        Assert.False(text.Contains("DbContext", StringComparison.OrdinalIgnoreCase), "Playoffs should not add database persistence.");
        Assert.False(text.Contains("Cloud", StringComparison.OrdinalIgnoreCase), "Playoffs should not add cloud services.");
        Assert.False(text.Contains("ThreeWay", StringComparison.OrdinalIgnoreCase), "Playoffs should not add unrelated trade systems.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot Scenario) PreparedPlayoffScenario()
    {
        var created = new MultiLeagueCareerService().CreateScenario(new MultiLeagueCareerService().SelectLeagueAndTeam(LeagueExperience.Nhl, "org-seattle-cascades"));
        var scenario = new TacticsService().EnsureTactics(new GameUsageService().EnsureGameUsage(new LineChemistryService().EnsureChemistry(created.ScenarioSnapshot)));
        var teams = SeasonFrameworkService.LeagueTeams(scenario).Take(6).ToArray();
        var orderedTeams = teams
            .OrderByDescending(team => team.OrganizationId == scenario.Organization.OrganizationId)
            .ThenBy(team => team.TeamName, StringComparer.Ordinal)
            .ToArray();
        var standings = new StandingsTable(
            scenario.Season.LeagueId,
            orderedTeams.Select((team, index) => new TeamStanding(
                team.OrganizationId,
                team.TeamName,
                60,
                42 - index,
                14 + index,
                4,
                88 - (index * 3),
                210 - (index * 6),
                150 + (index * 7))).ToArray());
        var stats = new SeasonStatsService();
        var completeGame = new ScheduledGame("alpha69-complete-game", scenario.CurrentDate.AddDays(-1), orderedTeams[0].OrganizationId, orderedTeams[1].OrganizationId)
            .Complete(new GameResult("alpha69-complete-game", 4, 2, orderedTeams[0].OrganizationId, orderedTeams[1].OrganizationId));
        scenario = WithDate(scenario, ScheduleEngine.PlayoffsBeginDate(scenario.Season));
        scenario = scenario with
        {
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true, SeasonBegun: true),
            Season = scenario.Season with { Status = SeasonStatus.Active, CurrentPhase = SeasonPhase.Playoffs },
            Schedule = new GameSchedule("alpha69-complete-schedule", scenario.Season.LeagueId, new[] { completeGame }),
            Standings = standings,
            TeamStats = stats.CreateTeamStats(teams),
            PlayerStats = stats.CreatePlayerStats(scenario.AlphaSnapshot),
            GoalieStats = stats.CreateGoalieStats(scenario.AlphaSnapshot),
            Playoffs = PlayoffState.Empty
        };
        scenario.Validate();
        return (created.Registry, scenario);
    }

    private static NewGmScenarioSnapshot WithDate(NewGmScenarioSnapshot scenario, DateOnly date)
    {
        var world = scenario.AlphaSnapshot.WorldState with { Clock = new WorldClock(new WorldDate(date)) };
        var season = scenario.Season with
        {
            CurrentDate = new SeasonDate(date),
            CurrentPhase = scenario.Season.PhaseOn(date)
        };
        return scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { WorldState = world, Season = season },
            Season = season
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var marker = Path.Combine(directory.FullName, "HockeyGmLegacy.slnx");
            if (File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
