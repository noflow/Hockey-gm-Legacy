namespace LegacyEngine.Integration;

public sealed record PlayerSeasonStatLine(
    string PersonId,
    string PlayerName,
    int GamesPlayed = 0,
    int Goals = 0,
    int Assists = 0,
    int PlusMinus = 0,
    int PenaltyMinutes = 0)
{
    public int Points => Goals + Assists;

    public PlayerSeasonStatLine ApplyGame(int goals, int assists, int plusMinus, int penaltyMinutes = 0) =>
        this with
        {
            GamesPlayed = GamesPlayed + 1,
            Goals = Goals + goals,
            Assists = Assists + assists,
            PlusMinus = PlusMinus + plusMinus,
            PenaltyMinutes = PenaltyMinutes + penaltyMinutes
        };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName))
        {
            throw new ArgumentException("Player stat line requires person id and name.");
        }
    }
}
