using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed record AlphaInboxItem(
    string InboxItemId,
    DateTimeOffset Date,
    LegacyEventType EventType,
    LegacyEventSeverity Severity,
    string Title,
    string Summary,
    string? PrimaryPersonId);
