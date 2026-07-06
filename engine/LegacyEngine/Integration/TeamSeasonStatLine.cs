namespace LegacyEngine.Integration;

public sealed record TeamSeasonStatLine(
    string OrganizationId,
    string TeamName,
    int GamesPlayed = 0,
    int GoalsFor = 0,
    int GoalsAgainst = 0)
{
    public TeamSeasonStatLine ApplyGame(int goalsFor, int goalsAgainst) =>
        this with
        {
            GamesPlayed = GamesPlayed + 1,
            GoalsFor = GoalsFor + goalsFor,
            GoalsAgainst = GoalsAgainst + goalsAgainst
        };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName))
        {
            throw new ArgumentException("Team stat line requires organization id and name.");
        }
    }
}
