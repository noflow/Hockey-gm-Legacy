using LegacyEngine.Events;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Seasons;

/// <summary>
/// The v1 Season engine. It owns the calendar and season lifecycle: creating a
/// season, advancing its date, detecting milestones, changing phase automatically,
/// and queuing events. Season timing comes from a rulebook where supplied, so league
/// dates are data rather than engine code.
///
/// Out of scope for v1: schedule generation, standings, games, playoff brackets,
/// awards voting, statistics, save/load, UI, and any game-client layer. The Season
/// engine does not simulate games.
/// </summary>
public sealed class SeasonEngine
{
    private readonly EventEngine _eventEngine;

    public SeasonEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public Season CreateSeason(
        string seasonId,
        string leagueId,
        int seasonYear,
        SeasonSettings? settings = null,
        Rulebook? rulebook = null,
        DateOnly? startingDate = null)
    {
        var effectiveSettings = settings
            ?? (rulebook is not null ? SeasonSettings.FromRulebook(rulebook) : SeasonSettings.Default);
        effectiveSettings.Validate();

        var calendar = SeasonCalendar.Build(seasonYear, effectiveSettings);
        var seasonStart = calendar.SeasonStart.Value;
        var currentDate = startingDate ?? seasonStart.AddDays(-1);

        var season = new Season(
            SeasonId: seasonId,
            LeagueId: leagueId,
            Year: seasonYear,
            Status: StatusFor(calendar, currentDate),
            CurrentPhase: calendar.PhaseOn(currentDate),
            CurrentDate: new SeasonDate(currentDate),
            Calendar: calendar,
            Settings: effectiveSettings);

        season.Validate();

        QueueSeasonEvent(
            season,
            LegacyEventType.SeasonCreated,
            currentDate,
            "Season created",
            $"Season {seasonYear} was created for league {leagueId}.");

        return season;
    }

    public SeasonResult AdvanceTo(Season season, DateOnly toDate)
    {
        season.Validate();

        var previousDate = season.CurrentDate.Value;
        if (toDate < previousDate)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate), "A season cannot advance to an earlier date.");
        }

        var previousPhase = season.CurrentPhase;
        var reached = season.Calendar.MilestonesBetween(previousDate, toDate);
        var transitions = new List<SeasonTransition>();
        var events = new List<LegacyEvent>();
        var current = season;

        foreach (var milestone in reached)
        {
            events.Add(QueueSeasonEvent(
                current,
                LegacyEventType.MilestoneReached,
                milestone.Date.Value,
                $"Milestone reached: {milestone.Label}",
                $"The '{milestone.Label}' milestone was reached.",
                milestone.Type));

            if (milestone.TargetPhase is not { } target || target == current.CurrentPhase)
            {
                continue;
            }

            var fromPhase = current.CurrentPhase;

            if (CloseEventFor(fromPhase) is { } closeEvent)
            {
                events.Add(QueueSeasonEvent(
                    current,
                    closeEvent,
                    milestone.Date.Value,
                    $"{fromPhase} closed",
                    $"The {fromPhase} phase closed.",
                    milestone.Type));
            }

            current = current with { CurrentPhase = target };

            events.Add(QueueSeasonEvent(
                current,
                LegacyEventType.PhaseChanged,
                milestone.Date.Value,
                $"Phase changed to {target}",
                $"The season moved from {fromPhase} to {target}.",
                milestone.Type));

            if (OpenEventFor(target) is { } openEvent)
            {
                events.Add(QueueSeasonEvent(
                    current,
                    openEvent,
                    milestone.Date.Value,
                    $"{target} opened",
                    $"The {target} phase opened.",
                    milestone.Type));
            }

            transitions.Add(new SeasonTransition(fromPhase, target, milestone.Date, milestone.Type));
        }

        current = current with
        {
            CurrentDate = new SeasonDate(toDate),
            Status = StatusFor(current.Calendar, toDate)
        };
        current.Validate();

        return new SeasonResult(
            Season: current,
            PreviousDate: new SeasonDate(previousDate),
            CurrentDate: current.CurrentDate,
            PreviousPhase: previousPhase,
            CurrentPhase: current.CurrentPhase,
            PhaseChanged: previousPhase != current.CurrentPhase,
            MilestonesReached: reached,
            Transitions: transitions,
            CreatedEvents: events,
            Summary: BuildSummary(current, reached, transitions));
    }

    public SeasonResult AdvanceDays(Season season, int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "A season cannot advance a negative number of days.");
        }

        return AdvanceTo(season, season.CurrentDate.Value.AddDays(days));
    }

    /// <summary>Answers "what season phase applies on this date?" without mutating the season.</summary>
    public static SeasonPhase CurrentPhaseFor(Season season, DateOnly onDate)
    {
        ArgumentNullException.ThrowIfNull(season);
        return season.PhaseOn(onDate);
    }

    private static SeasonStatus StatusFor(SeasonCalendar calendar, DateOnly date)
    {
        if (date < calendar.SeasonStart.Value)
        {
            return SeasonStatus.Upcoming;
        }

        return date >= calendar.SeasonEnd.Value ? SeasonStatus.Completed : SeasonStatus.Active;
    }

    private static LegacyEventType? OpenEventFor(SeasonPhase phase) => phase switch
    {
        SeasonPhase.Recruiting => LegacyEventType.RecruitingOpened,
        SeasonPhase.Draft => LegacyEventType.DraftOpened,
        SeasonPhase.FreeAgency => LegacyEventType.FreeAgencyOpened,
        _ => null
    };

    private static LegacyEventType? CloseEventFor(SeasonPhase phase) => phase switch
    {
        SeasonPhase.Recruiting => LegacyEventType.RecruitingClosed,
        SeasonPhase.Draft => LegacyEventType.DraftClosed,
        SeasonPhase.FreeAgency => LegacyEventType.FreeAgencyClosed,
        _ => null
    };

    private static string BuildSummary(
        Season season,
        IReadOnlyList<SeasonMilestone> reached,
        IReadOnlyList<SeasonTransition> transitions) =>
        $"Season {season.Year} advanced to {season.CurrentDate.Value:yyyy-MM-dd}; " +
        $"{reached.Count} milestone(s) and {transitions.Count} phase change(s); now in {season.CurrentPhase}.";

    private LegacyEvent QueueSeasonEvent(
        Season season,
        LegacyEventType eventType,
        DateOnly date,
        string title,
        string description,
        SeasonMilestoneType? milestone = null)
    {
        var details = new Dictionary<string, object?>
        {
            ["season_id"] = season.SeasonId,
            ["league_id"] = season.LeagueId,
            ["season_year"] = season.Year,
            ["phase"] = season.CurrentPhase.ToString()
        };

        if (milestone is { } milestoneType)
        {
            details["milestone"] = milestoneType.ToString();
        }

        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            title,
            description,
            new LegacyEventContext(
                LeagueId: season.LeagueId,
                SeasonId: season.SeasonId),
            details);

        return _eventEngine.QueueEvent(legacyEvent);
    }
}
