using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

internal sealed class Alpha40MultiSeasonTests
{
    public void SeasonCompletesWhenScheduleFinished()
    {
        var completed = CompleteSeason();

        Assert.True(completed.Result.Success, completed.Result.Message);
        Assert.True(completed.Result.ScenarioSnapshot.SeasonRollover.CurrentSeasonCompleted, "Rollover state should mark the season completed.");
    }

    public void FinalStandingsArchived()
    {
        var completed = CompleteSeason();

        Assert.True(completed.Result.Archive!.FinalStandings.Teams.Count > 0, "Final standings should be archived.");
        Assert.True(completed.Result.Archive.FinalStandings.Teams.Any(team => team.GamesPlayed > 0), "Archived standings should include played games.");
    }

    public void PlayerStatsArchived()
    {
        var completed = CompleteSeason();

        Assert.True(completed.Result.Archive!.PlayerStats.Count > 0, "Player stats should be archived.");
        Assert.True(completed.Result.Archive.PlayerStats.Any(stat => stat.GamesPlayed > 0), "Archived player stats should include games played.");
    }

    public void CurrentSeasonStatsReset()
    {
        var completed = CompleteSeason();
        var scenario = completed.Result.ScenarioSnapshot;
        var standings = scenario.Standings ?? throw new InvalidOperationException("New season standings should exist.");

        Assert.True(standings.Teams.All(team => team.GamesPlayed == 0), "New season standings should reset.");
        Assert.True(scenario.PlayerStats.All(stat => stat.GamesPlayed == 0), "New season player stats should reset.");
        Assert.True(scenario.GoalieStats.All(stat => stat.GamesPlayed == 0), "New season goalie stats should reset.");
    }

    public void OrganizationHistoryUpdated()
    {
        var completed = CompleteSeason();

        Assert.True(
            completed.Result.ScenarioSnapshot.OrganizationSeasonHistory.Any(item => item.SeasonYear == completed.Result.Archive!.SeasonYear),
            "Completed season should be added to organization history.");
    }

    public void EndOfSeasonReviewGenerated()
    {
        var completed = CompleteSeason();

        Assert.True(
            completed.Result.ScenarioSnapshot.ExecutiveReports.Reports.Any(report => report.Kind == ExecutiveReportKind.EndOfSeasonExecutiveReview),
            "End-of-season executive review should be archived.");
    }

    public void OffseasonPhaseBegins()
    {
        var completed = CompleteSeason();

        Assert.Equal(SeasonPhase.Offseason, completed.Result.ScenarioSnapshot.Season.CurrentPhase);
        Assert.True(completed.Result.ScenarioSnapshot.Season.Status == SeasonStatus.Upcoming, "Next season should be upcoming after rollover.");
    }

    public void ExpiringContractsIdentified()
    {
        var completed = CompleteSeason();

        Assert.True(completed.Result.ScenarioSnapshot.SeasonRollover.ExpiringContracts.Count > 0, "Expiring contracts should be identified.");
    }

    public void PendingActionsCreatedForContractDecisions()
    {
        var completed = CompleteSeason();

        Assert.True(
            completed.Result.ScenarioSnapshot.PendingActions.Any(action => action.IsOpen && action.ActionType == PendingGmActionType.ApproveContract),
            "Expired contracts should create pending GM contract decisions.");
    }

    public void PlayerAgesUpdateNaturally()
    {
        var completed = CompleteSeason();
        var player = completed.Result.ScenarioSnapshot.AlphaSnapshot.Players.First();
        var calculated = player.CalculateAge(completed.Result.ScenarioSnapshot.CurrentDate);

        Assert.True(calculated >= 15, "Player age should be calculated from birth date at the rollover date.");
    }

    public void NewDraftClassGenerated()
    {
        var completed = CompleteSeason();

        Assert.True(completed.Result.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Count >= 50, "Next draft class should populate the draft board.");
        Assert.True(completed.Result.ScenarioSnapshot.AlphaSnapshot.Recruits.Count >= 50, "Next draft class should also populate recruiting targets.");
    }

    public void NewDraftClassHasUniqueCleanNames()
    {
        var completed = CompleteSeason();
        var scenario = completed.Result.ScenarioSnapshot;
        var names = scenario.AlphaSnapshot.DraftBoard.Entries
            .Select(entry => scenario.AlphaSnapshot.People.Single(person => person.PersonId == entry.ProspectPersonId).Identity.DisplayName)
            .ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.False(names.Any(name => name.Any(char.IsDigit)), "Draft display names should not contain numeric suffixes.");
    }

    public void PreviousDraftClassNotReused()
    {
        var prepared = CompletedScheduleScenario();
        var oldIds = prepared.Scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToHashSet(StringComparer.Ordinal);
        var result = new SeasonRolloverService().CompleteSeasonAndEnterOffseason(prepared.Registry, prepared.Scenario);
        var newIds = result.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToArray();

        Assert.False(newIds.Any(oldIds.Contains), "Next draft class should not reuse previous draft class person ids.");
    }

    public void SaveLoadWorksAfterRollover()
    {
        var completed = CompleteSeason();
        var inbox = new InboxManager();
        inbox.AddRange(completed.Result.InboxItems);
        var budget = new BudgetOverviewService().Build(completed.Result.ScenarioSnapshot, RulebookPresets.CreateJuniorMajor());
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha40-{Guid.NewGuid():N}.json");
        var save = new SaveGameService().SaveCareer(
            completed.Result.ScenarioSnapshot,
            inbox.AllMessages,
            completed.Result.LeagueTransactions,
            new Dictionary<string, ActionCenterStatus>(StringComparer.Ordinal),
            budget,
            path,
            "Alpha 4.0 Rollover Save");

        var load = new SaveGameService().LoadFromFile(path, RulebookPresets.CreateJuniorMajor());

        Assert.True(save.Success, save.Message);
        Assert.True(load.Success, load.Message);
        Assert.Equal(completed.Result.ScenarioSnapshot.CurrentDate, load.SaveGame!.ScenarioSnapshot.CurrentDate);
        Assert.Equal(SeasonPhase.Offseason, load.SaveGame.ScenarioSnapshot.Season.CurrentPhase);
        Assert.True(load.SaveGame.ScenarioSnapshot.SeasonRollover.SeasonArchives.Count > 0, "Loaded save should preserve archived season.");
        Assert.True(load.SaveGame.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Count > 0, "Loaded save should preserve new draft class.");
        Assert.True(load.SaveGame.ScenarioSnapshot.PlayerStats.All(stat => stat.GamesPlayed == 0), "Loaded save should preserve reset current stats.");
        Assert.True(load.SaveGame.ScenarioSnapshot.PriorSeasonStats.Any(stat => stat.SeasonYear == completed.Result.Archive!.SeasonYear), "Loaded save should preserve historical stats.");
    }

    public void AlphaDesktopExposesOffseasonHistoryState()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Finish Season", StringComparison.Ordinal), "Desktop should expose Finish Season.");
        Assert.True(source.Contains("Season Archive", StringComparison.Ordinal), "Desktop should expose Season Archive.");
        Assert.True(source.Contains("Archived Seasons", StringComparison.Ordinal), "Desktop should expose archived season history.");
    }

    public void Alpha40HasNoGodotDatabaseCloudPlayoffAwardsOrRetirement()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Season*.*", SearchOption.TopDirectoryOnly)
            .Where(path => path.Contains("Rollover", StringComparison.Ordinal) || path.Contains("Archive", StringComparison.Ordinal) || path.Contains("Completion", StringComparison.Ordinal))
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 4.0 should not add Godot.");
        Assert.False(text.Contains("DbContext", StringComparison.Ordinal), "Alpha 4.0 should not add database persistence.");
        Assert.False(text.Contains("Cloud", StringComparison.Ordinal), "Alpha 4.0 should not add cloud save.");
        Assert.False(text.Contains("AwardWinner", StringComparison.Ordinal), "Alpha 4.0 should not add a full awards system.");
        Assert.False(text.Contains("RetirementEngine", StringComparison.Ordinal), "Alpha 4.0 should not add a retirement system.");
    }

    private static (EngineRegistry Registry, SeasonCompletionResult Result) CompleteSeason()
    {
        var prepared = CompletedScheduleScenario();
        var result = new SeasonRolloverService().CompleteSeasonAndEnterOffseason(prepared.Registry, prepared.Scenario);
        Assert.True(result.Success, result.Message);
        return (prepared.Registry, result);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot Scenario) CompletedScheduleScenario()
    {
        var created = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var scenario = new SeasonFrameworkService().EnsureSeasonFramework(created.Registry, created.ScenarioSnapshot);
        var stats = new SeasonStatsService();
        var simulator = new BasicGameSimulator();
        var recapService = new GameRecapService();
        var schedule = scenario.Schedule!;
        var standings = scenario.Standings!;
        var teamStats = scenario.TeamStats;
        var playerStats = scenario.PlayerStats;
        var goalieStats = scenario.GoalieStats;
        var recaps = new List<GameRecap>();

        foreach (var game in schedule.Games)
        {
            var result = simulator.Simulate(game);
            var completed = game.Complete(result);
            schedule = schedule.ReplaceGame(completed);
            standings = stats.ApplyStandings(standings, completed, result);
            teamStats = stats.ApplyTeamStats(teamStats, completed, result);
            playerStats = stats.ApplyPlayerStats(scenario.AlphaSnapshot, playerStats, completed, result);
            goalieStats = stats.ApplyGoalieStats(scenario.AlphaSnapshot, goalieStats, completed, result);
            recaps.Add(recapService.CreateRecap(scenario with
            {
                Schedule = schedule,
                Standings = standings,
                TeamStats = teamStats,
                PlayerStats = playerStats,
                GoalieStats = goalieStats,
                GameRecaps = recaps
            }, completed));
        }

        scenario = scenario with
        {
            Schedule = schedule,
            Standings = standings,
            TeamStats = teamStats,
            PlayerStats = playerStats,
            GoalieStats = goalieStats,
            GameRecaps = recaps,
            Season = scenario.Season with { Status = SeasonStatus.Active, CurrentPhase = SeasonPhase.RegularSeason }
        };
        scenario.Validate();
        return (created.Registry, scenario);
    }

    private static NewGmScenarioResult AdvanceToDraftDay(NewGmScenarioResult scenario)
    {
        var snapshot = scenario.AlphaSnapshot;
        var scenarioSnapshot = scenario.ScenarioSnapshot;
        var coordinator = new DailySimulationCoordinator();

        while (snapshot.CurrentDate < scenarioSnapshot.DraftDate)
        {
            var result = coordinator.AdvanceOneDay(scenario.Registry, snapshot);
            snapshot = result.WorldSnapshot;
            scenarioSnapshot = scenarioSnapshot with
            {
                AlphaSnapshot = snapshot,
                Season = snapshot.Season ?? scenarioSnapshot.Season
            };
        }

        return scenario with
        {
            AlphaSnapshot = snapshot,
            ScenarioSnapshot = scenarioSnapshot
        };
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
