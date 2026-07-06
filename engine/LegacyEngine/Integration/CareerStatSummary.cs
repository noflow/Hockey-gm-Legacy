using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record CareerStatSummary(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int Seasons,
    int GamesPlayed,
    int Goals = 0,
    int Assists = 0,
    int PenaltyMinutes = 0,
    int Wins = 0,
    int Losses = 0,
    int Shutouts = 0,
    string PrimaryLeague = "Junior",
    string SummaryText = "")
{
    public int Points => Goals + Assists;

    public bool IsGoalie => Position == RosterPosition.Goalie;

    public string DisplaySummary => !string.IsNullOrWhiteSpace(SummaryText)
        ? SummaryText
        : IsGoalie
            ? $"{Seasons} tracked season(s), {GamesPlayed} GP, {Wins}-{Losses}, {Shutouts} shutout(s) in {PrimaryLeague}."
            : $"{Seasons} tracked season(s), {GamesPlayed} GP, {Goals}-{Assists}-{Points}, {PenaltyMinutes} PIM in {PrimaryLeague}.";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(PrimaryLeague))
        {
            throw new ArgumentException("Career stat summary requires person, name, and league identity.");
        }

        if (Position == RosterPosition.Unknown)
        {
            throw new ArgumentException("Career stat summary must include a known position.", nameof(Position));
        }

        if (Seasons < 0 || GamesPlayed < 0 || Goals < 0 || Assists < 0 || PenaltyMinutes < 0 || Wins < 0 || Losses < 0 || Shutouts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(GamesPlayed), "Career stats cannot be negative.");
        }
    }
}
