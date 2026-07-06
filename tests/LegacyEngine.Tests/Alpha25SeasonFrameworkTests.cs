using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

internal sealed class Alpha25SeasonFrameworkTests
{
    public void ScheduleGenerated()
    {
        var ready = ReadyScenario();
        var scenario = new SeasonFrameworkService().EnsureSeasonFramework(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(scenario.Schedule is not null, "Schedule should be generated.");
        Assert.True(scenario.Schedule!.Games.Count > 0, "Schedule should contain games.");
    }

    public void GamesHaveDatesHomeAndAway()
    {
        var ready = ReadyScenario();
        var scenario = new SeasonFrameworkService().EnsureSeasonFramework(ready.Registry, ready.ScenarioSnapshot);
        var game = scenario.Schedule!.Games.First();

        Assert.True(game.Date != default, "Scheduled game should have a date.");
        Assert.False(string.IsNullOrWhiteSpace(game.HomeOrganizationId), "Scheduled game should have a home team.");
        Assert.False(string.IsNullOrWhiteSpace(game.AwayOrganizationId), "Scheduled game should have an away team.");
        Assert.False(game.HomeOrganizationId == game.AwayOrganizationId, "Home and away teams should be different.");
    }

    public void GameSimCreatesResult()
    {
        var game = new ScheduledGame("game-test-001", new DateOnly(2026, 9, 25), "home", "away");
        var result = new BasicGameSimulator().Simulate(game);

        Assert.Equal("game-test-001", result.GameId);
        Assert.True(result.HomeGoals >= 0, "Home goals should be non-negative.");
        Assert.True(result.AwayGoals >= 0, "Away goals should be non-negative.");
        Assert.False(string.IsNullOrWhiteSpace(result.WinnerOrganizationId), "Winner should be recorded.");
        Assert.False(string.IsNullOrWhiteSpace(result.LoserOrganizationId), "Loser should be recorded.");
    }

    public void StandingsUpdate()
    {
        var teams = new[] { ("home", "Home Club"), ("away", "Away Club") };
        var stats = new SeasonStatsService();
        var standings = stats.CreateStandings("league-test", teams);
        var game = new ScheduledGame("game-test-002", new DateOnly(2026, 9, 25), "home", "away");
        var result = new GameResult(game.GameId, 4, 2, "home", "away");

        standings = stats.ApplyStandings(standings, game, result);

        var home = standings.Teams.Single(team => team.OrganizationId == "home");
        var away = standings.Teams.Single(team => team.OrganizationId == "away");
        Assert.Equal(1, home.GamesPlayed);
        Assert.Equal(1, home.Wins);
        Assert.Equal(2, home.Points);
        Assert.Equal(1, away.Losses);
    }

    public void PlayerStatsUpdate()
    {
        var ready = ScenarioWithGameOn(DateOnly.FromDateTime(DateTime.Today));
        var game = ready.ScenarioSnapshot.Schedule!.Games.First();
        var result = new GameResult(game.GameId, 5, 2, game.HomeOrganizationId, game.AwayOrganizationId);
        var stats = new SeasonStatsService();

        var updated = stats.ApplyPlayerStats(ready.ScenarioSnapshot.AlphaSnapshot, ready.ScenarioSnapshot.PlayerStats, game, result);

        Assert.True(updated.Any(line => line.GamesPlayed == 1), "At least one skater should receive a game played.");
        Assert.True(updated.Sum(line => line.Goals) > 0, "Player goals should be updated.");
        Assert.True(updated.Sum(line => line.Assists) > 0, "Player assists should be updated.");
    }

    public void GoalieStatsUpdate()
    {
        var ready = ScenarioWithGameOn(DateOnly.FromDateTime(DateTime.Today));
        var game = ready.ScenarioSnapshot.Schedule!.Games.First();
        var result = new GameResult(game.GameId, 3, 1, game.HomeOrganizationId, game.AwayOrganizationId);
        var stats = new SeasonStatsService();

        var updated = stats.ApplyGoalieStats(ready.ScenarioSnapshot.AlphaSnapshot, ready.ScenarioSnapshot.GoalieStats, game, result);

        Assert.True(updated.Any(line => line.GamesPlayed == 1), "One goalie should receive a game played.");
        Assert.True(updated.Any(line => line.Saves > 0), "Goalie saves should be updated.");
    }

    public void DailyPipelineSimulatesScheduledGames()
    {
        var ready = ScenarioWithGameOnTomorrow();
        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.ScenarioSnapshot.Schedule!.Games.Single().Status == GameStatus.Completed, "Scheduled game should be completed by daily advance.");
        Assert.True(result.ScenarioSnapshot.Standings!.Teams.Sum(team => team.GamesPlayed) == 2, "Both teams should receive one standings game played.");
        Assert.True(result.ScenarioSnapshot.TeamStats.Sum(line => line.GamesPlayed) == 2, "Both teams should receive one team stat game played.");
    }

    public void InboxGameRecapGenerated()
    {
        var ready = ScenarioWithGameOnTomorrow();
        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.GamePlayed), "Player team game should create a recap inbox item.");
    }

    public void AlphaDesktopExposesScheduleStandingsAndStats()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("AddWorkspaceTab(tabs, \"Season\"", StringComparison.Ordinal), "AlphaDesktop should expose Season workspace.");
        Assert.True(text.Contains("new WorkspaceScreen(\"Schedule\", CreateTextScreen(\"Schedule\"))", StringComparison.Ordinal), "AlphaDesktop should expose Schedule.");
        Assert.True(text.Contains("new WorkspaceScreen(\"Standings\", CreateTextScreen(\"Standings\"))", StringComparison.Ordinal), "AlphaDesktop should expose Standings.");
        Assert.True(text.Contains("new WorkspaceScreen(\"Stats\", CreateTextScreen(\"Stats\"))", StringComparison.Ordinal), "AlphaDesktop should expose Stats.");
        Assert.True(text.Contains("Next Game", StringComparison.Ordinal), "Dashboard should expose next game.");
    }

    public void SeasonFrameworkHasNoGodotSaveOrFull3DSimulation()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Season*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Schedule*.cs"))
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Game*.cs"));
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Season framework should not depend on Godot.");
        Assert.False(text.Contains("SaveSystem", StringComparison.Ordinal), "Season framework should not implement save/load.");
        Assert.False(text.Contains("PlayByPlay", StringComparison.Ordinal), "Season framework should not implement full tactical simulation.");
        Assert.False(text.Contains("3D", StringComparison.OrdinalIgnoreCase), "Season framework should not implement 3D simulation.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithGameOnTomorrow()
    {
        var ready = ReadyScenario();
        return ScenarioWithGameOn(ready.ScenarioSnapshot.CurrentDate.AddDays(1));
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithGameOn(DateOnly date)
    {
        var ready = ReadyScenario();
        var teams = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot);
        var opponent = teams.First(team => team.OrganizationId != ready.ScenarioSnapshot.Organization.OrganizationId);
        var game = new ScheduledGame("alpha25-test-game-001", date, ready.ScenarioSnapshot.Organization.OrganizationId, opponent.OrganizationId);
        var schedule = new GameSchedule("alpha25-test-schedule", ready.ScenarioSnapshot.Season.LeagueId, new[] { game });
        var stats = new SeasonStatsService();
        var snapshot = ready.ScenarioSnapshot with
        {
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true, SeasonBegun: true),
            Schedule = schedule,
            Standings = stats.CreateStandings(ready.ScenarioSnapshot.Season.LeagueId, teams),
            TeamStats = stats.CreateTeamStats(teams),
            PlayerStats = stats.CreatePlayerStats(ready.ScenarioSnapshot.AlphaSnapshot),
            GoalieStats = stats.CreateGoalieStats(ready.ScenarioSnapshot.AlphaSnapshot)
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
            "camp-alpha25-ready",
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
            AffiliateRules = source.AffiliateRules
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
