using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record TrainingCampPlayer(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    TrainingCampInviteType InviteType,
    TrainingCampStatus Status,
    DateOnly InvitedOn,
    PlayerAcquisitionSource AcquisitionSource = PlayerAcquisitionSource.Unknown,
    string? SourceOrganizationId = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Training camp player person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            throw new ArgumentException("Training camp player name is required.", nameof(PlayerName));
        }
    }
}
