namespace LegacyEngine.Rosters;

public sealed record Roster(
    string RosterId,
    string OrganizationId,
    IReadOnlyList<RosterPlayer> Players)
{
    public IReadOnlyList<RosterPlayer> ActivePlayers => Players
        .Where(player => player.Status == RosterStatus.Active)
        .ToArray();

    public IReadOnlyList<RosterPlayer> CurrentPlayers => Players
        .Where(player => player.CountsTowardRoster)
        .ToArray();

    public RosterPlayer? FindPlayer(string personId) =>
        Players.SingleOrDefault(player => player.PersonId == personId);

    public bool HasActivePlayer(string personId) =>
        Players.Any(player => player.PersonId == personId && player.Status == RosterStatus.Active);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RosterId))
        {
            throw new ArgumentException("Roster id is required.", nameof(RosterId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        foreach (var player in Players)
        {
            player.Validate();
        }

        var duplicateActivePlayer = Players
            .Where(player => player.Status == RosterStatus.Active)
            .GroupBy(player => player.PersonId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateActivePlayer is not null)
        {
            throw new ArgumentException("Roster cannot contain duplicate active player entries.", nameof(Players));
        }
    }
}
