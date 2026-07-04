using LegacyEngine.Events;

namespace LegacyEngine.World;

public sealed class WorldEngine
{
    public WorldEngine(WorldState state, EventEngine eventEngine)
    {
        state.Validate();
        State = state;
        EventEngine = eventEngine;
    }

    public WorldState State { get; private set; }

    public EventEngine EventEngine { get; }

    public static WorldEngine CreateWorld(
        string worldName,
        DateOnly startDate,
        WorldPhase initialPhase = WorldPhase.Preseason,
        WorldSettings? settings = null,
        EventEngine? eventEngine = null,
        WorldId? worldId = null)
    {
        var state = new WorldState(
            WorldId: worldId ?? WorldId.New(),
            WorldName: worldName,
            Clock: new WorldClock(new WorldDate(startDate)),
            CurrentPhase: initialPhase,
            Settings: settings ?? new WorldSettings(),
            SystemRegistrations: new[]
            {
                new WorldSystemRegistration("EventEngine", true)
            });

        return new WorldEngine(state, eventEngine ?? new EventEngine());
    }

    public DailySimulationResult AdvanceOneDay() => AdvanceOneDay(processEvents: true);

    public IReadOnlyList<DailySimulationResult> AdvanceDays(int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "World cannot advance a negative number of days.");
        }

        var results = new List<DailySimulationResult>();
        for (var day = 0; day < days; day++)
        {
            results.Add(AdvanceOneDay());
        }

        return results;
    }

    public void SetPhase(WorldPhase phase)
    {
        State = State with { CurrentPhase = phase };
        State.Validate();
    }

    private DailySimulationResult AdvanceOneDay(bool processEvents)
    {
        var previousDate = State.CurrentDate;
        State = State with { Clock = State.Clock.AdvanceDays(1) };
        State.Validate();

        var processedEvents = processEvents
            ? EventEngine.ProcessQueuedEvents(ToWorldInstant(State.CurrentDate))
            : Array.Empty<LegacyEventResult>();

        return new DailySimulationResult(
            WorldId: State.WorldId,
            PreviousDate: previousDate,
            CurrentDate: State.CurrentDate,
            CurrentSeasonYear: State.CurrentSeasonYear,
            CurrentPhase: State.CurrentPhase,
            ProcessedEventCount: processedEvents.Count,
            ProcessedEvents: processedEvents);
    }

    private static DateTimeOffset ToWorldInstant(WorldDate date) =>
        new(date.Value.Year, date.Value.Month, date.Value.Day, 0, 0, 0, TimeSpan.Zero);
}
