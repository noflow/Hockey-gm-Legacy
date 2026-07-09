using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha26GameRecapStatsPolishTests
{
    public void CompletedGameCreatesRecap()
    {
        var result = SimulatePlayerTeamGame();

        Assert.True(result.ScenarioSnapshot.GameRecaps.Count == 1, "Completed game should create one recap.");
        Assert.True(result.GameRecaps.Count == 1, "Simulation result should return the created recap.");
    }

    public void RecapIncludesScoreWinnerTeamsAndDate()
    {
        var result = SimulatePlayerTeamGame();
        var recap = result.ScenarioSnapshot.GameRecaps.Single();

        Assert.Equal(result.ScenarioSnapshot.CurrentDate, recap.Date);
        Assert.False(string.IsNullOrWhiteSpace(recap.BoxScore.Home.TeamName), "Home team should be named.");
        Assert.False(string.IsNullOrWhiteSpace(recap.BoxScore.Away.TeamName), "Away team should be named.");
        Assert.False(string.IsNullOrWhiteSpace(recap.BoxScore.FinalScore), "Final score should be present.");
        Assert.False(string.IsNullOrWhiteSpace(recap.WinnerTeam), "Winner should be present.");
    }

    public void ThreeStarsGenerated()
    {
        var result = SimulatePlayerTeamGame();
        var recap = result.ScenarioSnapshot.GameRecaps.Single();

        Assert.True(recap.ThreeStars.Count == 3, "Recap should include three stars.");
        Assert.True(recap.ThreeStars.All(star => !string.IsNullOrWhiteSpace(star)), "Three stars should be readable.");
    }

    public void GameRecapInboxCreatedForPlayerTeam()
    {
        var result = SimulatePlayerTeamGame();

        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.GamePlayed), "Player team game should create a game recap inbox item.");
        Assert.True(result.InboxItems.Any(item => item.Summary.Contains("Record after game", StringComparison.Ordinal)), "Inbox recap should include record after game.");
        Assert.True(result.InboxItems.Any(item => item.Summary.Contains("Top performer", StringComparison.Ordinal)), "Inbox recap should include top performer.");
    }

    public void LeagueWideGamesDoNotSpamInbox()
    {
        var ready = ScenarioWithLeagueGameTomorrow();
        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.ScenarioSnapshot.GameRecaps.Count == 1, "League game can still be recapped internally.");
        Assert.False(result.InboxItems.Any(item => item.EventType == LegacyEventType.GamePlayed), "League-wide games should not create player inbox spam.");
    }

    public void StandingsSortedByPoints()
    {
        var table = new StandingsTable("league-test", new[]
        {
            new TeamStanding("a", "A", 3, 1, 2, 0, 2, 8, 9),
            new TeamStanding("b", "B", 3, 3, 0, 0, 6, 11, 4),
            new TeamStanding("c", "C", 3, 2, 1, 0, 4, 9, 6)
        });

        Assert.Equal("B", table.OrderedTeams().First().TeamName);
    }

    public void TeamLeadersGenerated()
    {
        var result = SimulatePlayerTeamGame();
        var leaders = new SeasonStatsPolishService().BuildLeaders(result.ScenarioSnapshot);

        Assert.True(leaders.TeamLeaders.Count > 0, "Team leaders should be generated.");
        Assert.True(leaders.TeamLeaders.Any(leader => leader.Category == "Goals For"), "Team leaders should include goals for.");
    }

    public void LeagueLeadersGenerated()
    {
        var result = SimulatePlayerTeamGame();
        var leaders = new SeasonStatsPolishService().BuildLeaders(result.ScenarioSnapshot);

        Assert.True(leaders.LeagueLeaders.Count > 0, "League leaders should be generated.");
        Assert.True(leaders.SkaterLeaders.Any(leader => leader.Category == "Points"), "Skater leaders should include points.");
        Assert.True(leaders.GoalieLeaders.Any(leader => leader.Category == "Save %"), "Goalie leaders should include save percentage.");
    }

    public void RecentResultsShown()
    {
        var result = SimulatePlayerTeamGame();
        var recent = new GameRecapService().RecentResults(result.ScenarioSnapshot);

        Assert.True(recent.Count == 1, "Recent results should include completed game.");
        Assert.Equal(GameStatus.Completed, recent[0].Status);
    }

    public void UpcomingGamesShown()
    {
        var ready = ScenarioWithPlayerTeamGameTomorrow();
        var upcoming = new GameRecapService().UpcomingGames(ready.ScenarioSnapshot);

        Assert.True(upcoming.Count == 1, "Upcoming games should include scheduled game.");
        Assert.Equal(GameStatus.Scheduled, upcoming[0].Status);
    }

    public void DashboardShowsLastGameNextGameAndRecord()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Last Game", StringComparison.Ordinal), "Dashboard should show last game.");
        Assert.True(text.Contains("Next Game", StringComparison.Ordinal), "Dashboard should show next game.");
        Assert.True(text.Contains("Team Record", StringComparison.Ordinal), "Dashboard should show team record.");
    }

    public void RecapDoesNotExposeHiddenRatings()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "engine", "LegacyEngine", "Integration", "GameRecap.cs"),
            Path.Combine(root, "engine", "LegacyEngine", "Integration", "GameRecapService.cs"),
            Path.Combine(root, "engine", "LegacyEngine", "Integration", "SeasonStatsPolishService.cs")
        };
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("CurrentAbility", StringComparison.Ordinal), "Recap UI should not expose current ability.");
        Assert.False(text.Contains("Potential", StringComparison.Ordinal), "Recap UI should not expose hidden potential.");
        Assert.False(text.Contains("HiddenRating", StringComparison.OrdinalIgnoreCase), "Recap UI should not expose hidden ratings.");
    }

    public void GameRecapPolishHasNoGodotSaveOrFullTacticalSimulation()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Game*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Stats*.cs"));
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Game recap polish should not depend on Godot.");
        Assert.False(text.Contains("SaveSystem", StringComparison.Ordinal), "Game recap polish should not implement save/load.");
        Assert.False(text.Contains("LineMatching", StringComparison.Ordinal), "Game recap polish should not implement line matching.");
        Assert.False(text.Contains("PlayByPlay", StringComparison.Ordinal), "Game recap polish should not implement play-by-play.");
    }

    private static SeasonSimulationResult SimulatePlayerTeamGame()
    {
        var ready = ScenarioWithPlayerTeamGameTomorrow();
        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(ready.Registry, ready.ScenarioSnapshot);
        return new SeasonSimulationResult(
            result.ScenarioSnapshot,
            result.ScenarioSnapshot.Schedule!.Games.Where(game => game.Status == GameStatus.Completed).ToArray(),
            result.ScenarioSnapshot.GameRecaps,
            result.InboxItems.Where(item => item.EventType == LegacyEventType.GamePlayed).ToArray(),
            result.Summary);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithPlayerTeamGameTomorrow()
    {
        var ready = ReadyScenario();
        var date = ready.ScenarioSnapshot.CurrentDate.AddDays(1);
        var teams = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot);
        var opponent = teams.First(team => team.OrganizationId != ready.ScenarioSnapshot.Organization.OrganizationId);
        var game = new ScheduledGame("alpha26-player-game-001", date, ready.ScenarioSnapshot.Organization.OrganizationId, opponent.OrganizationId);
        return WithSchedule(ready, new[] { game });
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithLeagueGameTomorrow()
    {
        var ready = ReadyScenario();
        var date = ready.ScenarioSnapshot.CurrentDate.AddDays(1);
        var teams = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot)
            .Where(team => team.OrganizationId != ready.ScenarioSnapshot.Organization.OrganizationId)
            .Take(2)
            .ToArray();
        var game = new ScheduledGame("alpha26-league-game-001", date, teams[0].OrganizationId, teams[1].OrganizationId);
        return WithSchedule(ready, new[] { game });
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) WithSchedule(
        (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ready,
        IReadOnlyList<ScheduledGame> games)
    {
        var teams = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot);
        var stats = new SeasonStatsService();
        var snapshot = ready.ScenarioSnapshot with
        {
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true, SeasonBegun: true),
            Schedule = new GameSchedule("alpha26-test-schedule", ready.ScenarioSnapshot.Season.LeagueId, games),
            Standings = stats.CreateStandings(ready.ScenarioSnapshot.Season.LeagueId, teams),
            TeamStats = stats.CreateTeamStats(teams),
            PlayerStats = stats.CreatePlayerStats(ready.ScenarioSnapshot.AlphaSnapshot),
            GoalieStats = stats.CreateGoalieStats(ready.ScenarioSnapshot.AlphaSnapshot),
            GameRecaps = Array.Empty<GameRecap>()
        };
        snapshot.Validate();
        return (ready.Registry, snapshot);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyScenario()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var roster = scenario.ScenarioSnapshot.AlphaSnapshot.Roster;
        var total = roster.CurrentPlayers.Count;
        var active = roster.ActivePlayers.Count;
        var goalies = roster.CurrentPlayers.Count(player => player.IsGoalie);
        var overage = roster.CurrentPlayers.Count(player => player.IsOverage());
        var imports = roster.CurrentPlayers.Count(player => player.IsImport);
        var rulebook = WithRosterRules(
            RulebookPresets.Create(DraftLeaguePreset.JuniorMajor),
            new RosterRules
            {
                MinRoster = total,
                MaxRoster = total,
                ActiveRoster = active,
                GoaliesRequired = goalies,
                OverageSlots = Math.Max(3, overage),
                ImportSlots = Math.Max(2, imports),
                InjuredReserveEnabled = true,
                ReserveListEnabled = true
            });

        var registry = scenario.Registry with { Rulebook = rulebook };
        var camp = new TrainingCamp(
            "camp-alpha26-ready",
            scenario.ScenarioSnapshot.Organization.OrganizationId,
            scenario.ScenarioSnapshot.CurrentDate,
            Array.Empty<TrainingCampPlayer>(),
            Array.Empty<TrainingCampEvaluation>(),
            CompletedOn: scenario.ScenarioSnapshot.CurrentDate);
        var snapshot = scenario.ScenarioSnapshot with
        {
            TrainingCamp = camp,
            PendingActions = Array.Empty<PendingGmAction>(),
            ProspectRights = Array.Empty<DraftRightsRecord>(),
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true)
        };
        snapshot.Validate();
        return (registry, snapshot);
    }

    private static Rulebook WithRosterRules(Rulebook source, RosterRules rosterRules) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = rosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules,
            SeasonRules = source.SeasonRules,
            AffiliateRules = source.AffiliateRules,
            FreeAgentRightsRules = source.FreeAgentRightsRules,
            ArbitrationRules = source.ArbitrationRules
        };

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
