namespace LegacyEngine.Integration;

public sealed record StandingsTable(
    string LeagueId,
    IReadOnlyList<TeamStanding> Teams)
{
    public StandingsTable ApplyGame(ScheduledGame game, GameResult result)
    {
        var teams = Teams.Select(team =>
        {
            if (team.OrganizationId == game.HomeOrganizationId)
            {
                return team.ApplyGame(result.HomeGoals, result.AwayGoals, result.OvertimeOrShootout && result.HomeGoals < result.AwayGoals);
            }

            if (team.OrganizationId == game.AwayOrganizationId)
            {
                return team.ApplyGame(result.AwayGoals, result.HomeGoals, result.OvertimeOrShootout && result.AwayGoals < result.HomeGoals);
            }

            return team;
        }).ToArray();

        return this with { Teams = teams };
    }

    public IReadOnlyList<TeamStanding> OrderedTeams() =>
        Teams
            .OrderByDescending(team => team.Points)
            .ThenByDescending(team => team.Wins)
            .ThenByDescending(team => team.GoalsFor - team.GoalsAgainst)
            .ThenBy(team => team.TeamName, StringComparer.Ordinal)
            .ToArray();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeagueId))
        {
            throw new ArgumentException("Standings table requires league id.", nameof(LeagueId));
        }

        foreach (var team in Teams)
        {
            team.Validate();
        }
    }
}
