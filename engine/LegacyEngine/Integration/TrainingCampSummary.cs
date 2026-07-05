using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record TrainingCampSummary(
    int PlayersInvited,
    int PlayersKept,
    int PlayersCutOrReleased,
    int PlayersAssignedOrReturned,
    int InjuryConcerns,
    RosterValidationResult RosterValidationResult,
    string StaffSummary)
{
    public void Validate()
    {
        if (PlayersInvited < 0
            || PlayersKept < 0
            || PlayersCutOrReleased < 0
            || PlayersAssignedOrReturned < 0
            || InjuryConcerns < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayersInvited), "Training camp summary counts cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(StaffSummary))
        {
            throw new ArgumentException("Training camp staff summary is required.", nameof(StaffSummary));
        }
    }
}
