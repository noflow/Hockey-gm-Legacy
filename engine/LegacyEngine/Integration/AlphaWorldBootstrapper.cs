using LegacyEngine.Draft;
using LegacyEngine.Development;
using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Owners;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class AlphaWorldBootstrapper
{
    public AlphaWorldSnapshot Bootstrap(EngineRegistry registry, DateOnly startDate)
    {
        var organizationId = "alpha-org-001";
        var gm = CreatePerson("person-gm-001", "Alex", "Mercer", Gender.NonBinary, new DateOnly(1986, 3, 4), "Canada", "Regina, SK")
            .AddRole(new PersonRole("role-gm-001", PersonRoleType.GeneralManager, organizationId, startDate, null, "General Manager"));
        var scoutPerson = CreatePerson("person-scout-001", "Mara", "Keane", Gender.Female, new DateOnly(1979, 7, 19), "Canada", "Halifax, NS")
            .AddRole(new PersonRole("role-scout-001", PersonRoleType.Scout, organizationId, startDate, null, "Regional Scout"));
        var coachPerson = CreatePerson("person-coach-001", "Theo", "Larsen", Gender.Male, new DateOnly(1975, 11, 6), "Canada", "Red Deer, AB")
            .AddRole(new PersonRole("role-coach-001", PersonRoleType.Coach, organizationId, startDate, null, "Head Coach"));
        var players = CreatePlayers(startDate);
        var people = new[] { gm, scoutPerson, coachPerson }.Concat(players).ToArray();

        var owner = new Owner(
            OwnerId: "owner-001",
            Name: "Evelyn Hart",
            OrganizationId: organizationId,
            Archetype: OwnerArchetype.Builder,
            Budget: new OwnerBudget(850_000, 300_000, 125_000, 200_000, 175_000),
            Goals: new[]
            {
                new OwnerGoal(OwnerGoalType.DevelopProspects, 5, "Build a respected development program."),
                new OwnerGoal(OwnerGoalType.ImproveFinances, 3, "Keep the club stable while growing attendance.")
            },
            Trust: 62,
            Confidence: 58,
            Patience: 72,
            AutonomyLevel: OwnerAutonomyLevel.Normal);
        owner.Validate();

        var scout = new Scout(
            ScoutId: "scout-001",
            Name: scoutPerson.Identity.DisplayName,
            Specialties: new[] { ScoutSpecialty.Amateur, ScoutSpecialty.Character },
            Accuracy: 68,
            Diligence: 74,
            ReportBias: 0);
        scout.Validate();

        var rosterEngine = registry.RosterEngine;
        var roster = rosterEngine.CreateRoster("roster-alpha-001", organizationId);
        roster = rosterEngine.AddPlayer(roster, AddMove(players[0], RosterPosition.Goalie, startDate)).Roster;
        roster = rosterEngine.AddPlayer(roster, AddMove(players[1], RosterPosition.Defense, startDate)).Roster;
        roster = rosterEngine.AddPlayer(roster, AddMove(players[2], RosterPosition.Center, startDate)).Roster;

        var recruits = players.Skip(3).Take(3)
            .Select((player, index) => RecruitProfile.Create(player.PersonId, DefaultPriorities())
                .ChangeInterest(organizationId, 35 + (index * 10), startDate))
            .ToArray();

        var draftBoard = DraftBoard.Create("draft-board-alpha-001", organizationId);
        for (var index = 0; index < recruits.Length; index++)
        {
            draftBoard = draftBoard.AddProspect(new DraftBoardEntry(
                ProspectPersonId: recruits[index].RecruitPersonId,
                Rank: index + 1,
                ScoutingReportId: $"report-alpha-{index + 1:000}",
                ScoutingConfidence: ScoutingConfidenceLevel.Medium,
                ProjectionText: "Useful junior prospect with room to grow."));
        }

        var relationships = CreateRelationships(gm, scoutPerson, coachPerson, players, startDate);
        var developmentProfiles = CreateDevelopmentProfiles(registry, players, startDate);
        var injuries = new[]
        {
            registry.InjuryEngine.CreateInjury(
                players[1].PersonId,
                startDate,
                InjuryBodyPart.Shoulder,
                InjuryType.Sprain,
                InjurySeverity.Minor,
                expectedReturnDate: startDate.AddDays(7),
                recurrenceRisk: 10,
                longTermImpact: 4,
                injuryId: "injury-alpha-001").Injury
        };

        QueueBootstrapEvent(registry.EventEngine, startDate, organizationId, gm.PersonId);

        var snapshot = new AlphaWorldSnapshot(
            WorldState: registry.WorldEngine.State,
            OrganizationId: organizationId,
            Owner: owner,
            GeneralManager: gm,
            Scout: scout,
            ScoutPerson: scoutPerson,
            CoachPerson: coachPerson,
            People: people,
            Players: players,
            Recruits: recruits,
            Roster: roster,
            DraftBoard: draftBoard,
            Relationships: relationships,
            DevelopmentProfiles: developmentProfiles,
            Injuries: injuries);
        snapshot.Validate();
        return snapshot;
    }

    public static (EngineRegistry Registry, AlphaWorldSnapshot Snapshot) CreateAlphaWorld(DateOnly? startDate = null, Rulebook? rulebook = null)
    {
        var date = startDate ?? new DateOnly(2026, 9, 1);
        var worldEngine = WorldEngine.CreateWorld(
            "Alpha Hockey World",
            date,
            WorldPhase.Preseason,
            eventEngine: new EventEngine());
        var registry = EngineRegistry.Create(worldEngine, rulebook);
        var snapshot = new AlphaWorldBootstrapper().Bootstrap(registry, date);
        return (registry, snapshot);
    }

    private static Person[] CreatePlayers(DateOnly startDate) =>
    [
        CreatePlayer("person-player-001", "Noah", "Vale", new DateOnly(2008, 1, 10), "Moose Jaw, SK", startDate),
        CreatePlayer("person-player-002", "Eli", "Brooks", new DateOnly(2007, 5, 22), "Brandon, MB", startDate),
        CreatePlayer("person-player-003", "Mateo", "Singh", new DateOnly(2008, 9, 2), "Surrey, BC", startDate),
        CreatePlayer("person-player-004", "Owen", "Price", new DateOnly(2009, 4, 12), "Saskatoon, SK", startDate),
        CreatePlayer("person-player-005", "Caleb", "Stone", new DateOnly(2009, 8, 30), "Winnipeg, MB", startDate),
        CreatePlayer("person-player-006", "Jonas", "Meyer", new DateOnly(2008, 11, 16), "Zurich, Switzerland", startDate, "Switzerland")
    ];

    private static Person CreatePlayer(
        string personId,
        string firstName,
        string lastName,
        DateOnly birthDate,
        string birthplace,
        DateOnly startDate,
        string nationality = "Canada") =>
        CreatePerson(personId, firstName, lastName, Gender.Male, birthDate, nationality, birthplace)
            .AddRole(new PersonRole($"role-player-{personId}", PersonRoleType.Player, "alpha-org-001", startDate, null, "Player"));

    private static Person CreatePerson(
        string personId,
        string firstName,
        string lastName,
        Gender gender,
        DateOnly birthDate,
        string nationality,
        string birthplace)
    {
        var person = new Person(
            PersonId: personId,
            Identity: new PersonIdentity(firstName, lastName, gender, birthDate, nationality, birthplace),
            Status: PersonStatus.Active,
            Roles: Array.Empty<PersonRole>(),
            Reputation: new PersonReputation(40, 30, 10),
            Personality: new PersonalityProfile(60, 60, 55, 58, 65),
            CareerTimeline: Array.Empty<CareerTimelineEntry>());
        person.Validate();
        return person;
    }

    private static IReadOnlyList<Relationship> CreateRelationships(
        Person gm,
        Person scout,
        Person coach,
        IReadOnlyList<Person> players,
        DateOnly startDate)
    {
        var defaults = new RelationshipDefaults(
            Trust: 65,
            Respect: 60,
            Confidence: 58,
            Loyalty: 55,
            Influence: 50,
            Friendship: 12,
            Rivalry: 0);

        return new[]
        {
            Relationship.Create("relationship-owner-gm-alpha", "owner-001", gm.PersonId, RelationshipType.OwnerToGM, startDate, defaults),
            Relationship.Create("relationship-gm-scout-alpha", gm.PersonId, scout.PersonId, RelationshipType.GMToScout, startDate, defaults),
            Relationship.Create("relationship-coach-player-alpha", coach.PersonId, players[0].PersonId, RelationshipType.CoachToPlayer, startDate, defaults)
        };
    }

    private static IReadOnlyList<PlayerDevelopmentProfile> CreateDevelopmentProfiles(
        EngineRegistry registry,
        IReadOnlyList<Person> players,
        DateOnly startDate) =>
        players
            .Take(3)
            .Select((player, index) => registry.DevelopmentEngine.CreateProfile(
                personId: player.PersonId,
                currentAbility: 42 + (index * 3),
                potential: 68 + (index * 4),
                stage: DevelopmentStage.Junior,
                traits: CreateDevelopmentTraits(index),
                lastUpdated: startDate))
            .ToArray();

    private static IReadOnlyList<DevelopmentTrait> CreateDevelopmentTraits(int index) =>
        new[]
        {
            new DevelopmentTrait(DevelopmentAttribute.Skating, 50 + index),
            new DevelopmentTrait(DevelopmentAttribute.Shooting, 48 + index),
            new DevelopmentTrait(DevelopmentAttribute.Passing, 49 + index),
            new DevelopmentTrait(DevelopmentAttribute.Defense, 47 + index),
            new DevelopmentTrait(DevelopmentAttribute.Physicality, 46 + index),
            new DevelopmentTrait(DevelopmentAttribute.HockeyIQ, 52 + index),
            new DevelopmentTrait(DevelopmentAttribute.WorkEthic, 68 + index),
            new DevelopmentTrait(DevelopmentAttribute.Coachability, 66 + index),
            new DevelopmentTrait(DevelopmentAttribute.Confidence, 60 + index)
        };

    private static RosterMove AddMove(Person player, RosterPosition position, DateOnly date) =>
        new(
            MoveType: RosterMoveType.Add,
            PersonId: player.PersonId,
            Date: date,
            Position: position,
            TargetStatus: RosterStatus.Active,
            Age: player.CalculateAge(date),
            IsImport: player.Identity.Nationality != "Canada");

    private static IReadOnlyDictionary<RecruitPriority, int> DefaultPriorities() =>
        new Dictionary<RecruitPriority, int>
        {
            [RecruitPriority.IceTime] = 75,
            [RecruitPriority.Development] = 85,
            [RecruitPriority.Education] = 55,
            [RecruitPriority.Winning] = 60,
            [RecruitPriority.DistanceFromHome] = 45,
            [RecruitPriority.Facilities] = 65,
            [RecruitPriority.Coaching] = 80,
            [RecruitPriority.PathwayToHigherHockey] = 78,
            [RecruitPriority.FamilyComfort] = 60
        };

    private static void QueueBootstrapEvent(EventEngine eventEngine, DateOnly date, string organizationId, string gmPersonId)
    {
        var legacyEvent = eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, TimeSpan.Zero),
            LegacyEventType.OwnerGoalSet,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            "Owner goals set",
            "Ownership set the first alpha development goals.",
            new LegacyEventContext(PrimaryPersonId: gmPersonId, OrganizationId: organizationId),
            new Dictionary<string, object?> { ["alpha_bootstrap"] = true });

        eventEngine.QueueEvent(legacyEvent);
    }
}
