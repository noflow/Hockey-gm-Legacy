namespace LegacyEngine.Rosters;

public sealed record RosterPlayer(
    string PersonId,
    RosterPosition Position,
    RosterStatus Status,
    DateOnly JoinedDate,
    DateOnly? ReleasedDate = null,
    int? Age = null,
    bool IsImport = false,
    PlayerAcquisitionSource AcquisitionSource = PlayerAcquisitionSource.Unknown)
{
    public bool CountsTowardRoster => Status is RosterStatus.Active or RosterStatus.Reserve or RosterStatus.InjuredReserve;

    public bool CountsTowardActiveRoster => Status == RosterStatus.Active;

    public bool IsGoalie => Position == RosterPosition.Goalie;

    public bool IsOverage(int overageAge = 20) => Age.HasValue && Age.Value >= overageAge;

    public RosterPlayer WithStatus(RosterStatus status, DateOnly date) =>
        this with
        {
            Status = status,
            ReleasedDate = status == RosterStatus.Released ? date : ReleasedDate
        };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Roster player person id is required.", nameof(PersonId));
        }

        if (Age is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Roster player age cannot be negative.");
        }

        if (Status == RosterStatus.Released && ReleasedDate is null)
        {
            throw new ArgumentException("Released roster players must have a released date.", nameof(ReleasedDate));
        }
    }
}
