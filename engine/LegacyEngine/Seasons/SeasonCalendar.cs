namespace LegacyEngine.Seasons;

/// <summary>
/// The ordered set of milestones for one season year, built from
/// <see cref="SeasonSettings"/>. It answers date questions: which milestones fall in
/// a window, and which phase applies on a given date. Independent leagues each hold
/// their own calendar, so their schedules never interfere.
/// </summary>
public sealed record SeasonCalendar(
    int SeasonYear,
    SeasonDate SeasonStart,
    IReadOnlyList<SeasonMilestone> Milestones)
{
    public static SeasonCalendar Build(int seasonYear, SeasonSettings settings)
    {
        settings.Validate();

        var start = new DateOnly(seasonYear, settings.SeasonStartMonth, settings.SeasonStartDay);
        var milestones = SeasonMilestones.InScheduleOrder
            .Select(type => new SeasonMilestone(
                Type: type,
                Date: new SeasonDate(start.AddDays(settings.OffsetFor(type))),
                TargetPhase: SeasonMilestones.TargetPhase(type),
                Label: SeasonMilestones.Label(type)))
            .OrderBy(milestone => milestone.Date.Value)
            .ThenBy(milestone => (int)milestone.Type)
            .ToArray();

        var calendar = new SeasonCalendar(seasonYear, new SeasonDate(start), milestones);
        calendar.Validate();
        return calendar;
    }

    public SeasonDate SeasonEnd => Milestones
        .OrderBy(milestone => milestone.Date.Value)
        .ThenBy(milestone => (int)milestone.Type)
        .Last()
        .Date;

    public IReadOnlyList<SeasonMilestone> MilestonesOn(DateOnly date) =>
        Ordered(Milestones.Where(milestone => milestone.Date.Value == date));

    public IReadOnlyList<SeasonMilestone> MilestonesBetween(DateOnly afterExclusive, DateOnly throughInclusive) =>
        Ordered(Milestones.Where(milestone =>
            milestone.Date.Value > afterExclusive && milestone.Date.Value <= throughInclusive));

    public SeasonPhase PhaseOn(DateOnly date)
    {
        var current = Milestones
            .Where(milestone => milestone.TargetPhase is not null && milestone.Date.Value <= date)
            .OrderBy(milestone => milestone.Date.Value)
            .ThenBy(milestone => (int)milestone.Type)
            .LastOrDefault();

        return current?.TargetPhase ?? SeasonPhase.Offseason;
    }

    public void Validate()
    {
        if (Milestones.Count == 0)
        {
            throw new ArgumentException("A season calendar must contain at least one milestone.", nameof(Milestones));
        }

        foreach (var milestone in Milestones)
        {
            milestone.Validate();
        }

        if (Milestones.Select(milestone => milestone.Type).Distinct().Count() != Milestones.Count)
        {
            throw new ArgumentException("Season milestone types must be unique within a calendar.", nameof(Milestones));
        }
    }

    private static IReadOnlyList<SeasonMilestone> Ordered(IEnumerable<SeasonMilestone> milestones) =>
        milestones
            .OrderBy(milestone => milestone.Date.Value)
            .ThenBy(milestone => (int)milestone.Type)
            .ToArray();
}
