using LegacyEngine.Contracts;
using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Organizations;
using LegacyEngine.Owners;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.Seasons;
using LegacyEngine.Staff;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class NewGmScenarioBootstrapper
{
    public NewGmScenarioResult Bootstrap(NewGmScenarioSettings? settings = null, Rulebook? rulebook = null)
    {
        var scenarioSettings = settings ?? new NewGmScenarioSettings();
        scenarioSettings.Validate();
        var activeRulebook = rulebook ?? RulebookPresets.CreateJuniorMajor();

        var seasonCalendar = SeasonCalendar.Build(scenarioSettings.SeasonYear, scenarioSettings.SeasonSettings);
        var draftDate = seasonCalendar.Milestones.Single(item => item.Type == SeasonMilestoneType.Draft).Date.Value;
        var startDate = draftDate.AddDays(-14);

        var worldEngine = WorldEngine.CreateWorld(
            scenarioSettings.WorldName,
            startDate,
            WorldPhase.Offseason,
            eventEngine: new EventEngine());
        var registry = EngineRegistry.Create(worldEngine, activeRulebook);
        var season = registry.SeasonEngine.CreateSeason(
            scenarioSettings.SeasonId,
            scenarioSettings.LeagueId,
            scenarioSettings.SeasonYear,
            scenarioSettings.SeasonSettings,
            activeRulebook,
            startDate);
        registry.WorldEngine.SetPhase(WorldPhase.Offseason);

        var gmProfile = new GmProfileFactory().Create(
            scenarioSettings.GmCreationSettings ?? GmProfileCreationSettings.JordanHayes(scenarioSettings.PlayerGmPersonId),
            startDate);
        var gm = gmProfile.Person
            .AddRole(new PersonRole("role-new-gm", PersonRoleType.GeneralManager, scenarioSettings.OrganizationId, startDate, null, "General Manager"));
        var ownerPerson = CreatePerson("person-owner-evelyn-hart", "Evelyn", "Hart", Gender.Female, new DateOnly(1965, 6, 4), "Canada", "Regina, SK", 70, 62, 28)
            .AddRole(new PersonRole("role-owner", PersonRoleType.Owner, scenarioSettings.OrganizationId, new DateOnly(2018, 5, 10), null, "Owner"));
        var headCoach = CreatePerson("person-coach-head", "Theo", "Larsen", Gender.Male, new DateOnly(1975, 11, 6), "Canada", "Red Deer, AB", 58, 52, 18)
            .AddRole(new PersonRole("role-head-coach", PersonRoleType.Coach, scenarioSettings.OrganizationId, new DateOnly(2024, 6, 1), null, "Head Coach"));
        var assistantCoach = CreatePerson("person-coach-assistant", "Priya", "Nandakumar", Gender.Female, new DateOnly(1984, 1, 27), "Canada", "Mississauga, ON", 50, 44, 12)
            .AddRole(new PersonRole("role-assistant-coach", PersonRoleType.Coach, scenarioSettings.OrganizationId, new DateOnly(2025, 7, 1), null, "Assistant Coach"));
        var headScoutPerson = CreatePerson("person-scout-head", "Mara", "Keane", Gender.Female, new DateOnly(1979, 7, 19), "Canada", "Halifax, NS", 55, 50, 14)
            .AddRole(new PersonRole("role-head-scout", PersonRoleType.Scout, scenarioSettings.OrganizationId, new DateOnly(2023, 8, 1), null, "Head Scout"));
        var regionalScoutPerson = CreatePerson("person-scout-west", "Cal", "Morrison", Gender.Male, new DateOnly(1982, 9, 8), "Canada", "Kelowna, BC", 44, 39, 8)
            .AddRole(new PersonRole("role-regional-scout", PersonRoleType.Scout, scenarioSettings.OrganizationId, new DateOnly(2025, 8, 1), null, "Regional Scout"));

        var rosterPlayers = CreateRosterPlayers(startDate, scenarioSettings.OrganizationId);
        var recruits = CreateRecruitPeople(startDate);
        var people = new[] { gm, ownerPerson, headCoach, assistantCoach, headScoutPerson, regionalScoutPerson }
            .Concat(rosterPlayers)
            .Concat(recruits)
            .ToArray();

        var owner = new Owner(
            OwnerId: "owner-prairie-falcons",
            Name: ownerPerson.Identity.DisplayName,
            OrganizationId: scenarioSettings.OrganizationId,
            Archetype: OwnerArchetype.Builder,
            Budget: new OwnerBudget(1_150_000, 410_000, 180_000, 245_000, 210_000),
            Goals: new[]
            {
                new OwnerGoal(OwnerGoalType.DevelopProspects, 5, "Become a club known for graduating dependable players."),
                new OwnerGoal(OwnerGoalType.BuildCommunityTrust, 4, "Stabilize the staff room and make expectations clear."),
                new OwnerGoal(OwnerGoalType.ImproveFinances, 3, "Protect the budget while improving fan confidence.")
            },
            Trust: 58,
            Confidence: 54,
            Patience: 68,
            AutonomyLevel: OwnerAutonomyLevel.Normal);
        owner.Validate();

        var organization = CreateOrganization(registry, scenarioSettings, ownerPerson.PersonId, startDate);
        var contracts = CreateContracts(registry, scenarioSettings.OrganizationId, startDate, gm, headCoach, assistantCoach, headScoutPerson, regionalScoutPerson);
        var staffMembers = CreateStaff(registry, scenarioSettings.OrganizationId, startDate, headCoach, assistantCoach, headScoutPerson, regionalScoutPerson, contracts);
        var roster = CreateRoster(registry, scenarioSettings, rosterPlayers, startDate);
        var recruitProfiles = CreateRecruitProfiles(scenarioSettings.OrganizationId, recruits, startDate);
        var draftBoard = CreateDraftBoard(scenarioSettings, recruitProfiles);
        var scout = new Scout(
            ScoutId: "scout-head-prairie-falcons",
            Name: headScoutPerson.Identity.DisplayName,
            Specialties: new[] { ScoutSpecialty.Amateur, ScoutSpecialty.Character, ScoutSpecialty.Goalie },
            Accuracy: 72,
            Diligence: 78,
            ReportBias: -2);
        scout.Validate();

        var relationships = CreateRelationships(owner, gm, headCoach, assistantCoach, headScoutPerson, regionalScoutPerson, rosterPlayers, startDate);
        var developmentProfiles = CreateDevelopmentProfiles(registry, rosterPlayers, startDate);
        var injuries = new[]
        {
            registry.InjuryEngine.CreateInjury(
                rosterPlayers[5].PersonId,
                startDate.AddDays(-4),
                InjuryBodyPart.Knee,
                InjuryType.Strain,
                InjurySeverity.Moderate,
                expectedReturnDate: startDate.AddDays(21),
                recurrenceRisk: 18,
                longTermImpact: 8,
                injuryId: "injury-new-gm-001").Injury
        };

        QueueScenarioEvent(registry.EventEngine, startDate, scenarioSettings.OrganizationId, gm.PersonId, draftDate);

        var alphaSnapshot = new AlphaWorldSnapshot(
            WorldState: registry.WorldEngine.State,
            OrganizationId: scenarioSettings.OrganizationId,
            Owner: owner,
            GeneralManager: gm,
            Scout: scout,
            ScoutPerson: headScoutPerson,
            CoachPerson: headCoach,
            People: people,
            Players: rosterPlayers,
            Recruits: recruitProfiles,
            Roster: roster,
            DraftBoard: draftBoard,
            Relationships: relationships,
            DevelopmentProfiles: developmentProfiles,
            Injuries: injuries)
        {
            Organization = organization,
            Season = season,
            StaffMembers = staffMembers,
            Contracts = contracts
        };
        alphaSnapshot.Validate();

        var firstDayInbox = new NewGmFirstDayInboxFactory().Create(
            startDate,
            draftDate,
            owner,
            gm,
            headCoach,
            scout,
            roster,
            injuries,
            season,
            gmProfile.PreferredName);

        var scenarioSnapshot = new NewGmScenarioSnapshot(
            AlphaSnapshot: alphaSnapshot,
            Organization: organization,
            Season: season,
            StaffMembers: staffMembers,
            Contracts: contracts,
            GeneralManagerProfile: gmProfile with { Person = gm },
            ScoutingAssignments: Array.Empty<ScoutingAssignment>(),
            DraftDate: draftDate,
            FirstDayInbox: firstDayInbox,
            ScenarioSummary: $"{gmProfile.PreferredName} takes over as GM of {organization.Name} two weeks before the junior draft.");
        scenarioSnapshot.Validate();

        return new NewGmScenarioResult(
            Registry: registry,
            ScenarioSnapshot: scenarioSnapshot,
            AlphaSnapshot: alphaSnapshot,
            FirstDayInbox: firstDayInbox,
            Summary: scenarioSnapshot.ScenarioSummary);
    }

    public static NewGmScenarioResult CreateScenario(NewGmScenarioSettings? settings = null, Rulebook? rulebook = null) =>
        new NewGmScenarioBootstrapper().Bootstrap(settings, rulebook);

    private static Organization CreateOrganization(
        EngineRegistry registry,
        NewGmScenarioSettings settings,
        string ownerPersonId,
        DateOnly startDate) =>
        registry.OrganizationEngine.CreateOrganization(
            settings.OrganizationId,
            OrganizationType.Team,
            new OrganizationIdentity("Prairie Falcons", "Moose Jaw", "SK", "Canada"),
            new DateOnly(1989, 9, 1),
            ownerPersonId,
            settings.LeagueId,
            settings.RosterId,
            new OrganizationCulture(
                DevelopmentFocus: 78,
                WinningPressure: 62,
                FinancialDiscipline: 70,
                CommunityFocus: 84,
                Innovation: 48,
                Loyalty: 73),
            new OrganizationReputation(Local: 66, League: 57, National: 34),
            new[]
            {
                new OrganizationMembership(settings.PlayerGmPersonId, JoinedOn: startDate),
                new OrganizationMembership("person-coach-head", StaffRole.HeadCoach, "dept-coaching", new DateOnly(2024, 6, 1)),
                new OrganizationMembership("person-coach-assistant", StaffRole.AssistantCoach, "dept-coaching", new DateOnly(2025, 7, 1)),
                new OrganizationMembership("person-scout-head", StaffRole.HeadScout, "dept-scouting", new DateOnly(2023, 8, 1)),
                new OrganizationMembership("person-scout-west", StaffRole.Scout, "dept-scouting", new DateOnly(2025, 8, 1))
            },
            new[]
            {
                new OrganizationDepartment("dept-hockey-ops", "Hockey Operations", StaffDepartment.Management),
                new OrganizationDepartment("dept-coaching", "Coaching", StaffDepartment.Coaching),
                new OrganizationDepartment("dept-scouting", "Scouting", StaffDepartment.Scouting)
            });

    private static IReadOnlyList<Contract> CreateContracts(
        EngineRegistry registry,
        string organizationId,
        DateOnly startDate,
        params Person[] people)
    {
        var contracts = new List<Contract>();
        for (var index = 0; index < people.Length; index++)
        {
            var person = people[index];
            var type = index switch
            {
                0 => ContractType.GMContract,
                1 or 2 => ContractType.CoachContract,
                _ => ContractType.ScoutContract
            };
            var offered = registry.ContractEngine.CreateOffer(new ContractOffer(
                OfferId: $"new-gm-contract-offer-{index + 1:00}",
                PersonId: person.PersonId,
                OrganizationId: organizationId,
                ContractType: type,
                Term: new ContractTerm(startDate.AddDays(-30), startDate.AddYears(1).AddDays(-1)),
                Money: new ContractMoney(42_000 + (index * 5_000), Currency: "CAD"),
                Clauses: Array.Empty<ContractClause>(),
                OfferedOn: startDate.AddDays(-30),
                Notes: "Existing organization contract reference for the Alpha 1.0 scenario."));
            contracts.Add(registry.ContractEngine.SignContract(offered, startDate.AddDays(-29)).Contract);
        }

        return contracts;
    }

    private static IReadOnlyList<StaffMember> CreateStaff(
        EngineRegistry registry,
        string organizationId,
        DateOnly startDate,
        Person headCoach,
        Person assistantCoach,
        Person headScout,
        Person regionalScout,
        IReadOnlyList<Contract> contracts)
    {
        var staff = new[]
        {
            BuildStaff(registry, headCoach.PersonId, organizationId, StaffRole.HeadCoach, 15, 68, CoachingAttributes(76, 70, 73), contracts[1], startDate),
            BuildStaff(registry, assistantCoach.PersonId, organizationId, StaffRole.AssistantCoach, 8, 57, CoachingAttributes(70, 62, 76), contracts[2], startDate),
            BuildStaff(registry, headScout.PersonId, organizationId, StaffRole.HeadScout, 17, 64, ScoutingAttributes(75, 78, 73), contracts[3], startDate),
            BuildStaff(registry, regionalScout.PersonId, organizationId, StaffRole.Scout, 10, 52, ScoutingAttributes(66, 70, 80), contracts[4], startDate)
        };

        return staff;
    }

    private static StaffMember BuildStaff(
        EngineRegistry registry,
        string personId,
        string organizationId,
        StaffRole role,
        int experience,
        int reputation,
        StaffAttributes attributes,
        Contract contract,
        DateOnly startDate)
    {
        var member = registry.StaffEngine.CreateStaffMember(
            personId,
            organizationId,
            role,
            yearsExperience: experience,
            reputation: reputation,
            attributes: attributes,
            contractId: contract.ContractId);
        member = registry.StaffEngine.Hire(member, startDate.AddDays(-30), new StaffContractReference(contract.ContractId, organizationId, contract.Term.StartDate, contract.Term.EndDate));
        return registry.StaffEngine.AssignRole(member, role, startDate.AddDays(-30), $"assignment-{personId}");
    }

    private static StaffAttributes CoachingAttributes(int teaching, int tactics, int development) =>
        StaffAttributes.ForCoaching(new Dictionary<StaffCoachingAttribute, int>
        {
            [StaffCoachingAttribute.Teaching] = teaching,
            [StaffCoachingAttribute.Tactics] = tactics,
            [StaffCoachingAttribute.Development] = development,
            [StaffCoachingAttribute.Communication] = 68,
            [StaffCoachingAttribute.Leadership] = 65
        });

    private static StaffAttributes ScoutingAttributes(int talent, int character, int regional) =>
        StaffAttributes.ForScouting(new Dictionary<StaffScoutingAttribute, int>
        {
            [StaffScoutingAttribute.TalentEvaluation] = talent,
            [StaffScoutingAttribute.CharacterEvaluation] = character,
            [StaffScoutingAttribute.RegionalKnowledge] = regional,
            [StaffScoutingAttribute.NorthAmericanKnowledge] = 72
        });

    private static Roster CreateRoster(
        EngineRegistry registry,
        NewGmScenarioSettings settings,
        IReadOnlyList<Person> players,
        DateOnly startDate)
    {
        var roster = registry.RosterEngine.CreateRoster(settings.RosterId, settings.OrganizationId);
        for (var index = 0; index < players.Count; index++)
        {
            roster = registry.RosterEngine.AddPlayer(roster, new RosterMove(
                MoveType: RosterMoveType.Add,
                PersonId: players[index].PersonId,
                Date: startDate.AddDays(-20),
                Position: PositionFor(index),
                TargetStatus: RosterStatus.Active,
                Age: players[index].CalculateAge(startDate),
                IsImport: players[index].Identity.Nationality != "Canada")).Roster;
        }

        return roster;
    }

    private static DraftBoard CreateDraftBoard(
        NewGmScenarioSettings settings,
        IReadOnlyList<RecruitProfile> recruits)
    {
        var board = DraftBoard.Create(settings.DraftBoardId, settings.OrganizationId);
        for (var index = 0; index < recruits.Count; index++)
        {
            board = board.AddProspect(new DraftBoardEntry(
                ProspectPersonId: recruits[index].RecruitPersonId,
                Rank: index + 1,
                ScoutingReportId: $"scenario-report-{index + 1:000}",
                ScoutingConfidence: index < 3 ? ScoutingConfidenceLevel.High : ScoutingConfidenceLevel.Medium,
                ProjectionText: ProjectionFor(index),
                IsStarred: index < 2,
                PersonalNotes: index < 3 ? "GM note: review again before draft day." : "",
                AnalyticsSummary: AnalyticsFor(index)));
        }

        return board;
    }

    private static IReadOnlyList<RecruitProfile> CreateRecruitProfiles(
        string organizationId,
        IReadOnlyList<Person> recruits,
        DateOnly startDate) =>
        recruits
            .Select((person, index) => RecruitProfile.Create(person.PersonId, PrioritiesFor(index))
                .ChangeInterest(organizationId, 40 + (index * 4), startDate.AddDays(-7)))
            .ToArray();

    private static IReadOnlyList<Relationship> CreateRelationships(
        Owner owner,
        Person gm,
        Person headCoach,
        Person assistantCoach,
        Person headScout,
        Person regionalScout,
        IReadOnlyList<Person> players,
        DateOnly startDate)
    {
        var steady = new RelationshipDefaults(62, 60, 57, 54, 50, 8, 0);
        var cautious = new RelationshipDefaults(52, 58, 48, 46, 45, 4, 0);

        return new[]
        {
            Relationship.Create("rel-owner-gm-new-gm", owner.OwnerId, gm.PersonId, RelationshipType.OwnerToGM, startDate, steady),
            Relationship.Create("rel-gm-owner-new-gm", gm.PersonId, owner.OwnerId, RelationshipType.GMToOwner, startDate, cautious),
            Relationship.Create("rel-gm-head-coach-new-gm", gm.PersonId, headCoach.PersonId, RelationshipType.Professional, startDate, cautious),
            Relationship.Create("rel-head-coach-gm-new-gm", headCoach.PersonId, gm.PersonId, RelationshipType.Professional, startDate, steady),
            Relationship.Create("rel-gm-head-scout-new-gm", gm.PersonId, headScout.PersonId, RelationshipType.GMToScout, startDate, steady),
            Relationship.Create("rel-head-scout-gm-new-gm", headScout.PersonId, gm.PersonId, RelationshipType.ScoutToGM, startDate, steady),
            Relationship.Create("rel-gm-assistant-coach-new-gm", gm.PersonId, assistantCoach.PersonId, RelationshipType.Professional, startDate, cautious),
            Relationship.Create("rel-gm-regional-scout-new-gm", gm.PersonId, regionalScout.PersonId, RelationshipType.GMToScout, startDate, cautious),
            Relationship.Create("rel-coach-captain-new-gm", headCoach.PersonId, players[0].PersonId, RelationshipType.CoachToPlayer, startDate, steady)
        };
    }

    private static IReadOnlyList<PlayerDevelopmentProfile> CreateDevelopmentProfiles(
        EngineRegistry registry,
        IReadOnlyList<Person> players,
        DateOnly startDate) =>
        players
            .Select((player, index) => registry.DevelopmentEngine.CreateProfile(
                player.PersonId,
                currentAbility: 38 + (index % 8),
                potential: 58 + (index % 12),
                stage: DevelopmentStage.Junior,
                traits: DevelopmentTraits(index),
                lastUpdated: startDate.AddDays(-15)))
            .ToArray();

    private static IReadOnlyList<DevelopmentTrait> DevelopmentTraits(int index) =>
        new[]
        {
            new DevelopmentTrait(DevelopmentAttribute.Skating, 52 + (index % 8)),
            new DevelopmentTrait(DevelopmentAttribute.Shooting, 48 + (index % 10)),
            new DevelopmentTrait(DevelopmentAttribute.Passing, 50 + (index % 9)),
            new DevelopmentTrait(DevelopmentAttribute.Defense, 47 + (index % 11)),
            new DevelopmentTrait(DevelopmentAttribute.Physicality, 45 + (index % 12)),
            new DevelopmentTrait(DevelopmentAttribute.HockeyIQ, 51 + (index % 10)),
            new DevelopmentTrait(DevelopmentAttribute.WorkEthic, 61 + (index % 14)),
            new DevelopmentTrait(DevelopmentAttribute.Coachability, 58 + (index % 13)),
            new DevelopmentTrait(DevelopmentAttribute.Confidence, 54 + (index % 12))
        };

    private static IReadOnlyList<Person> CreateRosterPlayers(DateOnly startDate, string organizationId)
    {
        var names = new[]
        {
            ("Noah", "Vale", "Moose Jaw, SK", "Canada"),
            ("Eli", "Brooks", "Brandon, MB", "Canada"),
            ("Mateo", "Singh", "Surrey, BC", "Canada"),
            ("Owen", "Price", "Saskatoon, SK", "Canada"),
            ("Caleb", "Stone", "Winnipeg, MB", "Canada"),
            ("Jonas", "Meyer", "Zurich", "Switzerland"),
            ("Finn", "Lacroix", "Quebec City, QC", "Canada"),
            ("Rylan", "Kerr", "Medicine Hat, AB", "Canada"),
            ("Sam", "Okafor", "Calgary, AB", "Canada"),
            ("Luca", "Bianchi", "Burnaby, BC", "Canada"),
            ("Wyatt", "Reed", "Yorkton, SK", "Canada"),
            ("Emil", "Soderberg", "Stockholm", "Sweden"),
            ("Carter", "Moon", "Lethbridge, AB", "Canada"),
            ("Asher", "Bell", "Prince Albert, SK", "Canada"),
            ("Dante", "Rossi", "Vancouver, BC", "Canada"),
            ("Mason", "Li", "Richmond, BC", "Canada"),
            ("Kieran", "Fox", "Regina, SK", "Canada"),
            ("Isaac", "Gould", "Portage la Prairie, MB", "Canada"),
            ("Tomas", "Novak", "Brno", "Czechia"),
            ("Ben", "Hartley", "Estevan, SK", "Canada"),
            ("Nico", "Fraser", "Kamloops, BC", "Canada"),
            ("Arjun", "Rao", "Edmonton, AB", "Canada")
        };

        return names
            .Select((name, index) => CreatePlayer(
                $"person-roster-{index + 1:000}",
                name.Item1,
                name.Item2,
                new DateOnly(2007 + (index % 3), (index % 12) + 1, Math.Min(24, (index % 27) + 1)),
                name.Item4,
                name.Item3,
                organizationId,
                startDate))
            .ToArray();
    }

    private static IReadOnlyList<Person> CreateRecruitPeople(DateOnly startDate)
    {
        var seeds = new[]
        {
            ("Tate", "Marlow", "Canada", "Dauphin, MB"),
            ("Julian", "Chen", "Canada", "Victoria, BC"),
            ("Pavel", "Kral", "Czechia", "Prague"),
            ("Cole", "Bishop", "Canada", "Swift Current, SK"),
            ("Henrik", "Aasen", "Norway", "Oslo"),
            ("Miles", "Tanner", "Canada", "Weyburn, SK"),
            ("Leo", "Park", "Canada", "Calgary, AB"),
            ("Nolan", "Savoie", "Canada", "St. Albert, AB")
        };

        return Enumerable.Range(0, 60)
            .Select(index =>
            {
                var seed = seeds[index % seeds.Length];
                return CreatePlayer(
                $"person-recruit-{index + 1:000}",
                seed.Item1,
                seed.Item2,
                new DateOnly(2009, (index % 12) + 1, Math.Min(24, (index % 25) + 1)),
                seed.Item3,
                seed.Item4,
                "unassigned",
                startDate);
            })
            .ToArray();
    }

    private static Person CreatePlayer(
        string personId,
        string firstName,
        string lastName,
        DateOnly birthDate,
        string nationality,
        string birthplace,
        string organizationId,
        DateOnly startDate) =>
        CreatePerson(personId, firstName, lastName, Gender.Male, birthDate, nationality, birthplace, 36, 28, 5)
            .AddRole(new PersonRole($"role-player-{personId}", PersonRoleType.Player, organizationId, startDate, null, "Player"));

    private static Person CreatePerson(
        string personId,
        string firstName,
        string lastName,
        Gender gender,
        DateOnly birthDate,
        string nationality,
        string birthplace,
        int localReputation,
        int leagueReputation,
        int nationalReputation)
    {
        var person = new Person(
            PersonId: personId,
            Identity: new PersonIdentity(firstName, lastName, gender, birthDate, nationality, birthplace),
            Status: PersonStatus.Active,
            Roles: Array.Empty<PersonRole>(),
            Reputation: new PersonReputation(localReputation, leagueReputation, nationalReputation),
            Personality: new PersonalityProfile(62, 64, 57, 59, 68),
            CareerTimeline: Array.Empty<CareerTimelineEntry>());
        person.Validate();
        return person;
    }

    private static RosterPosition PositionFor(int index) =>
        index switch
        {
            0 or 1 => RosterPosition.Goalie,
            >= 2 and <= 8 => RosterPosition.Defense,
            _ when index % 3 == 0 => RosterPosition.Center,
            _ when index % 3 == 1 => RosterPosition.LeftWing,
            _ => RosterPosition.RightWing
        };

    private static IReadOnlyDictionary<RecruitPriority, int> PrioritiesFor(int index) =>
        new Dictionary<RecruitPriority, int>
        {
            [RecruitPriority.IceTime] = 70 + (index % 12),
            [RecruitPriority.Development] = 82 - (index % 8),
            [RecruitPriority.Education] = 55 + (index % 10),
            [RecruitPriority.Winning] = 58 + (index % 13),
            [RecruitPriority.DistanceFromHome] = 40 + (index % 20),
            [RecruitPriority.Facilities] = 63 + (index % 14),
            [RecruitPriority.Coaching] = 78 - (index % 9),
            [RecruitPriority.PathwayToHigherHockey] = 80 - (index % 10),
            [RecruitPriority.FamilyComfort] = 60 + (index % 12)
        };

    private static string ProjectionFor(int index) =>
        index switch
        {
            0 => "Top board forward with high pace, strong habits, and a realistic top-six junior path.",
            1 => "Reliable defense prospect; projection leans safer than flashy.",
            2 => "High-upside goalie with uneven viewings and strong technical growth indicators.",
            3 => "Competitive center whose family comfort and school fit may decide recruitment.",
            4 => "Import winger with skill, confidence, and adjustment risk.",
            _ => "Useful draft option with incomplete information and enough traits to keep tracking."
        };

    private static string AnalyticsFor(int index) =>
        index switch
        {
            0 => "Analytics: top pace and chance-creation indicators in this class.",
            1 => "Analytics: low-risk transition profile, limited offensive ceiling.",
            2 => "Analytics: volatile goalie sample, strong recovery trend.",
            _ when index % 5 == 0 => "Analytics: high-event profile with some risk in repeat viewings.",
            _ => "Analytics: neutral profile; more scouting confidence needed."
        };

    private static void QueueScenarioEvent(
        EventEngine eventEngine,
        DateOnly date,
        string organizationId,
        string gmPersonId,
        DateOnly draftDate)
    {
        var legacyEvent = eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, TimeSpan.Zero),
            LegacyEventType.Generic,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            "New GM scenario started",
            $"The newly hired GM took over two weeks before the draft on {draftDate:yyyy-MM-dd}.",
            new LegacyEventContext(PrimaryPersonId: gmPersonId, OrganizationId: organizationId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_1_0_new_gm", ["draft_date"] = draftDate });

        eventEngine.QueueEvent(legacyEvent);
    }
}
