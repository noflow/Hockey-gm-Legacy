namespace LegacyEngine.Integration;

public sealed record PlayerTeamHistory(
    string PersonId,
    string PersonName,
    string TeamName,
    string LeagueName,
    int FromSeasonYear,
    int ToSeasonYear,
    string Role,
    string Notes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(LeagueName)
            || string.IsNullOrWhiteSpace(Role)
            || string.IsNullOrWhiteSpace(Notes))
        {
            throw new ArgumentException("Player team history requires person, team, league, role, and notes.");
        }

        if (FromSeasonYear > ToSeasonYear)
        {
            throw new ArgumentException("Player team history season range is invalid.");
        }
    }
}
