namespace LegacyEngine.Events;

public sealed class LegacyEventHistory
{
    private readonly List<LegacyEvent> _events = [];

    public int Count => _events.Count;

    public IReadOnlyList<LegacyEvent> AllEvents => _events
        .OrderBy(item => item.OccurredAt)
        .ThenBy(item => item.EventId, StringComparer.Ordinal)
        .ToArray();

    public LegacyEvent Archive(LegacyEvent legacyEvent)
    {
        legacyEvent.Validate();

        if (legacyEvent.Status != LegacyEventStatus.Processed)
        {
            throw new InvalidOperationException("Only processed events can be archived.");
        }

        if (_events.Any(item => item.EventId == legacyEvent.EventId))
        {
            throw new ArgumentException("Event ids must be unique within event history.", nameof(legacyEvent));
        }

        var archived = legacyEvent.MarkArchived();
        _events.Add(archived);
        return archived;
    }

    public IReadOnlyList<LegacyEvent> QueryByPersonId(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        return AllEvents.Where(item => item.Context.HasPerson(personId)).ToArray();
    }

    public IReadOnlyList<LegacyEvent> QueryByOrganizationId(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        return AllEvents.Where(item => item.Context.OrganizationId == organizationId).ToArray();
    }

    public IReadOnlyList<LegacyEvent> QueryByEventType(LegacyEventType eventType) =>
        AllEvents.Where(item => item.EventType == eventType).ToArray();

    public IReadOnlyList<LegacyEvent> QueryByDateRange(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt < startsAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endsAt), "Date range end cannot be before start.");
        }

        return AllEvents
            .Where(item => item.OccurredAt >= startsAt && item.OccurredAt <= endsAt)
            .ToArray();
    }
}
