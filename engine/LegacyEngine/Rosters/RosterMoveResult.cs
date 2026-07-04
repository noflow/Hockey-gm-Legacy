using LegacyEngine.Events;

namespace LegacyEngine.Rosters;

public sealed record RosterMoveResult(
    bool Success,
    Roster Roster,
    RosterMove Move,
    RosterValidationResult ValidationResult,
    LegacyEvent? CreatedEvent,
    string Message);
