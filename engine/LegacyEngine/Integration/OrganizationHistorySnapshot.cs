namespace LegacyEngine.Integration;

public sealed record OrganizationHistorySnapshot(
    string OrganizationId,
    string OrganizationName,
    int PriorSeasonYear,
    int Wins,
    int Losses,
    int OvertimeLosses,
    int Points,
    int GoalsFor,
    int GoalsAgainst,
    string PlayoffResult,
    string PreviousLeagueChampion,
    string Summary)
{
    public string RecordText => $"{Wins}-{Losses}-{OvertimeLosses}, {Points} pts, {GoalsFor} GF / {GoalsAgainst} GA";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(PlayoffResult)
            || string.IsNullOrWhiteSpace(PreviousLeagueChampion)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Organization history requires identity, prior result, champion, and summary.");
        }

        if (Wins < 0 || Losses < 0 || OvertimeLosses < 0 || Points < 0 || GoalsFor < 0 || GoalsAgainst < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Wins), "Organization history stats cannot be negative.");
        }
    }
}
