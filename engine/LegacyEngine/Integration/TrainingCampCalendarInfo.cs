using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record TrainingCampCalendarInfo(
    DateOnly OpensOn,
    DateOnly ClosesOn,
    int DaysUntilRosterDeadline,
    int CurrentCampRosterCount,
    int RequiredOpeningRosterSize,
    int PlayersOverLimit,
    RosterValidationResult RosterValidationResult)
{
    public bool IsRosterCompliant => RosterValidationResult.IsValid;
}
