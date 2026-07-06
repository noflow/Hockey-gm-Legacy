namespace LegacyEngine.Integration;

public sealed class BasicGameSimulator
{
    public GameResult Simulate(ScheduledGame game)
    {
        game.Validate();
        var seed = Math.Abs(HashCode.Combine(game.GameId, game.Date.DayNumber, game.HomeOrganizationId, game.AwayOrganizationId));
        var homeGoals = 2 + (seed % 4);
        var awayGoals = 1 + ((seed / 7) % 4);
        var overtime = false;
        if (homeGoals == awayGoals)
        {
            overtime = true;
            if (seed % 2 == 0)
            {
                homeGoals++;
            }
            else
            {
                awayGoals++;
            }
        }

        var winner = homeGoals > awayGoals ? game.HomeOrganizationId : game.AwayOrganizationId;
        var loser = homeGoals > awayGoals ? game.AwayOrganizationId : game.HomeOrganizationId;
        var result = new GameResult(game.GameId, homeGoals, awayGoals, winner, loser, overtime);
        result.Validate();
        return result;
    }
}
