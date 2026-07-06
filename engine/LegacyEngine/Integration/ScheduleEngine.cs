using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class ScheduleEngine
{
    public GameSchedule GenerateSchedule(
        string scheduleId,
        string leagueId,
        DateOnly seasonBeginDate,
        DateOnly playoffsBeginDate,
        IReadOnlyList<(string OrganizationId, string TeamName)> teams)
    {
        if (teams.Count < 2)
        {
            throw new ArgumentException("Schedule generation requires at least two teams.", nameof(teams));
        }

        var regularSeasonDays = Math.Max(14, playoffsBeginDate.DayNumber - seasonBeginDate.DayNumber);
        var maxGames = Math.Min(regularSeasonDays / 3, teams.Count * 8);
        var games = new List<ScheduledGame>();
        var dayOffset = 0;
        var gameNumber = 1;

        while (games.Count < maxGames)
        {
            for (var homeIndex = 0; homeIndex < teams.Count && games.Count < maxGames; homeIndex++)
            {
                var awayIndex = (homeIndex + gameNumber) % teams.Count;
                if (awayIndex == homeIndex)
                {
                    awayIndex = (awayIndex + 1) % teams.Count;
                }

                var home = gameNumber % 2 == 0 ? teams[homeIndex] : teams[awayIndex];
                var away = gameNumber % 2 == 0 ? teams[awayIndex] : teams[homeIndex];
                games.Add(new ScheduledGame(
                    GameId: $"{scheduleId}-game-{gameNumber:000}",
                    Date: seasonBeginDate.AddDays(dayOffset),
                    HomeOrganizationId: home.OrganizationId,
                    AwayOrganizationId: away.OrganizationId));
                gameNumber++;
                dayOffset = Math.Min(regularSeasonDays - 1, dayOffset + 3);
            }
        }

        var schedule = new GameSchedule(scheduleId, leagueId, games);
        schedule.Validate();
        return schedule;
    }

    public static DateOnly SeasonBeginDate(Season season) =>
        season.Calendar.Milestones.Single(item => item.Type == SeasonMilestoneType.SeasonBegins).Date!.Value;

    public static DateOnly PlayoffsBeginDate(Season season) =>
        season.Calendar.Milestones.Single(item => item.Type == SeasonMilestoneType.PlayoffsBegin).Date!.Value;
}
