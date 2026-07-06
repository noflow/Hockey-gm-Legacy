namespace LegacyEngine.Integration;

public sealed class SeasonStatsPolishService
{
    public SeasonStatLeaders BuildLeaders(NewGmScenarioSnapshot scenario)
    {
        var teamLeaders = new List<StatLeader>();
        var skaterLeaders = new List<StatLeader>();
        var goalieLeaders = new List<StatLeader>();
        var leagueLeaders = new List<StatLeader>();

        var standings = scenario.Standings?.OrderedTeams() ?? Array.Empty<TeamStanding>();
        AddTeamLeader(teamLeaders, "Goals For", scenario.TeamStats.OrderByDescending(team => team.GoalsFor).FirstOrDefault(), team => team.GoalsFor);
        AddTeamLeader(teamLeaders, "Goals Against", scenario.TeamStats.OrderBy(team => team.GoalsAgainst).FirstOrDefault(), team => team.GoalsAgainst);
        AddStandingLeader(teamLeaders, "Points", standings.FirstOrDefault(), team => team.Points);
        AddStandingLeader(teamLeaders, "Goal Differential", standings.OrderByDescending(team => team.GoalsFor - team.GoalsAgainst).FirstOrDefault(), team => team.GoalsFor - team.GoalsAgainst);

        AddSkaterLeader(skaterLeaders, "Goals", scenario.PlayerStats.OrderByDescending(player => player.Goals).FirstOrDefault(), player => player.Goals);
        AddSkaterLeader(skaterLeaders, "Assists", scenario.PlayerStats.OrderByDescending(player => player.Assists).FirstOrDefault(), player => player.Assists);
        AddSkaterLeader(skaterLeaders, "Points", scenario.PlayerStats.OrderByDescending(player => player.Points).FirstOrDefault(), player => player.Points);
        AddSkaterLeader(skaterLeaders, "Plus/Minus", scenario.PlayerStats.OrderByDescending(player => player.PlusMinus).FirstOrDefault(), player => player.PlusMinus);
        AddSkaterLeader(skaterLeaders, "PIM", scenario.PlayerStats.OrderByDescending(player => player.PenaltyMinutes).FirstOrDefault(), player => player.PenaltyMinutes);

        AddGoalieLeader(goalieLeaders, "Wins", scenario.GoalieStats.OrderByDescending(goalie => goalie.Wins).FirstOrDefault(), goalie => goalie.Wins);
        AddGoalieLeader(goalieLeaders, "Save %", scenario.GoalieStats.OrderByDescending(goalie => goalie.SavePercentage).FirstOrDefault(), goalie => goalie.SavePercentage);
        AddGoalieLeader(goalieLeaders, "GAA", scenario.GoalieStats.Where(goalie => goalie.GamesPlayed > 0).OrderBy(goalie => goalie.GoalsAgainstAverage).FirstOrDefault(), goalie => goalie.GoalsAgainstAverage);
        AddGoalieLeader(goalieLeaders, "Shutouts", scenario.GoalieStats.OrderByDescending(goalie => goalie.Shutouts).FirstOrDefault(), goalie => goalie.Shutouts);

        leagueLeaders.AddRange(teamLeaders.Take(4));
        leagueLeaders.AddRange(skaterLeaders.Take(4));
        leagueLeaders.AddRange(goalieLeaders.Take(4));

        var leaders = new SeasonStatLeaders(teamLeaders, skaterLeaders, goalieLeaders, leagueLeaders);
        leaders.Validate();
        return leaders;
    }

    private static void AddTeamLeader(List<StatLeader> leaders, string category, TeamSeasonStatLine? line, Func<TeamSeasonStatLine, decimal> value)
    {
        if (line is not null)
        {
            leaders.Add(new StatLeader(category, line.TeamName, $"GP {line.GamesPlayed}", value(line)));
        }
    }

    private static void AddStandingLeader(List<StatLeader> leaders, string category, TeamStanding? standing, Func<TeamStanding, decimal> value)
    {
        if (standing is not null)
        {
            leaders.Add(new StatLeader(category, standing.TeamName, $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}", value(standing)));
        }
    }

    private static void AddSkaterLeader(List<StatLeader> leaders, string category, PlayerSeasonStatLine? line, Func<PlayerSeasonStatLine, decimal> value)
    {
        if (line is not null)
        {
            leaders.Add(new StatLeader(category, line.PlayerName, $"G {line.Goals}, A {line.Assists}, PTS {line.Points}", value(line)));
        }
    }

    private static void AddGoalieLeader(List<StatLeader> leaders, string category, GoalieSeasonStatLine? line, Func<GoalieSeasonStatLine, decimal> value)
    {
        if (line is not null)
        {
            leaders.Add(new StatLeader(category, line.PlayerName, $"W {line.Wins}, SV% {line.SavePercentage:0.000}, GAA {line.GoalsAgainstAverage:0.00}", value(line)));
        }
    }
}
