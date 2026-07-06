using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class SeasonStatsService
{
    public StandingsTable CreateStandings(string leagueId, IReadOnlyList<(string OrganizationId, string TeamName)> teams)
    {
        var table = new StandingsTable(
            leagueId,
            teams.Select(team => new TeamStanding(team.OrganizationId, team.TeamName, 0, 0, 0, 0, 0, 0, 0)).ToArray());
        table.Validate();
        return table;
    }

    public IReadOnlyList<TeamSeasonStatLine> CreateTeamStats(IReadOnlyList<(string OrganizationId, string TeamName)> teams) =>
        teams.Select(team => new TeamSeasonStatLine(team.OrganizationId, team.TeamName)).ToArray();

    public IReadOnlyList<PlayerSeasonStatLine> CreatePlayerStats(AlphaWorldSnapshot snapshot) =>
        snapshot.Roster.ActivePlayers
            .Where(player => player.Position != RosterPosition.Goalie)
            .Select(player => new PlayerSeasonStatLine(player.PersonId, PlayerName(snapshot, player.PersonId)))
            .ToArray();

    public IReadOnlyList<GoalieSeasonStatLine> CreateGoalieStats(AlphaWorldSnapshot snapshot) =>
        snapshot.Roster.ActivePlayers
            .Where(player => player.Position == RosterPosition.Goalie)
            .Select(player => new GoalieSeasonStatLine(player.PersonId, PlayerName(snapshot, player.PersonId)))
            .ToArray();

    public StandingsTable ApplyStandings(StandingsTable standings, ScheduledGame game, GameResult result) =>
        standings.ApplyGame(game, result);

    public IReadOnlyList<TeamSeasonStatLine> ApplyTeamStats(
        IReadOnlyList<TeamSeasonStatLine> teamStats,
        ScheduledGame game,
        GameResult result) =>
        teamStats.Select(team =>
        {
            if (team.OrganizationId == game.HomeOrganizationId)
            {
                return team.ApplyGame(result.HomeGoals, result.AwayGoals);
            }

            return team.OrganizationId == game.AwayOrganizationId
                ? team.ApplyGame(result.AwayGoals, result.HomeGoals)
                : team;
        }).ToArray();

    public IReadOnlyList<PlayerSeasonStatLine> ApplyPlayerStats(
        AlphaWorldSnapshot snapshot,
        IReadOnlyList<PlayerSeasonStatLine> playerStats,
        ScheduledGame game,
        GameResult result)
    {
        if (!InvolvesPlayerTeam(snapshot, game))
        {
            return playerStats;
        }

        var playerTeamGoals = game.HomeOrganizationId == snapshot.OrganizationId ? result.HomeGoals : result.AwayGoals;
        var activeSkaters = snapshot.Roster.ActivePlayers.Where(player => player.Position != RosterPosition.Goalie).ToArray();
        return playerStats.Select((line, index) =>
        {
            var goals = index < playerTeamGoals ? 1 : 0;
            var assists = index >= playerTeamGoals && index < playerTeamGoals * 3 ? 1 : 0;
            var plusMinus = goals > 0 ? 1 : 0;
            return activeSkaters.Any(player => player.PersonId == line.PersonId)
                ? line.ApplyGame(goals, assists, plusMinus)
                : line;
        }).ToArray();
    }

    public IReadOnlyList<GoalieSeasonStatLine> ApplyGoalieStats(
        AlphaWorldSnapshot snapshot,
        IReadOnlyList<GoalieSeasonStatLine> goalieStats,
        ScheduledGame game,
        GameResult result)
    {
        if (!InvolvesPlayerTeam(snapshot, game) || goalieStats.Count == 0)
        {
            return goalieStats;
        }

        var won = result.WinnerOrganizationId == snapshot.OrganizationId;
        var goalsAgainst = game.HomeOrganizationId == snapshot.OrganizationId ? result.AwayGoals : result.HomeGoals;
        var saves = 24 + Math.Abs(HashCode.Combine(game.GameId, snapshot.OrganizationId)) % 13;
        var starter = goalieStats[0].ApplyGame(won, goalsAgainst, saves);
        return goalieStats.Select((line, index) => index == 0 ? starter : line).ToArray();
    }

    private static bool InvolvesPlayerTeam(AlphaWorldSnapshot snapshot, ScheduledGame game) =>
        game.HomeOrganizationId == snapshot.OrganizationId || game.AwayOrganizationId == snapshot.OrganizationId;

    private static string PlayerName(AlphaWorldSnapshot snapshot, string personId) =>
        snapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;
}
