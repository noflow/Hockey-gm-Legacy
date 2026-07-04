using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Relationships;
using LegacyEngine.World;

internal sealed class WorldEngineTests
{
    public void WorldCreation()
    {
        var first = BuildWorld("First World");
        var second = BuildWorld("Second World");

        first.State.Validate();
        Assert.False(first.State.WorldId == second.State.WorldId, "World ids should be unique.");
        Assert.True(first.State.WorldId.Value.StartsWith("world-", StringComparison.Ordinal), "World id should use world prefix.");
        Assert.Equal("First World", first.State.WorldName);
        Assert.Equal(WorldPhase.Preseason, first.State.CurrentPhase);
        Assert.Equal(1, first.State.SystemRegistrations.Count);
    }

    public void CurrentDateStored()
    {
        var world = BuildWorld(startDate: new DateOnly(2026, 8, 15));

        Assert.Equal(new DateOnly(2026, 8, 15), world.State.CurrentDate.Value);
    }

    public void AdvanceOneDay()
    {
        var world = BuildWorld(startDate: new DateOnly(2026, 8, 15));
        var result = world.AdvanceOneDay();

        Assert.Equal(new DateOnly(2026, 8, 15), result.PreviousDate.Value);
        Assert.Equal(new DateOnly(2026, 8, 16), result.CurrentDate.Value);
        Assert.Equal(new DateOnly(2026, 8, 16), world.State.CurrentDate.Value);
    }

    public void AdvanceMultipleDays()
    {
        var world = BuildWorld(startDate: new DateOnly(2026, 8, 15));
        var results = world.AdvanceDays(5);

        Assert.Equal(5, results.Count);
        Assert.Equal(new DateOnly(2026, 8, 20), world.State.CurrentDate.Value);
        Assert.Equal(new DateOnly(2026, 8, 16), results[0].CurrentDate.Value);
        Assert.Equal(new DateOnly(2026, 8, 20), results[4].CurrentDate.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.AdvanceDays(-1));
    }

    public void SeasonYearUpdates()
    {
        var world = BuildWorld(
            startDate: new DateOnly(2026, 8, 31),
            settings: new WorldSettings(SeasonStartMonth: 9, SeasonStartDay: 1));

        Assert.Equal(2025, world.State.CurrentSeasonYear);

        var result = world.AdvanceOneDay();

        Assert.Equal(new DateOnly(2026, 9, 1), world.State.CurrentDate.Value);
        Assert.Equal(2026, world.State.CurrentSeasonYear);
        Assert.Equal(2026, result.CurrentSeasonYear);
    }

    public void PhaseCanBeSet()
    {
        var world = BuildWorld();

        world.SetPhase(WorldPhase.RegularSeason);
        Assert.Equal(WorldPhase.RegularSeason, world.State.CurrentPhase);

        world.SetPhase(WorldPhase.Playoffs);
        Assert.Equal(WorldPhase.Playoffs, world.State.CurrentPhase);
    }

    public void DailySimulationResultReturned()
    {
        var world = BuildWorld(startDate: new DateOnly(2026, 9, 1), phase: WorldPhase.RegularSeason);
        var result = world.AdvanceOneDay();

        Assert.Equal(world.State.WorldId, result.WorldId);
        Assert.Equal(new DateOnly(2026, 9, 1), result.PreviousDate.Value);
        Assert.Equal(new DateOnly(2026, 9, 2), result.CurrentDate.Value);
        Assert.Equal(2026, result.CurrentSeasonYear);
        Assert.Equal(WorldPhase.RegularSeason, result.CurrentPhase);
        Assert.Equal(0, result.ProcessedEventCount);
        Assert.Equal(0, result.ProcessedEvents.Count);
    }

    public void EventQueueProcessingIsCalled()
    {
        var eventEngine = new EventEngine();
        var world = BuildWorld(eventEngine: eventEngine);
        eventEngine.QueueEvent(eventEngine.CreateEvent(
            new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero),
            LegacyEventType.SeasonStarted,
            LegacyEventSeverity.Info,
            LegacyEventVisibility.Internal,
            "Season preparation started",
            "The world recorded a queued season event.",
            new LegacyEventContext(SeasonId: "season-2026"),
            new Dictionary<string, object?> { ["source"] = "world-test" },
            "event-world-001"));

        var result = world.AdvanceOneDay();

        Assert.Equal(1, result.ProcessedEventCount);
        Assert.Equal(0, eventEngine.Queue.Count);
        Assert.Equal(1, eventEngine.History.Count);
        Assert.Equal("event-world-001", result.ProcessedEvents[0].EventId);
        Assert.Equal(LegacyEventStatus.Archived, eventEngine.History.AllEvents[0].Status);
    }

    public void WorldEngineDoesNotMutateExternalDomainState()
    {
        var person = BuildPerson();
        var relationship = Relationship.Create(
            relationshipId: "relationship-a-b",
            fromPersonId: "person-a",
            toPersonId: "person-b",
            relationshipType: RelationshipType.GMToScout,
            createdOn: new DateOnly(2026, 1, 1));
        var world = BuildWorld();

        world.AdvanceDays(3);

        Assert.Equal(PersonStatus.Active, person.Status);
        Assert.Equal(0, person.CareerTimeline.Count);
        Assert.Equal(50, relationship.Trust);
        Assert.Equal(0, relationship.History.Count);
    }

    private static WorldEngine BuildWorld(
        string worldName = "Hockey GM Legacy Test World",
        DateOnly? startDate = null,
        WorldPhase phase = WorldPhase.Preseason,
        WorldSettings? settings = null,
        EventEngine? eventEngine = null) =>
        WorldEngine.CreateWorld(
            worldName,
            startDate ?? new DateOnly(2026, 8, 15),
            phase,
            settings,
            eventEngine);

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
