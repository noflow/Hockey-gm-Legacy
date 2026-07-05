using LegacyEngine.Events;

namespace LegacyEngine.Communication;

public sealed record CommunicationMessage(
    string MessageId,
    DateOnly SentOn,
    CommunicationChannel Channel,
    CommunicationVisibility Visibility,
    CommunicationSource Source,
    IReadOnlyList<CommunicationRecipient> Recipients,
    string Subject,
    string Body,
    LegacyEventSeverity Severity,
    string? OrganizationId,
    string? SourceEventId,
    LegacyEventType? SourceEventType,
    IReadOnlyDictionary<string, object?> Metadata)
{
    public bool IsImportant => Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical;

    public DateTimeOffset SentAt => new(SentOn.Year, SentOn.Month, SentOn.Day, 12, 0, 0, TimeSpan.Zero);

    public string? PrimaryRecipientPersonId =>
        Recipients.Select(recipient => recipient.PersonId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

    public bool HasRecipient(string personOrOrganizationId) =>
        Recipients.Any(recipient => recipient.Matches(personOrOrganizationId));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MessageId))
        {
            throw new ArgumentException("Message id is required.", nameof(MessageId));
        }

        if (string.IsNullOrWhiteSpace(Subject))
        {
            throw new ArgumentException("Message subject is required.", nameof(Subject));
        }

        if (string.IsNullOrWhiteSpace(Body))
        {
            throw new ArgumentException("Message body is required.", nameof(Body));
        }

        Source.Validate();

        if (Recipients is null || Recipients.Count == 0)
        {
            throw new ArgumentException("A message must have at least one recipient.", nameof(Recipients));
        }

        foreach (var recipient in Recipients)
        {
            recipient.Validate();
        }

        if (Metadata is null)
        {
            throw new ArgumentNullException(nameof(Metadata), "Message metadata dictionary is required.");
        }
    }
}
