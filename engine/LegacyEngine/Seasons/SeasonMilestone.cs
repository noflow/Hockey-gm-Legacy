namespace LegacyEngine.Seasons;

/// <summary>
/// A scheduled point in a season's calendar: a milestone type on a specific date,
/// with the phase it moves the season into (null for markers) and a label.
/// </summary>
public sealed record SeasonMilestone(
    SeasonMilestoneType Type,
    SeasonDate Date,
    SeasonPhase? TargetPhase,
    string Label)
{
    public bool ChangesPhase => TargetPhase is not null;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new ArgumentException("Milestone label is required.", nameof(Label));
        }
    }
}
