namespace LegacyEngine.Integration;

public sealed record SeasonArchive(
    string ArchiveId,
    string SeasonId,
    int SeasonYear,
    DateOnly CompletedOn,
    string LeagueId,
    string OrganizationId,
    string OrganizationName,
    StandingsTable FinalStandings,
    IReadOnlyList<TeamSeasonStatLine> TeamStats,
    IReadOnlyList<PlayerSeasonStatLine> PlayerStats,
    IReadOnlyList<GoalieSeasonStatLine> GoalieStats,
    IReadOnlyList<ScheduledGame> GameResults,
    IReadOnlyList<GameRecap> GameRecaps,
    string ChampionTeamName,
    string Summary)
{
    public TeamStanding? PlayerTeamStanding =>
        FinalStandings.Teams.FirstOrDefault(team => team.OrganizationId == OrganizationId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ArchiveId)
            || string.IsNullOrWhiteSpace(SeasonId)
            || string.IsNullOrWhiteSpace(LeagueId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(ChampionTeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Season archive requires ids, organization, champion, and summary.");
        }

        if (SeasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Season archive year must be positive.");
        }

        FinalStandings.Validate();
        foreach (var stat in TeamStats)
        {
            stat.Validate();
        }

        foreach (var stat in PlayerStats)
        {
            stat.Validate();
        }

        foreach (var stat in GoalieStats)
        {
            stat.Validate();
        }

        foreach (var game in GameResults)
        {
            game.Validate();
        }

        foreach (var recap in GameRecaps)
        {
            recap.Validate();
        }
    }
}
