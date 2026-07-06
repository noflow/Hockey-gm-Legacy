namespace LegacyEngine.Integration;

public sealed record GameResult(
    string GameId,
    int HomeGoals,
    int AwayGoals,
    string WinnerOrganizationId,
    string LoserOrganizationId,
    bool OvertimeOrShootout = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GameId) || string.IsNullOrWhiteSpace(WinnerOrganizationId) || string.IsNullOrWhiteSpace(LoserOrganizationId))
        {
            throw new ArgumentException("Game result requires game, winner, and loser ids.");
        }

        if (HomeGoals < 0 || AwayGoals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(HomeGoals), "Goals cannot be negative.");
        }
    }
}
