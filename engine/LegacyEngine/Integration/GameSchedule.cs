namespace LegacyEngine.Integration;

public sealed record GameSchedule(
    string ScheduleId,
    string LeagueId,
    IReadOnlyList<ScheduledGame> Games)
{
    public IReadOnlyList<ScheduledGame> GamesOn(DateOnly date) =>
        Games.Where(game => game.Date == date).OrderBy(game => game.GameId, StringComparer.Ordinal).ToArray();

    public ScheduledGame? NextGameFor(string organizationId, DateOnly date) =>
        Games
            .Where(game => game.Status == GameStatus.Scheduled
                && game.Date >= date
                && (game.HomeOrganizationId == organizationId || game.AwayOrganizationId == organizationId))
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameId, StringComparer.Ordinal)
            .FirstOrDefault();

    public GameSchedule ReplaceGame(ScheduledGame game) =>
        this with { Games = Games.Select(existing => existing.GameId == game.GameId ? game : existing).ToArray() };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScheduleId) || string.IsNullOrWhiteSpace(LeagueId))
        {
            throw new ArgumentException("Game schedule requires ids.");
        }

        if (Games.Select(game => game.GameId).Distinct(StringComparer.Ordinal).Count() != Games.Count)
        {
            throw new ArgumentException("Scheduled game ids must be unique.", nameof(Games));
        }

        foreach (var game in Games)
        {
            game.Validate();
        }
    }
}
