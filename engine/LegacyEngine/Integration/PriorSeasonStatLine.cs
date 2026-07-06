using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record PriorSeasonStatLine(
    string PersonId,
    string PlayerName,
    int SeasonYear,
    string TeamName,
    string LeagueName,
    RosterPosition Position,
    int GamesPlayed,
    int Goals = 0,
    int Assists = 0,
    int PlusMinus = 0,
    int PenaltyMinutes = 0,
    int Wins = 0,
    int Losses = 0,
    decimal SavePercentage = 0m,
    decimal GoalsAgainstAverage = 0m)
{
    public int Points => Goals + Assists;

    public bool IsGoalie => Position == RosterPosition.Goalie;

    public string SummaryText => IsGoalie
        ? $"{PlayerName} {SeasonYear}: {GamesPlayed} GP, {Wins}-{Losses}, {SavePercentage:0.000} SV%, {GoalsAgainstAverage:0.00} GAA with {TeamName} ({LeagueName})."
        : $"{PlayerName} {SeasonYear}: {GamesPlayed} GP, {Goals}-{Assists}-{Points}, {PlusMinus:+#;-#;0}, {PenaltyMinutes} PIM with {TeamName} ({LeagueName}).";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(LeagueName))
        {
            throw new ArgumentException("Prior season stat line requires player, team, and league identity.");
        }

        if (Position == RosterPosition.Unknown)
        {
            throw new ArgumentException("Prior season stat line must include a known position.", nameof(Position));
        }

        if (GamesPlayed < 0 || Goals < 0 || Assists < 0 || PenaltyMinutes < 0 || Wins < 0 || Losses < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(GamesPlayed), "Prior season stats cannot be negative.");
        }

        if (SavePercentage is < 0m or > 1m || GoalsAgainstAverage < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(SavePercentage), "Goalie rates must be realistic non-hidden stat values.");
        }
    }
}
