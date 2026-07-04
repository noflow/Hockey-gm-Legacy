namespace LegacyEngine.Events;

public sealed record LegacyEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    LegacyEventType EventType,
    LegacyEventSeverity Severity,
    LegacyEventVisibility Visibility,
    LegacyEventStatus Status,
    string Title,
    string Description,
    LegacyEventContext Context,
    IReadOnlyDictionary<string, object?> Metadata)
{
    public LegacyEvent MarkQueued() => this with { Status = LegacyEventStatus.Queued };

    public LegacyEvent MarkProcessed() => this with { Status = LegacyEventStatus.Processed };

    public LegacyEvent MarkArchived() => this with { Status = LegacyEventStatus.Archived };

    public LegacyEvent MarkFailed() => this with { Status = LegacyEventStatus.Failed };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId))
        {
            throw new ArgumentException("Event id is required.", nameof(EventId));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Event title is required.", nameof(Title));
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Event description is required.", nameof(Description));
        }

        if (Context is null)
        {
            throw new ArgumentNullException(nameof(Context), "Event context is required.");
        }

        if (Metadata is null)
        {
            throw new ArgumentNullException(nameof(Metadata), "Event metadata dictionary is required.");
        }
    }
}
