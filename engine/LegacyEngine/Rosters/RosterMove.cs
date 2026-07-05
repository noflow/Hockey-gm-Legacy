namespace LegacyEngine.Rosters;

public sealed record RosterMove(
    RosterMoveType MoveType,
    string PersonId,
    DateOnly Date,
    RosterPosition? Position = null,
    RosterStatus? TargetStatus = null,
    int? Age = null,
    bool IsImport = false,
    string Reason = "",
    PlayerAcquisitionSource AcquisitionSource = PlayerAcquisitionSource.Unknown)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Roster move person id is required.", nameof(PersonId));
        }

        if (MoveType == RosterMoveType.Add && Position is null)
        {
            throw new ArgumentException("Add roster moves require a position.", nameof(Position));
        }
    }
}
