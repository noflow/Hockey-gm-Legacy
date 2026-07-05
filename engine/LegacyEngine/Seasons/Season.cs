namespace LegacyEngine.Seasons;

/// <summary>
/// A single league season: its year, status, current phase and date, calendar, and
/// timing settings. A season references its league by id, so multiple leagues can
/// run independent calendars at the same time. Mutations are immutable
/// <c>with</c> copies; Event Engine integration lives in <see cref="SeasonEngine"/>.
/// </summary>
public sealed record Season(
    string SeasonId,
    string LeagueId,
    int Year,
    SeasonStatus Status,
    SeasonPhase CurrentPhase,
    SeasonDate CurrentDate,
    SeasonCalendar Calendar,
    SeasonSettings Settings)
{
    /// <summary>The phase the season is in on the given date, computed from the calendar.</summary>
    public SeasonPhase PhaseOn(DateOnly date) => Calendar.PhaseOn(date);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SeasonId))
        {
            throw new ArgumentException("Season id is required.", nameof(SeasonId));
        }

        if (string.IsNullOrWhiteSpace(LeagueId))
        {
            throw new ArgumentException("League id is required.", nameof(LeagueId));
        }

        if (Year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Year), "Season year must be positive.");
        }

        Settings.Validate();
        Calendar.Validate();

        if (Calendar.SeasonYear != Year)
        {
            throw new ArgumentException("Season calendar year must match the season year.", nameof(Calendar));
        }
    }
}
