namespace LegacyEngine.Integration;

public sealed record ScheduledGame(
    string GameId,
    DateOnly Date,
    string HomeOrganizationId,
    string AwayOrganizationId,
    GameStatus Status = GameStatus.Scheduled,
    GameResult? Result = null)
{
    public bool IsToday(DateOnly date) => Date == date && Status == GameStatus.Scheduled;

    public ScheduledGame Complete(GameResult result)
    {
        result.Validate();
        if (Status == GameStatus.Completed)
        {
            throw new InvalidOperationException("Game is already completed.");
        }

        if (result.GameId != GameId)
        {
            throw new ArgumentException("Game result id must match scheduled game id.", nameof(result));
        }

        return this with { Status = GameStatus.Completed, Result = result };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GameId) || string.IsNullOrWhiteSpace(HomeOrganizationId) || string.IsNullOrWhiteSpace(AwayOrganizationId))
        {
            throw new ArgumentException("Scheduled game requires ids.");
        }

        if (HomeOrganizationId == AwayOrganizationId)
        {
            throw new ArgumentException("A scheduled game requires different home and away teams.");
        }

        Result?.Validate();
    }
}
