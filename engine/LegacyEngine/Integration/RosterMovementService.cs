using LegacyEngine.RuleEngine;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

/// <summary>
/// Keeps core roster status changes and organization-depth presentation in one
/// place. Affiliate, waiver, prospect-rights, and training-camp decisions stay
/// with their existing rule-driven services.
/// </summary>
public sealed class RosterMovementService
{
    private static readonly RosterMovementType[] CoreMoves =
    {
        RosterMovementType.Activate,
        RosterMovementType.Reserve,
        RosterMovementType.InjuredReserve,
        RosterMovementType.Release
    };

    public RosterDepthChart BuildDepthChart(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var allocation = scenario.OrganizationRoster
            ?? new RosterAllocationService().BuildOrganizationRoster(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var assignments = scenario.CurrentLineup?.Assignments
            .GroupBy(assignment => assignment.PersonId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, LineupRoleAssignment>(StringComparer.Ordinal);
        var rosterStatuses = scenario.AlphaSnapshot.Roster.Players
            .ToDictionary(player => player.PersonId, player => player, StringComparer.Ordinal);

        var players = allocation.Players
            .GroupBy(player => player.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(player => PositionOrder(player.Position))
            .ThenBy(player => GroupOrder(player.Group))
            .ThenByDescending(player => player.Age)
            .ThenBy(player => player.PlayerName, StringComparer.Ordinal)
            .Select(player =>
            {
                assignments.TryGetValue(player.PersonId, out var assignment);
                rosterStatuses.TryGetValue(player.PersonId, out var rosterPlayer);
                return new RosterDepthPlayer(
                    player.PersonId,
                    player.PlayerName,
                    player.Position,
                    player.Age,
                    player.Group,
                    player.CurrentLevel,
                    player.CurrentTeamName ?? player.CurrentLevel,
                    rosterPlayer?.Status,
                    assignment?.SlotLabel ?? "Not in lineup",
                    assignment is null ? "Depth / prospect path" : LineupDisplay.Role(assignment.CurrentRole),
                    player.ContractId is null ? "Unsigned rights / no active contract" : "Signed organization contract");
            })
            .ToArray();

        var groups = Enum.GetValues<RosterPosition>()
            .Where(position => position != RosterPosition.Unknown)
            .Select(position => new RosterDepthGroup(
                position,
                PositionLabel(position),
                players.Where(player => player.Position == position).ToArray()))
            .ToArray();
        var chart = new RosterDepthChart(
            scenario.Organization.OrganizationId,
            scenario.CurrentDate,
            groups,
            players.Length,
            $"{players.Length} organization players across {groups.Count(group => group.Players.Count > 0)} position groups. Select a player for lineup, contract, development, and movement actions.");
        chart.Validate();
        return chart;
    }

    public IReadOnlyList<RosterMovementOption> BuildMovementOptions(
        NewGmScenarioSnapshot scenario,
        string personId,
        Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var player = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        if (player is null)
        {
            return Array.Empty<RosterMovementOption>();
        }

        var options = CoreMoves
            .Select(move => OptionFor(scenario, player, move, rulebook ?? scenario.LeagueProfile.Rulebook))
            .ToArray();
        foreach (var option in options)
        {
            option.Validate();
        }

        return options;
    }

    public RosterMovementResult Move(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        RosterMovementType movementType,
        string reason = "GM roster movement")
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(personId))
        {
            return Failure(scenario, "Select a player before changing roster status.");
        }

        var player = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        if (player is null)
        {
            return Failure(scenario, "Selected player is not on the game-day roster.");
        }

        var targetStatus = TargetStatus(movementType);
        var option = OptionFor(scenario, player, movementType, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        if (!option.IsAvailable)
        {
            return Failure(scenario, option.Reason);
        }

        var move = new RosterMove(
            MoveTypeFor(movementType),
            personId,
            scenario.CurrentDate,
            player.Position,
            targetStatus,
            player.Age,
            player.IsImport,
            string.IsNullOrWhiteSpace(reason) ? "GM roster movement" : reason,
            player.AcquisitionSource);
        var validator = registry.Rulebook?.RosterRules is null ? null : new RosterRuleValidator(registry.Rulebook);
        var result = registry.RosterEngine.MovePlayer(scenario.AlphaSnapshot.Roster, move, validator);
        if (!result.Success)
        {
            return new RosterMovementResult(false, scenario, result, result.Message);
        }

        var updated = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { Roster = result.Roster },
            OrganizationRoster = null
        };

        if (targetStatus != RosterStatus.Active && updated.CurrentLineup is not null)
        {
            var assignment = updated.CurrentLineup.Assignments.FirstOrDefault(item => item.PersonId == personId && item.Slot != LineupSlot.HealthyScratch);
            if (assignment is not null)
            {
                var lineupResult = new LineupService().RemovePlayerFromSlot(updated, assignment.Slot);
                if (lineupResult.Success)
                {
                    updated = lineupResult.ScenarioSnapshot;
                }
            }
        }

        updated = new RosterAllocationService().EnsureAllocation(updated, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var message = $"{NameFor(updated, personId)} moved to {StatusLabel(targetStatus)}. Review the lineup if a slot opened.";
        var movementResult = new RosterMovementResult(true, updated, result, message);
        movementResult.Validate();
        return movementResult;
    }

    internal static string PositionLabel(RosterPosition position) => position switch
    {
        RosterPosition.Center => "Centers",
        RosterPosition.LeftWing => "Left Wings",
        RosterPosition.RightWing => "Right Wings",
        RosterPosition.Defense => "Defensemen",
        RosterPosition.Goalie => "Goalies",
        _ => "Unassigned Position"
    };

    private static RosterMovementOption OptionFor(NewGmScenarioSnapshot scenario, RosterPlayer player, RosterMovementType movementType, Rulebook? rulebook)
    {
        var target = TargetStatus(movementType);
        if (player.Status == target)
        {
            return new RosterMovementOption(movementType, LabelFor(movementType), false, $"Player is already {StatusLabel(target)}.");
        }

        if (player.Status == RosterStatus.Released)
        {
            return new RosterMovementOption(movementType, LabelFor(movementType), false, "Released players cannot be moved back onto the roster in this view.");
        }

        var hypothetical = scenario.AlphaSnapshot.Roster with
        {
            Players = scenario.AlphaSnapshot.Roster.Players
                .Select(item => item.PersonId == player.PersonId ? item.WithStatus(target, scenario.CurrentDate) : item)
                .ToArray()
        };
        var validator = rulebook?.RosterRules is null ? null : new RosterRuleValidator(rulebook);
        var validation = new RosterEngine().ValidateRoster(hypothetical, validator);
        if (!validation.IsValid)
        {
            return new RosterMovementOption(movementType, LabelFor(movementType), false, validation.Message);
        }

        var reason = movementType switch
        {
            RosterMovementType.Activate => "Player can be returned to the active roster.",
            RosterMovementType.Reserve => "Move the player out of the active lineup while keeping roster membership.",
            RosterMovementType.InjuredReserve => "Place the player on injured reserve; lineup usage will be cleared.",
            RosterMovementType.Release => "Release the player from this roster. Contract and rights review may still be required.",
            _ => "Available roster movement."
        };
        return new RosterMovementOption(movementType, LabelFor(movementType), true, reason);
    }

    private static RosterMoveType MoveTypeFor(RosterMovementType movementType) => movementType switch
    {
        RosterMovementType.Activate => RosterMoveType.MoveToActive,
        RosterMovementType.Reserve => RosterMoveType.MoveToReserve,
        RosterMovementType.InjuredReserve => RosterMoveType.MoveToInjuredReserve,
        RosterMovementType.Release => RosterMoveType.Release,
        _ => throw new ArgumentOutOfRangeException(nameof(movementType))
    };

    private static RosterStatus TargetStatus(RosterMovementType movementType) => movementType switch
    {
        RosterMovementType.Activate => RosterStatus.Active,
        RosterMovementType.Reserve => RosterStatus.Reserve,
        RosterMovementType.InjuredReserve => RosterStatus.InjuredReserve,
        RosterMovementType.Release => RosterStatus.Released,
        _ => throw new ArgumentOutOfRangeException(nameof(movementType))
    };

    private static string LabelFor(RosterMovementType movementType) => movementType switch
    {
        RosterMovementType.Activate => "Move to Active",
        RosterMovementType.Reserve => "Move to Reserve",
        RosterMovementType.InjuredReserve => "Place on Injured Reserve",
        RosterMovementType.Release => "Release Player",
        _ => movementType.ToString()
    };

    private static string StatusLabel(RosterStatus status) => status switch
    {
        RosterStatus.Active => "Active",
        RosterStatus.Reserve => "Reserve",
        RosterStatus.InjuredReserve => "Injured Reserve",
        RosterStatus.AssignedToAffiliate => "Affiliate",
        RosterStatus.Released => "Released",
        _ => status.ToString()
    };

    private static string NameFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.Concat(scenario.AlphaSnapshot.Players)
            .FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.OrganizationRoster?.Players.FirstOrDefault(player => player.PersonId == personId)?.PlayerName
        ?? personId;

    private static int PositionOrder(RosterPosition position) => position switch
    {
        RosterPosition.Center => 0,
        RosterPosition.LeftWing => 1,
        RosterPosition.RightWing => 2,
        RosterPosition.Defense => 3,
        RosterPosition.Goalie => 4,
        _ => 5
    };

    private static int GroupOrder(OrganizationRosterGroup group) => group switch
    {
        OrganizationRosterGroup.NhlActiveRoster => 0,
        OrganizationRosterGroup.AhlAffiliateRoster => 1,
        OrganizationRosterGroup.OtherContracted => 2,
        OrganizationRosterGroup.SignedJuniorReturn => 3,
        OrganizationRosterGroup.UnsignedProspectRights => 4,
        OrganizationRosterGroup.InjuredOrUnavailable => 5,
        _ => 6
    };

    private static RosterMovementResult Failure(NewGmScenarioSnapshot scenario, string message) =>
        new(false, scenario, null, message);
}
