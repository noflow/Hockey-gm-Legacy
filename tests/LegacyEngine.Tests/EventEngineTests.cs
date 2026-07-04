using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Relationships;

internal sealed class EventEngineTests
{
    public void CreateEventsWithUniqueIds()
    {
        var engine = new EventEngine();
        var first = BuildEvent(engine, title: "First event");
        var second = BuildEvent(engine, title: "Second event");

        Assert.False(first.EventId == second.EventId, "Created events should have unique ids.");
        Assert.True(first.EventId.StartsWith("event-", StringComparison.Ordinal), "Event ids should use the event prefix.");
        Assert.Equal(LegacyEventStatus.Created, first.Status);
    }

    public void EventStoresRequiredFields()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 1, 10, 30, 0, TimeSpan.Zero);
        var context = new LegacyEventContext(
            PrimaryPersonId: "person-a",
            SecondaryPersonId: "person-b",
            OrganizationId: "org-1",
            LeagueId: "league-1",
            SeasonId: "season-2026",
            GameId: "game-7",
            RelationshipId: "relationship-a-b",
            RulebookId: "junior_v1");
        var metadata = new Dictionary<string, object?> { ["reason"] = "Trust changed.", ["amount"] = 5 };
        var legacyEvent = new EventEngine().CreateEvent(
            occurredAt,
            LegacyEventType.RelationshipChanged,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Staff,
            "Relationship changed",
            "Trust increased after a meeting.",
            context,
            metadata,
            eventId: "event-known");

        legacyEvent.Validate();
        Assert.Equal("event-known", legacyEvent.EventId);
        Assert.Equal(occurredAt, legacyEvent.OccurredAt);
        Assert.Equal(LegacyEventType.RelationshipChanged, legacyEvent.EventType);
        Assert.Equal(LegacyEventSeverity.Notice, legacyEvent.Severity);
        Assert.Equal(LegacyEventVisibility.Staff, legacyEvent.Visibility);
        Assert.Equal(LegacyEventStatus.Created, legacyEvent.Status);
        Assert.Equal("person-a", legacyEvent.Context.PrimaryPersonId);
        Assert.Equal("person-b", legacyEvent.Context.SecondaryPersonId);
        Assert.Equal("org-1", legacyEvent.Context.OrganizationId);
        Assert.Equal("league-1", legacyEvent.Context.LeagueId);
        Assert.Equal("season-2026", legacyEvent.Context.SeasonId);
        Assert.Equal("game-7", legacyEvent.Context.GameId);
        Assert.Equal("relationship-a-b", legacyEvent.Context.RelationshipId);
        Assert.Equal("junior_v1", legacyEvent.Context.RulebookId);
        Assert.Equal(5, legacyEvent.Metadata["amount"]);
    }

    public void QueueEvents()
    {
        var engine = new EventEngine();
        var legacyEvent = BuildEvent(engine, eventId: "event-queue");
        var queued = engine.QueueEvent(legacyEvent);

        Assert.Equal(LegacyEventStatus.Queued, queued.Status);
        Assert.Equal(1, engine.Queue.Count);
        Assert.Equal("event-queue", engine.Queue.PendingEvents[0].EventId);
        Assert.Throws<ArgumentException>(() => engine.QueueEvent(legacyEvent));
    }

    public void ProcessQueuedEventsInDateOrder()
    {
        var engine = new EventEngine();
        engine.QueueEvent(BuildEvent(engine, eventId: "event-late", occurredAt: new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));
        engine.QueueEvent(BuildEvent(engine, eventId: "event-early", occurredAt: new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        engine.QueueEvent(BuildEvent(engine, eventId: "event-middle", occurredAt: new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)));

        var results = engine.ProcessQueuedEvents(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, results.Count);
        Assert.Equal("event-early", results[0].EventId);
        Assert.Equal("event-middle", results[1].EventId);
        Assert.Equal("event-late", results[2].EventId);
        Assert.Equal("event-early", engine.History.AllEvents[0].EventId);
        Assert.Equal("event-middle", engine.History.AllEvents[1].EventId);
        Assert.Equal("event-late", engine.History.AllEvents[2].EventId);
    }

    public void ProcessAndArchiveEvents()
    {
        var engine = new EventEngine();
        engine.QueueEvent(BuildEvent(engine, eventId: "event-process"));

        var results = engine.ProcessQueuedEvents(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, engine.Queue.Count);
        Assert.Equal(1, engine.History.Count);
        Assert.Equal(1, results.Count);
        Assert.True(results[0].Success, "Processed event result should indicate success.");
        Assert.Equal(LegacyEventStatus.Processed, results[0].Status);
        Assert.Equal(LegacyEventStatus.Archived, engine.History.AllEvents[0].Status);
        Assert.Throws<InvalidOperationException>(() => engine.History.Archive(BuildEvent(engine, eventId: "event-not-processed")));
    }

    public void QueryEventHistory()
    {
        var engine = new EventEngine();
        engine.QueueEvent(BuildEvent(
            engine,
            eventId: "event-person-a",
            eventType: LegacyEventType.PersonCreated,
            occurredAt: new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            context: new LegacyEventContext(PrimaryPersonId: "person-a", OrganizationId: "org-1")));
        engine.QueueEvent(BuildEvent(
            engine,
            eventId: "event-person-b",
            eventType: LegacyEventType.RoleStarted,
            occurredAt: new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero),
            context: new LegacyEventContext(PrimaryPersonId: "person-b", SecondaryPersonId: "person-a", OrganizationId: "org-2")));
        engine.QueueEvent(BuildEvent(
            engine,
            eventId: "event-org-1",
            eventType: LegacyEventType.ScoutAssigned,
            occurredAt: new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero),
            context: new LegacyEventContext(OrganizationId: "org-1")));
        engine.ProcessQueuedEvents(new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, engine.History.QueryByPersonId("person-a").Count);
        Assert.Equal(2, engine.History.QueryByOrganizationId("org-1").Count);
        Assert.Equal(1, engine.History.QueryByEventType(LegacyEventType.ScoutAssigned).Count);
        Assert.Equal(2, engine.History.QueryByDateRange(
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero)).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.History.QueryByDateRange(
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    public void EventEngineDoesNotMutateExternalDomainState()
    {
        var person = BuildPerson();
        var relationship = Relationship.Create(
            relationshipId: "relationship-a-b",
            fromPersonId: "person-a",
            toPersonId: "person-b",
            relationshipType: RelationshipType.GMToScout,
            createdOn: new DateOnly(2026, 1, 1));
        var engine = new EventEngine();

        engine.QueueEvent(BuildEvent(
            engine,
            eventId: "event-external",
            eventType: LegacyEventType.RelationshipChanged,
            context: new LegacyEventContext(
                PrimaryPersonId: person.PersonId,
                RelationshipId: relationship.RelationshipId)));
        engine.ProcessQueuedEvents(new DateTimeOffset(2026, 1, 2, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(PersonStatus.Active, person.Status);
        Assert.Equal(0, person.CareerTimeline.Count);
        Assert.Equal(50, relationship.Trust);
        Assert.Equal(0, relationship.History.Count);
    }

    private static LegacyEvent BuildEvent(
        EventEngine engine,
        string? eventId = null,
        string title = "Generic event",
        LegacyEventType eventType = LegacyEventType.Generic,
        DateTimeOffset? occurredAt = null,
        LegacyEventContext? context = null) =>
        engine.CreateEvent(
            occurredAt ?? new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Info,
            LegacyEventVisibility.Internal,
            title,
            "A generic legacy event was recorded.",
            context ?? new LegacyEventContext(PrimaryPersonId: "person-a", OrganizationId: "org-1"),
            new Dictionary<string, object?> { ["source"] = "test" },
            eventId);

    private static Person BuildPerson() =>
        new(
            PersonId: "person-a",
            Identity: new PersonIdentity(
                FirstName: "Mika",
                LastName: "Tanaka",
                Gender: Gender.Female,
                BirthDate: new DateOnly(2008, 1, 15),
                Nationality: "Canada",
                Birthplace: "Vancouver, British Columbia"),
            Status: PersonStatus.Active,
            Roles: Array.Empty<PersonRole>(),
            Reputation: new PersonReputation(Local: 50, League: 40, National: 20),
            Personality: new PersonalityProfile(
                Ambition: 72,
                Loyalty: 65,
                Temperament: 58,
                Adaptability: 80,
                Professionalism: 70),
            CareerTimeline: Array.Empty<CareerTimelineEntry>());
}
