using LegacyEngine.Events;
using LegacyEngine.Integration;

namespace LegacyEngine.Communication;

public sealed class CommunicationEngine
{
    private readonly EventEngine _eventEngine;
    private readonly List<CommunicationMessage> _messages = [];
    private readonly List<Rumor> _rumors = [];
    private readonly List<OrganizationKnowledgeItem> _knowledge = [];

    public CommunicationEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public IReadOnlyList<CommunicationMessage> AllMessages => Ordered(_messages);

    public IReadOnlyList<Rumor> AllRumors => _rumors
        .OrderBy(item => item.CreatedOn)
        .ThenBy(item => item.RumorId, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<OrganizationKnowledgeItem> AllKnowledge => _knowledge
        .OrderBy(item => item.LearnedOn)
        .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
        .ToArray();

    public CommunicationMessage SendMessage(
        CommunicationSource source,
        IEnumerable<CommunicationRecipient> recipients,
        CommunicationChannel channel,
        CommunicationVisibility visibility,
        string subject,
        string body,
        DateOnly sentOn,
        LegacyEventSeverity severity = LegacyEventSeverity.Info,
        string? organizationId = null,
        string? sourceEventId = null,
        LegacyEventType? sourceEventType = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        string? messageId = null)
    {
        var message = new CommunicationMessage(
            MessageId: messageId ?? CreateMessageId(),
            SentOn: sentOn,
            Channel: channel,
            Visibility: visibility,
            Source: source,
            Recipients: recipients?.ToArray() ?? Array.Empty<CommunicationRecipient>(),
            Subject: subject,
            Body: body,
            Severity: severity,
            OrganizationId: organizationId ?? source.OrganizationId,
            SourceEventId: sourceEventId,
            SourceEventType: sourceEventType,
            Metadata: metadata ?? new Dictionary<string, object?>());

        message.Validate();
        _messages.Add(message);
        return message;
    }

    public CommunicationMessage CreateMessageFromEvent(
        LegacyEvent legacyEvent,
        CommunicationSource source,
        IEnumerable<CommunicationRecipient> recipients,
        CommunicationChannel channel = CommunicationChannel.Announcement)
    {
        legacyEvent.Validate();

        return SendMessage(
            source: source,
            recipients: recipients,
            channel: channel,
            visibility: MapVisibility(legacyEvent.Visibility),
            subject: legacyEvent.Title,
            body: legacyEvent.Description,
            sentOn: DateOnly.FromDateTime(legacyEvent.OccurredAt.UtcDateTime),
            severity: legacyEvent.Severity,
            organizationId: legacyEvent.Context.OrganizationId,
            sourceEventId: legacyEvent.EventId,
            sourceEventType: legacyEvent.EventType,
            metadata: new Dictionary<string, object?>
            {
                ["source_event_id"] = legacyEvent.EventId,
                ["source_event_type"] = legacyEvent.EventType.ToString()
            });
    }

    public Rumor CreateRumor(
        string subject,
        string body,
        int reliability,
        CommunicationVisibility visibility,
        DateOnly createdOn,
        string? subjectPersonId = null,
        string? organizationId = null,
        string? sourceEventId = null,
        string? originId = null,
        string? rumorId = null)
    {
        var rumor = Rumor.Create(
            rumorId ?? CreateRumorId(),
            createdOn,
            subject,
            body,
            reliability,
            visibility,
            subjectPersonId,
            organizationId,
            sourceEventId,
            originId);

        _rumors.Add(rumor);
        return rumor;
    }

    public Rumor CorroborateRumor(Rumor rumor, int reliabilityDelta)
    {
        rumor.Validate();

        var updated = rumor.Corroborate(reliabilityDelta);
        var index = _rumors.FindIndex(item => item.RumorId == rumor.RumorId);
        if (index >= 0)
        {
            _rumors[index] = updated;
        }
        else
        {
            _rumors.Add(updated);
        }

        return updated;
    }

    public OrganizationKnowledgeItem RecordKnowledge(
        string organizationId,
        string topic,
        string detail,
        RumorConfidence confidence,
        DateOnly learnedOn,
        string? subjectPersonId = null,
        string? sourceMessageId = null,
        string? sourceRumorId = null,
        string? sourceEventId = null,
        string? knowledgeId = null)
    {
        var item = new OrganizationKnowledgeItem(
            KnowledgeId: knowledgeId ?? CreateKnowledgeId(),
            OrganizationId: organizationId,
            LearnedOn: learnedOn,
            Topic: topic,
            Detail: detail,
            Confidence: confidence,
            SubjectPersonId: subjectPersonId,
            SourceMessageId: sourceMessageId,
            SourceRumorId: sourceRumorId,
            SourceEventId: sourceEventId);

        item.Validate();
        _knowledge.Add(item);
        return item;
    }

    public IReadOnlyList<CommunicationMessage> MessagesForRecipient(string personOrOrganizationId)
    {
        if (string.IsNullOrWhiteSpace(personOrOrganizationId))
        {
            throw new ArgumentException("Recipient id is required.", nameof(personOrOrganizationId));
        }

        return Ordered(_messages.Where(message => message.HasRecipient(personOrOrganizationId)));
    }

    public IReadOnlyList<CommunicationMessage> MessagesForOrganization(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        return Ordered(_messages.Where(message =>
            message.OrganizationId == organizationId
            || message.Source.OrganizationId == organizationId
            || message.Recipients.Any(recipient => recipient.OrganizationId == organizationId)));
    }

    public IReadOnlyList<CommunicationMessage> MessagesByVisibility(CommunicationVisibility visibility) =>
        Ordered(_messages.Where(message => message.Visibility == visibility));

    public IReadOnlyList<CommunicationMessage> MessagesByDateRange(DateOnly startsOn, DateOnly endsOn)
    {
        if (endsOn < startsOn)
        {
            throw new ArgumentOutOfRangeException(nameof(endsOn), "Date range end cannot be before start.");
        }

        return Ordered(_messages.Where(message => message.SentOn >= startsOn && message.SentOn <= endsOn));
    }

    public IReadOnlyList<OrganizationKnowledgeItem> KnowledgeForOrganization(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        return _knowledge
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.LearnedOn)
            .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
            .ToArray();
    }

    public AlphaInboxItem ToInboxItem(CommunicationMessage message)
    {
        message.Validate();

        return new AlphaInboxItem(
            InboxItemId: $"inbox:{message.MessageId}",
            Date: message.SentAt,
            EventType: message.SourceEventType ?? LegacyEventType.Generic,
            Severity: message.Severity,
            Title: message.Subject,
            Summary: message.Body,
            PrimaryPersonId: message.PrimaryRecipientPersonId ?? message.Source.PersonId);
    }

    public IReadOnlyList<AlphaInboxItem> BuildInboxItems() =>
        Ordered(_messages.Where(message => message.IsImportant))
            .Select(ToInboxItem)
            .ToArray();

    public static CommunicationVisibility MapVisibility(LegacyEventVisibility visibility) => visibility switch
    {
        LegacyEventVisibility.Public => CommunicationVisibility.Public,
        LegacyEventVisibility.League => CommunicationVisibility.League,
        LegacyEventVisibility.Organization => CommunicationVisibility.Organization,
        _ => CommunicationVisibility.Private
    };

    private static IReadOnlyList<CommunicationMessage> Ordered(IEnumerable<CommunicationMessage> messages) =>
        messages
            .OrderBy(message => message.SentOn)
            .ThenBy(message => message.MessageId, StringComparer.Ordinal)
            .ToArray();

    private static string CreateMessageId() => $"message-{Guid.NewGuid():N}";

    private static string CreateRumorId() => $"rumor-{Guid.NewGuid():N}";

    private static string CreateKnowledgeId() => $"knowledge-{Guid.NewGuid():N}";
}
