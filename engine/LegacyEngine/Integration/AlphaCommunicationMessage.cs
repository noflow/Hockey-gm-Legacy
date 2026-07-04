using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed record AlphaCommunicationMessage(
    string MessageId,
    DateTimeOffset Date,
    LegacyEventType SourceEventType,
    LegacyEventSeverity Severity,
    string Subject,
    string Body,
    string? PrimaryPersonId);
