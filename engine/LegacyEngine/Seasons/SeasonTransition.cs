namespace LegacyEngine.Seasons;

/// <summary>
/// A record of the season moving from one phase to another, and the milestone that
/// triggered it.
/// </summary>
public sealed record SeasonTransition(
    SeasonPhase FromPhase,
    SeasonPhase ToPhase,
    SeasonDate Date,
    SeasonMilestoneType Trigger);
