using LegacyEngine.Events;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class SeasonFrameworkService
{
    private static readonly IReadOnlyList<(string OrganizationId, string TeamName)> OpponentTeams =
    [
        ("org-north-valley", "North Valley Wolves"),
        ("org-river-city", "River City Royals"),
        ("org-lakeview", "Lakeview Miners"),
        ("org-parkland", "Parkland Bears"),
        ("org-summit", "Summit Ridge Hawks")
    ];

    public NewGmScenarioSnapshot EnsureSeasonFramework(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        if (scenario.Schedule is not null && scenario.Standings is not null)
        {
            return scenario;
        }

        var teams = LeagueTeams(scenario);
        var schedule = new ScheduleEngine().GenerateSchedule(
            $"schedule:{scenario.Season.SeasonId}",
            scenario.Season.LeagueId,
            ScheduleEngine.SeasonBeginDate(scenario.Season),
            ScheduleEngine.PlayoffsBeginDate(scenario.Season),
            teams);
        var stats = new SeasonStatsService();
        var standings = stats.CreateStandings(scenario.Season.LeagueId, teams);
        var alpha = scenario.AlphaSnapshot;
        var updated = scenario with
        {
            Schedule = schedule,
            Standings = standings,
            TeamStats = stats.CreateTeamStats(teams),
            PlayerStats = stats.CreatePlayerStats(alpha),
            GoalieStats = stats.CreateGoalieStats(alpha)
        };
        updated.Validate();
        return updated;
    }

    public SeasonSimulationResult SimulateScheduledGamesForCurrentDate(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var current = EnsureSeasonFramework(registry, scenario);
        var schedule = current.Schedule!;
        var standings = current.Standings!;
        var stats = new SeasonStatsService();
        var simulator = new BasicGameSimulator();
        var simulated = new List<ScheduledGame>();
        var inbox = new List<AlphaInboxItem>();
        var playerStats = current.PlayerStats;
        var goalieStats = current.GoalieStats;
        var teamStats = current.TeamStats;

        foreach (var game in schedule.GamesOn(current.CurrentDate).Where(game => game.Status == GameStatus.Scheduled))
        {
            var result = simulator.Simulate(game);
            var completed = game.Complete(result);
            schedule = schedule.ReplaceGame(completed);
            standings = stats.ApplyStandings(standings, completed, result);
            teamStats = stats.ApplyTeamStats(teamStats, completed, result);
            playerStats = stats.ApplyPlayerStats(current.AlphaSnapshot, playerStats, completed, result);
            goalieStats = stats.ApplyGoalieStats(current.AlphaSnapshot, goalieStats, completed, result);
            simulated.Add(completed);

            QueueGameEvent(registry, current, completed);
            if (completed.HomeOrganizationId == current.Organization.OrganizationId || completed.AwayOrganizationId == current.Organization.OrganizationId)
            {
                inbox.Add(GameRecapInbox(current, completed));
            }
        }

        var updated = current with
        {
            Schedule = schedule,
            Standings = standings,
            TeamStats = teamStats,
            PlayerStats = playerStats,
            GoalieStats = goalieStats
        };
        var summary = simulated.Count == 0
            ? "No scheduled games today."
            : $"Simulated {simulated.Count} scheduled game(s).";
        var simulation = new SeasonSimulationResult(updated, simulated, inbox, summary);
        simulation.Validate();
        return simulation;
    }

    public ScheduledGame? NextGame(NewGmScenarioSnapshot scenario) =>
        scenario.Schedule?.NextGameFor(scenario.Organization.OrganizationId, scenario.CurrentDate);

    public static IReadOnlyList<(string OrganizationId, string TeamName)> LeagueTeams(NewGmScenarioSnapshot scenario) =>
        new[] { (scenario.Organization.OrganizationId, scenario.Organization.Name) }
            .Concat(OpponentTeams)
            .ToArray();

    private static void QueueGameEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, ScheduledGame game)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 21, 30, 0, TimeSpan.Zero),
            LegacyEventType.GamePlayed,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            "Game played",
            RecapText(scenario, game),
            new LegacyEventContext(OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId, GameId: game.GameId),
            new Dictionary<string, object?>
            {
                ["home_goals"] = game.Result!.HomeGoals,
                ["away_goals"] = game.Result.AwayGoals,
                ["winner"] = game.Result.WinnerOrganizationId
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem GameRecapInbox(NewGmScenarioSnapshot scenario, ScheduledGame game) =>
        new(
            InboxItemId: $"inbox:game-recap:{game.GameId}",
            Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 22, 0, 0, TimeSpan.Zero),
            EventType: LegacyEventType.GamePlayed,
            Severity: LegacyEventSeverity.Notice,
            Title: $"Game Recap: {TeamName(scenario, game.HomeOrganizationId)} {game.Result!.HomeGoals}, {TeamName(scenario, game.AwayOrganizationId)} {game.Result.AwayGoals}",
            Summary: RecapText(scenario, game),
            PrimaryPersonId: null);

    private static string RecapText(NewGmScenarioSnapshot scenario, ScheduledGame game)
    {
        var result = game.Result!;
        return $"{TeamName(scenario, game.HomeOrganizationId)} {result.HomeGoals}, {TeamName(scenario, game.AwayOrganizationId)} {result.AwayGoals}. Winner: {TeamName(scenario, result.WinnerOrganizationId)}{(result.OvertimeOrShootout ? " (OT/SO placeholder)" : string.Empty)}.";
    }

    private static string TeamName(NewGmScenarioSnapshot scenario, string organizationId)
    {
        var team = LeagueTeams(scenario).FirstOrDefault(team => team.OrganizationId == organizationId);
        return string.IsNullOrWhiteSpace(team.TeamName) ? organizationId : team.TeamName;
    }
}
