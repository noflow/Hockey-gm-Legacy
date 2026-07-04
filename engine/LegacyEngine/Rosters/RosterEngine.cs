using LegacyEngine.Events;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Rosters;

public sealed class RosterEngine
{
    private readonly EventEngine _eventEngine;

    public RosterEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public Roster CreateRoster(string rosterId, string organizationId)
    {
        var roster = new Roster(rosterId, organizationId, Array.Empty<RosterPlayer>());
        roster.Validate();
        return roster;
    }

    public RosterMoveResult AddPlayer(Roster roster, RosterMove move, RosterRuleValidator? validator = null)
    {
        roster.Validate();
        move.Validate();

        if (move.MoveType != RosterMoveType.Add)
        {
            throw new ArgumentException("Roster move must be Add.", nameof(move));
        }

        if (roster.HasActivePlayer(move.PersonId))
        {
            return Failure(roster, move, "DUPLICATE_ACTIVE_PLAYER", "Player is already active on this roster.");
        }

        var player = new RosterPlayer(
            PersonId: move.PersonId,
            Position: move.Position!.Value,
            Status: move.TargetStatus ?? RosterStatus.Active,
            JoinedDate: move.Date,
            Age: move.Age,
            IsImport: move.IsImport);

        var updated = roster with { Players = roster.Players.Append(player).ToArray() };
        var validation = ValidateRoster(updated, validator);
        if (!validation.IsValid)
        {
            return Failure(updated, move, validation);
        }

        var legacyEvent = QueueRosterEvent(
            updated,
            move.PersonId,
            LegacyEventType.PlayerAddedToRoster,
            move.Date,
            "Player added to roster",
            "A player was added to the roster.");

        return Success(updated, move, validation, legacyEvent, "Player added to roster.");
    }

    public RosterMoveResult RemovePlayer(Roster roster, RosterMove move, RosterRuleValidator? validator = null)
    {
        roster.Validate();
        move.Validate();

        if (move.MoveType != RosterMoveType.Remove)
        {
            throw new ArgumentException("Roster move must be Remove.", nameof(move));
        }

        var player = roster.FindPlayer(move.PersonId);
        if (player is null)
        {
            return Failure(roster, move, "PLAYER_NOT_ON_ROSTER", "Player is not on this roster.");
        }

        var updated = roster with { Players = roster.Players.Where(item => item.PersonId != move.PersonId).ToArray() };
        var validation = ValidateRoster(updated, validator);
        if (!validation.IsValid)
        {
            return Failure(updated, move, validation);
        }

        var legacyEvent = QueueRosterEvent(
            updated,
            move.PersonId,
            LegacyEventType.PlayerRemovedFromRoster,
            move.Date,
            "Player removed from roster",
            "A player was removed from the roster.");

        return Success(updated, move, validation, legacyEvent, "Player removed from roster.");
    }

    public RosterMoveResult MovePlayer(Roster roster, RosterMove move, RosterRuleValidator? validator = null)
    {
        roster.Validate();
        move.Validate();

        var targetStatus = move.MoveType switch
        {
            RosterMoveType.MoveToActive => RosterStatus.Active,
            RosterMoveType.MoveToReserve => RosterStatus.Reserve,
            RosterMoveType.MoveToInjuredReserve => RosterStatus.InjuredReserve,
            RosterMoveType.Release => RosterStatus.Released,
            _ => throw new ArgumentException("Roster move must target a status change.", nameof(move))
        };

        var player = roster.FindPlayer(move.PersonId);
        if (player is null)
        {
            return Failure(roster, move, "PLAYER_NOT_ON_ROSTER", "Player is not on this roster.");
        }

        if (targetStatus == RosterStatus.Active && roster.Players.Any(item => item.PersonId == move.PersonId && item.Status == RosterStatus.Active && item != player))
        {
            return Failure(roster, move, "DUPLICATE_ACTIVE_PLAYER", "Player is already active on this roster.");
        }

        var movedPlayer = player.WithStatus(targetStatus, move.Date);
        var updated = roster with
        {
            Players = roster.Players.Select(item => item.PersonId == move.PersonId ? movedPlayer : item).ToArray()
        };

        var validation = ValidateRoster(updated, validator);
        if (!validation.IsValid)
        {
            return Failure(updated, move, validation);
        }

        var eventType = targetStatus switch
        {
            RosterStatus.InjuredReserve => LegacyEventType.PlayerMovedToInjuredReserve,
            RosterStatus.Released => LegacyEventType.PlayerReleased,
            _ => LegacyEventType.PlayerAddedToRoster
        };

        var title = targetStatus switch
        {
            RosterStatus.InjuredReserve => "Player moved to injured reserve",
            RosterStatus.Released => "Player released",
            _ => "Player roster status changed"
        };

        var legacyEvent = QueueRosterEvent(updated, move.PersonId, eventType, move.Date, title, title + ".");
        return Success(updated, move, validation, legacyEvent, title + ".");
    }

    public RosterValidationResult ValidateRoster(Roster roster, RosterRuleValidator? validator = null)
    {
        roster.Validate();

        if (validator is null)
        {
            return RosterValidationResult.Valid();
        }

        var currentPlayers = roster.CurrentPlayers;
        var activePlayers = roster.ActivePlayers;
        var ruleResult = validator.Validate(new RosterValidationRequest(
            TotalPlayers: currentPlayers.Count,
            ActivePlayers: activePlayers.Count,
            Goalies: currentPlayers.Count(player => player.IsGoalie),
            OveragePlayers: currentPlayers.Count(player => player.IsOverage()),
            ImportPlayers: currentPlayers.Count(player => player.IsImport)));

        return ruleResult.IsValid
            ? RosterValidationResult.Valid(ruleResult.Message)
            : RosterValidationResult.Failure(ruleResult.RuleCode, ruleResult.Message, ruleResult.Details);
    }

    private LegacyEvent QueueRosterEvent(
        Roster roster,
        string personId,
        LegacyEventType eventType,
        DateOnly date,
        string title,
        string description)
    {
        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: personId,
                OrganizationId: roster.OrganizationId),
            new Dictionary<string, object?>
            {
                ["roster_id"] = roster.RosterId,
                ["organization_id"] = roster.OrganizationId
            });

        return _eventEngine.QueueEvent(legacyEvent);
    }

    private static RosterMoveResult Success(
        Roster roster,
        RosterMove move,
        RosterValidationResult validation,
        LegacyEvent legacyEvent,
        string message) =>
        new(true, roster, move, validation, legacyEvent, message);

    private static RosterMoveResult Failure(
        Roster roster,
        RosterMove move,
        string ruleCode,
        string message) =>
        Failure(roster, move, RosterValidationResult.Failure(ruleCode, message));

    private static RosterMoveResult Failure(
        Roster roster,
        RosterMove move,
        RosterValidationResult validation) =>
        new(false, roster, move, validation, null, validation.Message);
}
