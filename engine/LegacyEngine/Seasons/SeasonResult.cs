using LegacyEngine.Events;

namespace LegacyEngine.Seasons;

/// <summary>
/// Describes what changed when a season advanced: the resulting season, the date and
/// phase before and after, any milestones reached, any phase transitions, and the
/// events that were queued.
/// </summary>
public sealed record SeasonResult(
    Season Season,
    SeasonDate PreviousDate,
    SeasonDate CurrentDate,
    SeasonPhase PreviousPhase,
    SeasonPhase CurrentPhase,
    bool PhaseChanged,
    IReadOnlyList<SeasonMilestone> MilestonesReached,
    IReadOnlyList<SeasonTransition> Transitions,
    IReadOnlyList<LegacyEvent> CreatedEvents,
    string Summary);
