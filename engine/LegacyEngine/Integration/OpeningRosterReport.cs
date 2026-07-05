using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record OpeningRosterReport(
    int CurrentRosterSize,
    int RequiredRosterSize,
    int Goalies,
    int Defense,
    int Forwards,
    int Prospects,
    int UnsignedPlayers,
    int TrainingCampInvitees,
    int PlayersRequiringDecisions,
    RosterValidationResult ValidationResult)
{
    public bool IsReady =>
        ValidationResult.IsValid
        && UnsignedPlayers == 0
        && PlayersRequiringDecisions == 0;
}
