namespace LegacyEngine.Events;

public sealed class EventEngine
{
    public EventEngine()
        : this(new LegacyEventQueue(), new LegacyEventHistory())
    {
    }

    public EventEngine(LegacyEventQueue queue, LegacyEventHistory history)
    {
        Queue = queue;
        History = history;
    }

    public LegacyEventQueue Queue { get; }

    public LegacyEventHistory History { get; }

    public LegacyEvent CreateEvent(
        DateTimeOffset occurredAt,
        LegacyEventType eventType,
        LegacyEventSeverity severity,
        LegacyEventVisibility visibility,
        string title,
        string description,
        LegacyEventContext? context = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        string? eventId = null)
    {
        var legacyEvent = new LegacyEvent(
            EventId: eventId ?? CreateEventId(),
            OccurredAt: occurredAt,
            EventType: eventType,
            Severity: severity,
            Visibility: visibility,
            Status: LegacyEventStatus.Created,
            Title: title,
            Description: description,
            Context: context ?? new LegacyEventContext(),
            Metadata: metadata ?? new Dictionary<string, object?>());

        legacyEvent.Validate();
        return legacyEvent;
    }

    public LegacyEvent QueueEvent(LegacyEvent legacyEvent) => Queue.Enqueue(legacyEvent);

    public IReadOnlyList<LegacyEventResult> ProcessQueuedEvents(DateTimeOffset processedAt)
    {
        var results = new List<LegacyEventResult>();

        foreach (var queuedEvent in Queue.DequeueInDateOrder())
        {
            var processedEvent = queuedEvent.MarkProcessed();
            History.Archive(processedEvent);
            results.Add(new LegacyEventResult(
                EventId: processedEvent.EventId,
                Success: true,
                Status: processedEvent.Status,
                ProcessedAt: processedAt,
                Message: "Event processed and archived.",
                Details: new Dictionary<string, object?>
                {
                    ["event_type"] = processedEvent.EventType.ToString(),
                    ["occurred_at"] = processedEvent.OccurredAt
                }));
        }

        return results;
    }

    private static string CreateEventId() => $"event-{Guid.NewGuid():N}";
}
