namespace LegacyEngine.Integration;

public sealed record TeamStanding(
    string OrganizationId,
    string TeamName,
    int GamesPlayed,
    int Wins,
    int Losses,
    int OvertimeLosses,
    int Points,
    int GoalsFor,
    int GoalsAgainst)
{
    public TeamStanding ApplyGame(int goalsFor, int goalsAgainst, bool overtimeLoss = false)
    {
        var win = goalsFor > goalsAgainst;
        return this with
        {
            GamesPlayed = GamesPlayed + 1,
            Wins = Wins + (win ? 1 : 0),
            Losses = Losses + (!win && !overtimeLoss ? 1 : 0),
            OvertimeLosses = OvertimeLosses + (!win && overtimeLoss ? 1 : 0),
            Points = Points + (win ? 2 : overtimeLoss ? 1 : 0),
            GoalsFor = GoalsFor + goalsFor,
            GoalsAgainst = GoalsAgainst + goalsAgainst
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName))
        {
            throw new ArgumentException("Standing requires team identity.");
        }
    }
}
