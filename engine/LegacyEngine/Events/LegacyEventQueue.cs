namespace LegacyEngine.Events;

public sealed class LegacyEventQueue
{
    private readonly List<LegacyEvent> _events = [];

    public int Count => _events.Count;

    public IReadOnlyList<LegacyEvent> PendingEvents => _events
        .OrderBy(item => item.OccurredAt)
        .ThenBy(item => item.EventId, StringComparer.Ordinal)
        .ToArray();

    public LegacyEvent Enqueue(LegacyEvent legacyEvent)
    {
        legacyEvent.Validate();

        if (_events.Any(item => item.EventId == legacyEvent.EventId))
        {
            throw new ArgumentException("Event ids must be unique within the queue.", nameof(legacyEvent));
        }

        var queuedEvent = legacyEvent.MarkQueued();
        _events.Add(queuedEvent);
        return queuedEvent;
    }

    public IReadOnlyList<LegacyEvent> DequeueInDateOrder()
    {
        var ordered = PendingEvents;
        _events.Clear();
        return ordered;
    }
}
