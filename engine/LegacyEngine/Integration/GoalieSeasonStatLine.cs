namespace LegacyEngine.Integration;

public sealed record GoalieSeasonStatLine(
    string PersonId,
    string PlayerName,
    int GamesPlayed = 0,
    int Wins = 0,
    int Losses = 0,
    int GoalsAgainst = 0,
    int Saves = 0,
    int Shutouts = 0)
{
    public decimal SavePercentage => Saves + GoalsAgainst == 0
        ? 0m
        : Math.Round((decimal)Saves / (Saves + GoalsAgainst), 3);

    public decimal GoalsAgainstAverage => GamesPlayed == 0
        ? 0m
        : Math.Round((decimal)GoalsAgainst / GamesPlayed, 2);

    public GoalieSeasonStatLine ApplyGame(bool won, int goalsAgainst, int saves) =>
        this with
        {
            GamesPlayed = GamesPlayed + 1,
            Wins = Wins + (won ? 1 : 0),
            Losses = Losses + (won ? 0 : 1),
            GoalsAgainst = GoalsAgainst + goalsAgainst,
            Saves = Saves + saves,
            Shutouts = Shutouts + (goalsAgainst == 0 ? 1 : 0)
        };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName))
        {
            throw new ArgumentException("Goalie stat line requires person id and name.");
        }
    }
}
