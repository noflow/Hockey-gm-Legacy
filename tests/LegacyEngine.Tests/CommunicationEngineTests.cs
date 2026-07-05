using LegacyEngine.Communication;
using LegacyEngine.Events;

internal sealed class CommunicationEngineTests
{
    public void MessageCreation()
    {
        var engine = new CommunicationEngine();
        var message = engine.SendMessage(
            CommunicationSource.FromPerson("gm-001", "GM Sarah Kane", "org-001"),
            new[] { CommunicationRecipient.Person("scout-001", "Chief Scout", "org-001") },
            CommunicationChannel.Email,
            CommunicationVisibility.Private,
            "Scouting assignment",
            "Please cover the weekend tournament in Kelowna.",
            new DateOnly(2026, 9, 1));

        Assert.True(message.MessageId.Length > 0, "Message should have an id.");
        Assert.Equal("Scouting assignment", message.Subject);
        Assert.Equal(1, message.Recipients.Count);
        Assert.Equal(CommunicationChannel.Email, message.Channel);
        Assert.Equal(1, engine.AllMessages.Count);
    }

    public void PrivateMessageDelivery()
    {
        var engine = new CommunicationEngine();
        engine.SendMessage(
            CommunicationSource.FromPerson("gm-001", "GM"),
            new[] { CommunicationRecipient.Person("coach-001", "Head Coach") },
            CommunicationChannel.DirectMessage,
            CommunicationVisibility.Private,
            "Line changes",
            "Move Petrov up to the first line tonight.",
            new DateOnly(2026, 9, 2));

        var delivered = engine.MessagesForRecipient("coach-001");
        Assert.Equal(1, delivered.Count);
        Assert.Equal(CommunicationVisibility.Private, delivered[0].Visibility);
        Assert.Equal(0, engine.MessagesForRecipient("scout-009").Count);
    }

    public void OrganizationWideMessage()
    {
        var engine = new CommunicationEngine();
        engine.SendMessage(
            CommunicationSource.FromPerson("gm-001", "GM", "org-001"),
            new[] { CommunicationRecipient.Organization("org-001", "Whole Organization") },
            CommunicationChannel.Announcement,
            CommunicationVisibility.Organization,
            "Playoff push",
            "Everyone reports tomorrow at 7am for the video session.",
            new DateOnly(2026, 9, 3),
            organizationId: "org-001");

        var orgMessages = engine.MessagesForOrganization("org-001");
        Assert.Equal(1, orgMessages.Count);
        Assert.Equal(CommunicationVisibility.Organization, orgMessages[0].Visibility);
        Assert.Equal(0, engine.MessagesForOrganization("org-002").Count);
    }

    public void PublicMessage()
    {
        var engine = new CommunicationEngine();
        engine.SendMessage(
            CommunicationSource.System("League Office"),
            new[]
            {
                CommunicationRecipient.Organization("org-001", "Club"),
                CommunicationRecipient.Organization("org-002", "Rival Club")
            },
            CommunicationChannel.PressRelease,
            CommunicationVisibility.Public,
            "Schedule released",
            "The full league schedule is now public.",
            new DateOnly(2026, 9, 4));

        var publicMessages = engine.MessagesByVisibility(CommunicationVisibility.Public);
        Assert.Equal(1, publicMessages.Count);
        Assert.Equal(2, publicMessages[0].Recipients.Count);
    }

    public void RumorCreation()
    {
        var engine = new CommunicationEngine();
        var rumor = engine.CreateRumor(
            "Trade interest",
            "A rival club is quietly shopping their captain.",
            reliability: 40,
            CommunicationVisibility.League,
            new DateOnly(2026, 9, 5),
            subjectPersonId: "player-050");

        Assert.Equal(1, engine.AllRumors.Count);
        Assert.Equal("player-050", rumor.SubjectPersonId);
        Assert.True(rumor.RumorId.Length > 0, "Rumor should have an id.");
    }

    public void RumorConfidenceChanges()
    {
        var engine = new CommunicationEngine();
        var rumor = engine.CreateRumor(
            "Coaching change",
            "The bench boss may be let go after the road trip.",
            reliability: 20,
            CommunicationVisibility.League,
            new DateOnly(2026, 9, 6));

        Assert.Equal(RumorConfidence.Baseless, rumor.Confidence);

        var corroborated = engine.CorroborateRumor(rumor, 60);
        Assert.Equal(80, corroborated.Reliability);
        Assert.Equal(RumorConfidence.Reliable, corroborated.Confidence);
        Assert.Equal(RumorConfidence.Reliable, engine.AllRumors[0].Confidence);
    }

    public void KnowledgeItemStorage()
    {
        var engine = new CommunicationEngine();
        engine.RecordKnowledge(
            "org-001",
            "Prospect health",
            "Top prospect recovered fully from a wrist injury.",
            RumorConfidence.Confirmed,
            new DateOnly(2026, 9, 7),
            subjectPersonId: "player-010");

        var knowledge = engine.KnowledgeForOrganization("org-001");
        Assert.Equal(1, knowledge.Count);
        Assert.True(knowledge[0].IsVerified, "Confirmed knowledge should be verified.");
        Assert.Equal("player-010", knowledge[0].SubjectPersonId);
        Assert.Equal(0, engine.KnowledgeForOrganization("org-002").Count);
    }

    public void EventToMessageConversion()
    {
        var eventEngine = new EventEngine();
        var engine = new CommunicationEngine(eventEngine);
        var legacyEvent = eventEngine.CreateEvent(
            new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero),
            LegacyEventType.PlayerInjured,
            LegacyEventSeverity.Warning,
            LegacyEventVisibility.Organization,
            "Player injured",
            "Star winger left the game with a lower-body injury.",
            new LegacyEventContext(PrimaryPersonId: "player-021", OrganizationId: "org-001"));

        var message = engine.CreateMessageFromEvent(
            legacyEvent,
            CommunicationSource.System("Medical Staff"),
            new[] { CommunicationRecipient.Person("gm-001", "General Manager", "org-001") });

        Assert.Equal("Player injured", message.Subject);
        Assert.Equal(CommunicationVisibility.Organization, message.Visibility);
        Assert.Equal(LegacyEventSeverity.Warning, message.Severity);
        Assert.Equal(legacyEvent.EventId, message.SourceEventId);
        Assert.Equal("org-001", message.OrganizationId);
        Assert.Equal(new DateOnly(2026, 9, 8), message.SentOn);
    }

    public void InboxItemConversion()
    {
        var engine = new CommunicationEngine();
        engine.SendMessage(
            CommunicationSource.FromPerson("gm-001", "GM"),
            new[] { CommunicationRecipient.Person("coach-001", "Head Coach") },
            CommunicationChannel.DirectMessage,
            CommunicationVisibility.Private,
            "Routine note",
            "Nothing urgent, just a reminder about the team dinner.",
            new DateOnly(2026, 9, 9),
            severity: LegacyEventSeverity.Info);

        var important = engine.SendMessage(
            CommunicationSource.System("Medical Staff"),
            new[] { CommunicationRecipient.Person("gm-001", "General Manager") },
            CommunicationChannel.Announcement,
            CommunicationVisibility.Organization,
            "Injury update",
            "The captain will miss six weeks.",
            new DateOnly(2026, 9, 10),
            severity: LegacyEventSeverity.Critical,
            sourceEventType: LegacyEventType.PlayerInjured);

        var inbox = engine.BuildInboxItems();
        Assert.Equal(1, inbox.Count);
        Assert.Equal(LegacyEventType.PlayerInjured, inbox[0].EventType);
        Assert.Equal("gm-001", inbox[0].PrimaryPersonId);

        var single = engine.ToInboxItem(important);
        Assert.Equal($"inbox:{important.MessageId}", single.InboxItemId);
        Assert.Equal(LegacyEventSeverity.Critical, single.Severity);
    }
}
