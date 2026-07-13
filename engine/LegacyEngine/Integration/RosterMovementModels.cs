using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum RosterMovementType
{
    Activate,
    Reserve,
    InjuredReserve,
    Release
}

public sealed record RosterMovementOption(
    RosterMovementType MovementType,
    string Label,
    bool IsAvailable,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Label) || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Roster movement option requires a label and reason.");
        }
    }
}

public sealed record RosterDepthPlayer(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int Age,
    OrganizationRosterGroup Group,
    string Level,
    string Team,
    RosterStatus? RosterStatus,
    string LineupSlot,
    string LineupRole,
    string ContractSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || Age < 0
            || string.IsNullOrWhiteSpace(Level)
            || string.IsNullOrWhiteSpace(Team)
            || string.IsNullOrWhiteSpace(LineupSlot)
            || string.IsNullOrWhiteSpace(LineupRole)
            || string.IsNullOrWhiteSpace(ContractSummary))
        {
            throw new ArgumentException("Roster depth player requires readable organization context.");
        }
    }
}

public sealed record RosterDepthGroup(
    RosterPosition Position,
    string Label,
    IReadOnlyList<RosterDepthPlayer> Players)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new ArgumentException("Roster depth group requires a label.");
        }

        foreach (var player in Players)
        {
            player.Validate();
        }
    }
}

public sealed record RosterDepthChart(
    string OrganizationId,
    DateOnly AsOf,
    IReadOnlyList<RosterDepthGroup> Groups,
    int TotalPlayers,
    string Summary)
{
    public RosterDepthGroup Group(RosterPosition position) =>
        Groups.FirstOrDefault(group => group.Position == position)
        ?? new RosterDepthGroup(position, RosterMovementService.PositionLabel(position), Array.Empty<RosterDepthPlayer>());

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || TotalPlayers < 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Roster depth chart is invalid.");
        }

        foreach (var group in Groups)
        {
            group.Validate();
        }

        if (Groups.SelectMany(group => group.Players).GroupBy(player => player.PersonId, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Roster depth chart cannot duplicate a player across position groups.");
        }
    }
}

public sealed record RosterMovementResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    RosterMoveResult? RosterResult,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Roster movement result requires a message.");
        }
    }
}
