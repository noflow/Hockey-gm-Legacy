using LegacyEngine.Contracts;
using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Names;
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
using PeopleCareerTimelineEntry = LegacyEngine.People.CareerTimelineEntry;

namespace LegacyEngine.Integration;

public sealed class NewGmScenarioBootstrapper
{
    public NewGmScenarioResult Bootstrap(NewGmScenarioSettings? settings = null, Rulebook? rulebook = null)
    {
        var scenarioSettings = settings ?? new NewGmScenarioSettings();
        scenarioSettings.Validate();
        var activeRulebook = rulebook ?? RulebookPresets.CreateJuniorMajor();
        var leagueProfile = ResolveLeagueProfile(scenarioSettings, activeRulebook);
        var teamSelection = ResolveTeamSelection(scenarioSettings);

        var seasonCalendar = SeasonCalendar.Build(scenarioSettings.SeasonYear, scenarioSettings.SeasonSettings);
        var draftDate = seasonCalendar.Milestones.Single(item => item.Type == SeasonMilestoneType.Draft).Date.Value;
        var startDate = draftDate.AddDays(-14);

        var worldEngine = WorldEngine.CreateWorld(
            scenarioSettings.WorldName,
            startDate,
            WorldPhase.Offseason,
            eventEngine: new EventEngine());
        var registry = EngineRegistry.Create(worldEngine, activeRulebook);
        var nameRegistry = new NameUniquenessRegistry();
        var nameGenerator = new NameGenerator(NameGenerationSettings.CreateDefault(scenarioSettings.SeasonYear + 231));
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
        var ownerName = nameGenerator.Generate(nameRegistry, ClassScope(scenarioSettings.SeasonYear, "owners"), NameOrigin.CanadaEnglish, NameOrigin.CanadaFrench);
        var headCoachName = nameGenerator.Generate(nameRegistry, ClassScope(scenarioSettings.SeasonYear, "staff"), StaffOrigins());
        var assistantCoachName = nameGenerator.Generate(nameRegistry, ClassScope(scenarioSettings.SeasonYear, "staff"), StaffOrigins());
        var headScoutName = nameGenerator.Generate(nameRegistry, ClassScope(scenarioSettings.SeasonYear, "scouts"), StaffOrigins());
        var regionalScoutName = nameGenerator.Generate(nameRegistry, ClassScope(scenarioSettings.SeasonYear, "scouts"), StaffOrigins());
        var ownerPerson = CreatePerson("person-owner-evelyn-hart", ownerName.FirstName, ownerName.LastName, Gender.Female, new DateOnly(1965, 6, 4), ownerName.Nationality, ownerName.Birthplace, 70, 62, 28)
            .AddRole(new PersonRole("role-owner", PersonRoleType.Owner, scenarioSettings.OrganizationId, new DateOnly(2018, 5, 10), null, "Owner"));
        var headCoach = CreatePerson("person-coach-head", headCoachName.FirstName, headCoachName.LastName, Gender.Male, new DateOnly(1975, 11, 6), headCoachName.Nationality, headCoachName.Birthplace, 58, 52, 18)
            .AddRole(new PersonRole("role-head-coach", PersonRoleType.Coach, scenarioSettings.OrganizationId, new DateOnly(2024, 6, 1), null, "Head Coach"));
        var assistantCoach = CreatePerson("person-coach-assistant", assistantCoachName.FirstName, assistantCoachName.LastName, Gender.Female, new DateOnly(1984, 1, 27), assistantCoachName.Nationality, assistantCoachName.Birthplace, 50, 44, 12)
            .AddRole(new PersonRole("role-assistant-coach", PersonRoleType.Coach, scenarioSettings.OrganizationId, new DateOnly(2025, 7, 1), null, "Assistant Coach"));
        var headScoutPerson = CreatePerson("person-scout-head", headScoutName.FirstName, headScoutName.LastName, Gender.Female, new DateOnly(1979, 7, 19), headScoutName.Nationality, headScoutName.Birthplace, 55, 50, 14)
            .AddRole(new PersonRole("role-head-scout", PersonRoleType.Scout, scenarioSettings.OrganizationId, new DateOnly(2023, 8, 1), null, "Head Scout"));
        var regionalScoutPerson = CreatePerson("person-scout-west", regionalScoutName.FirstName, regionalScoutName.LastName, Gender.Male, new DateOnly(1982, 9, 8), regionalScoutName.Nationality, regionalScoutName.Birthplace, 44, 39, 8)
            .AddRole(new PersonRole("role-regional-scout", PersonRoleType.Scout, scenarioSettings.OrganizationId, new DateOnly(2025, 8, 1), null, "Regional Scout"));

        var rosterPlayers = CreateRosterPlayers(startDate, scenarioSettings.OrganizationId, nameGenerator, nameRegistry, scenarioSettings.SeasonYear);
        var recruits = CreateRecruitPeople(startDate, nameGenerator, nameRegistry, scenarioSettings.SeasonYear);
        var freeAgentPeople = CreateFreeAgentPeople(startDate, nameGenerator, nameRegistry, scenarioSettings.SeasonYear);
        var tradeBlockPeople = TradeService.CreateTradeBlockPeople(startDate, scenarioSettings.SeasonYear, nameGenerator, nameRegistry);
        var people = new[] { gm, ownerPerson, headCoach, assistantCoach, headScoutPerson, regionalScoutPerson }
            .Concat(rosterPlayers)
            .Concat(recruits)
            .Concat(freeAgentPeople)
            .Concat(tradeBlockPeople)
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
        var staffContracts = CreateContracts(registry, scenarioSettings.OrganizationId, startDate, activeRulebook, scenarioSettings.SeasonSettings, gm, headCoach, assistantCoach, headScoutPerson, regionalScoutPerson);
        var playerContracts = CreateRosterPlayerContracts(registry, scenarioSettings.OrganizationId, startDate, scenarioSettings.SeasonSettings, rosterPlayers);
        var contracts = staffContracts.Concat(playerContracts).ToArray();
        var staffMembers = CreateStaff(registry, scenarioSettings.OrganizationId, startDate, headCoach, assistantCoach, headScoutPerson, regionalScoutPerson, staffContracts);
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
        var inheritedScoutingReports = CreateInheritedScoutingReports(draftBoard, recruits, scout, startDate);

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
            rosterPlayers,
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
            LeagueProfile: leagueProfile,
            TeamSelection: teamSelection,
            DraftDate: draftDate,
            FirstDayInbox: firstDayInbox,
            ScenarioSummary: ScenarioSummaryFor(leagueProfile, gmProfile.PreferredName, organization.Name))
        {
            CompletedScoutingReports = inheritedScoutingReports
        };
        var history = new ExistingWorldHistoryService().CreateHistory(scenarioSnapshot);
        var freeAgentMarket = new FreeAgentMarketService().GenerateMarket(scenarioSnapshot, freeAgentPeople);
        var tradeBlock = new TradeService().GenerateTradeBlock(scenarioSnapshot, tradeBlockPeople);
        scenarioSnapshot = scenarioSnapshot with
        {
            PriorSeasonStats = history.PriorSeasonStats.Concat(freeAgentMarket.FreeAgents.Select(agent => agent.LastSeasonStats)).ToArray(),
            CareerStatSummaries = history.CareerStatSummaries.Concat(freeAgentMarket.FreeAgents.Select(agent => agent.CareerStats)).ToArray(),
            PlayerTeamHistories = history.PlayerTeamHistories,
            PlayerCareerTimelines = history.PlayerCareerTimelines,
            OrganizationHistory = history.OrganizationHistory,
            DraftHistory = history.DraftHistory,
            FreeAgentMarket = freeAgentMarket,
            TradeBlock = tradeBlock
        };
        var careerHistory = new CareerHistoryService().CreateInitialHistory(scenarioSnapshot);
        scenarioSnapshot = scenarioSnapshot with
        {
            CareerTimeline = careerHistory.CareerTimeline,
            DraftPickHistory = careerHistory.DraftPickHistory,
            DraftClassHistory = careerHistory.DraftClassHistory,
            StaffCareerHistory = careerHistory.StaffCareerHistory,
            GmCareerHistory = careerHistory.GmCareerHistory,
            OrganizationSeasonHistory = careerHistory.OrganizationSeasonHistory,
            TransactionHistory = careerHistory.TransactionHistory
        };
        scenarioSnapshot = new PlayerPipelineService().EnsurePipeline(scenarioSnapshot);
        scenarioSnapshot = new DevelopmentPlanningService().EnsureScenarioPlans(scenarioSnapshot);
        QueueScenarioEvent(registry.EventEngine, startDate, scenarioSettings.OrganizationId, gm.PersonId, draftDate, LegacyEventType.FreeAgentMarketOpened, "Free agent market opened", $"{freeAgentMarket.FreeAgents.Count} unsigned players are available for review.");
        QueueScenarioEvent(registry.EventEngine, startDate, scenarioSettings.OrganizationId, gm.PersonId, draftDate, LegacyEventType.TradeBlockUpdated, "League trade block updated", $"{tradeBlock.Entries.Count} players are available on the league trade block.");
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

    private static LeagueProfile ResolveLeagueProfile(NewGmScenarioSettings settings, Rulebook rulebook)
    {
        if (settings.LeagueProfile is not null)
        {
            return settings.LeagueProfile with { Rulebook = rulebook };
        }

        var service = new MultiLeagueCareerService();
        var experience = rulebook.LeagueType switch
        {
            "nhl_style" => LeagueExperience.Nhl,
            "ahl_style" => LeagueExperience.Ahl,
            "custom" => LeagueExperience.Custom,
            _ => settings.LeagueExperience
        };
        return service.GetProfile(experience) with { Rulebook = rulebook };
    }

    private static TeamSelectionOption ResolveTeamSelection(NewGmScenarioSettings settings) =>
        settings.TeamSelection
        ?? new TeamSelectionOption(
            settings.OrganizationId,
            settings.TeamName,
            settings.TeamCity,
            settings.TeamRegion,
            settings.TeamCountry,
            "crest placeholder",
            settings.PreviousRecord,
            settings.OwnerExpectations,
            1_150_000m,
            50,
            "Balanced",
            "Balanced",
            "Balanced",
            settings.ParentOrganizationId,
            settings.AffiliateOrganizationId);

    private static string ScenarioSummaryFor(LeagueProfile profile, string preferredName, string organizationName) =>
        profile.Experience switch
        {
            LeagueExperience.Nhl => $"{preferredName} takes over as GM of {organizationName} two weeks before the pro draft.",
            LeagueExperience.Ahl => $"{preferredName} takes over as GM of {organizationName} during affiliate roster planning.",
            LeagueExperience.Custom => $"{preferredName} takes over as GM of {organizationName} in a custom league placeholder career.",
            _ => $"{preferredName} takes over as GM of {organizationName} two weeks before the junior draft."
        };

    private static Organization CreateOrganization(
        EngineRegistry registry,
        NewGmScenarioSettings settings,
        string ownerPersonId,
        DateOnly startDate) =>
        registry.OrganizationEngine.CreateOrganization(
            settings.OrganizationId,
            OrganizationType.Team,
            new OrganizationIdentity(settings.TeamName, settings.TeamCity, settings.TeamRegion, settings.TeamCountry),
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
                new OrganizationDepartment("dept-executive", "Executive", StaffDepartment.Executive),
                new OrganizationDepartment("dept-coaching", "Coaching", StaffDepartment.Coaching),
                new OrganizationDepartment("dept-scouting", "Scouting", StaffDepartment.Scouting),
                new OrganizationDepartment("dept-medical", "Medical", StaffDepartment.Medical),
                new OrganizationDepartment("dept-equipment", "Equipment", StaffDepartment.Equipment)
            },
            parentOrganizationId: settings.ParentOrganizationId,
            affiliateOrganizationId: settings.AffiliateOrganizationId);

    private static IReadOnlyList<Contract> CreateContracts(
        EngineRegistry registry,
        string organizationId,
        DateOnly startDate,
        Rulebook rulebook,
        SeasonSettings seasonSettings,
        params Person[] people)
    {
        var contracts = new List<Contract>();
        var budget = new StaffBudgetService();
        for (var index = 0; index < people.Length; index++)
        {
            var person = people[index];
            var type = index switch
            {
                0 => ContractType.GMContract,
                1 or 2 => ContractType.CoachContract,
                _ => ContractType.ScoutContract
            };
            var salary = index switch
            {
                0 => budget.GmSalaryRange(rulebook).Midpoint,
                1 => budget.RangeFor(StaffRole.HeadCoach, rulebook).Midpoint,
                2 => budget.RangeFor(StaffRole.AssistantCoach, rulebook).Midpoint,
                3 => budget.RangeFor(StaffRole.HeadScout, rulebook).Midpoint,
                _ => budget.RangeFor(StaffRole.Scout, rulebook).Midpoint
            };
            var contractStart = startDate.AddDays(-30);
            var offered = registry.ContractEngine.CreateOffer(new ContractOffer(
                OfferId: $"new-gm-contract-offer-{index + 1:00}",
                PersonId: person.PersonId,
                OrganizationId: organizationId,
                ContractType: type,
                Term: ContractExpiryCalendar.TermForYears(contractStart, seasonSettings, 1),
                Money: new ContractMoney(salary, Currency: "CAD"),
                Clauses: Array.Empty<ContractClause>(),
                OfferedOn: contractStart,
                Notes: "Existing organization contract reference for the Alpha 1.0 scenario."));
            contracts.Add(registry.ContractEngine.SignContract(offered, startDate.AddDays(-29)).Contract);
        }

        return contracts;
    }

    private static IReadOnlyList<Contract> CreateRosterPlayerContracts(
        EngineRegistry registry,
        string organizationId,
        DateOnly startDate,
        SeasonSettings seasonSettings,
        IReadOnlyList<Person> rosterPlayers)
    {
        var contracts = new List<Contract>();
        for (var index = 0; index < rosterPlayers.Count; index++)
        {
            var player = rosterPlayers[index];
            var termYears = index switch
            {
                < 8 => 1,
                < 14 => 2,
                _ => 3
            };
            var offeredOn = startDate.AddMonths(-10).AddDays(index % 20);
            var term = ContractExpiryCalendar.TermForYears(offeredOn, seasonSettings, termYears);
            var offered = registry.ContractEngine.CreateOffer(new ContractOffer(
                OfferId: $"inherited-player-contract-{index + 1:00}",
                PersonId: player.PersonId,
                OrganizationId: organizationId,
                ContractType: ContractType.JuniorPlayerAgreement,
                Term: term,
                Money: new ContractMoney(SalaryOrStipend: 1_200m + (index % 6 * 150m), Currency: "CAD"),
                Clauses: index % 4 == 0
                    ? new[] { new ContractClause("education-support", ContractClauseType.EducationPackage, "Existing education support from prior management.") }
                    : Array.Empty<ContractClause>(),
                OfferedOn: offeredOn,
                Notes: "Inherited junior player agreement from previous management."));
            var signed = registry.ContractEngine.SignContract(offered, offeredOn.AddDays(1)).Contract;
            contracts.Add(term.EndDate < startDate ? signed.Expire(startDate) : signed);
        }

        return contracts;
    }

    private static IReadOnlyList<ScoutingReport> CreateInheritedScoutingReports(
        DraftBoard draftBoard,
        IReadOnlyList<Person> recruits,
        Scout scout,
        DateOnly startDate)
    {
        var reports = new List<ScoutingReport>();
        var recruitById = recruits.ToDictionary(person => person.PersonId, StringComparer.Ordinal);
        foreach (var entry in draftBoard.Entries.OrderBy(entry => entry.Rank).Take(45))
        {
            if (!recruitById.TryGetValue(entry.ProspectPersonId, out var person) || entry.Bio is null)
            {
                continue;
            }

            var currentCenter = entry.Rank switch
            {
                <= 5 => 58,
                <= 15 => 50,
                <= 30 => 44,
                _ => 38
            };
            var potentialCenter = entry.Rank switch
            {
                <= 5 => 78,
                <= 15 => 70,
                <= 30 => 63,
                _ => 56
            };
            var confidence = entry.Rank <= 10
                ? ScoutingConfidenceLevel.High
                : entry.Rank <= 35
                    ? ScoutingConfidenceLevel.Medium
                    : ScoutingConfidenceLevel.Low;
            var uncertainty = confidence switch
            {
                ScoutingConfidenceLevel.High => 7,
                ScoutingConfidenceLevel.Medium => 12,
                _ => 18
            };
            var current = RangeAround(currentCenter, uncertainty);
            var potential = RangeAround(potentialCenter, uncertainty + 4);
            var report = new ScoutingReport(
                ReportId: entry.ScoutingReportId ?? $"scenario-report-{entry.Rank:000}",
                PlayerId: entry.ProspectPersonId,
                ScoutId: scout.ScoutId,
                AssignmentId: $"previous-season-scouting-{entry.Rank:000}",
                CreatedOn: startDate.AddDays(-60 + (entry.Rank % 35)),
                Facts: new[]
                {
                    $"{person.Identity.DisplayName} is a {person.CalculateAge(startDate)}-year-old {entry.Bio.Position}.",
                    $"Current team: {entry.Bio.CurrentTeam} ({entry.Bio.League})."
                },
                Observations: new[]
                {
                    $"Previous staff tracked {entry.Bio.ShootsCatches}, {entry.Bio.HeightDisplay}, {entry.Bio.WeightDisplay}.",
                    entry.Bio.CharacterSummary,
                    entry.AnalyticsSummary
                },
                Opinions: new[]
                {
                    $"Current picture: {CurrentPictureFor(entry, confidence)}",
                    $"Future projection: {entry.Bio.PotentialLineupProjection}; {entry.ProjectionText}",
                    confidence == ScoutingConfidenceLevel.High ? "Staff feel comfortable using this report for draft-day decisions." : "Staff recommend at least one more viewing before making a major commitment."
                },
                Unknowns: new[]
                {
                    "Long-term development curve remains uncertain.",
                    "Translation against older junior competition still needs more evidence."
                },
                Confidence: confidence,
                CurrentAbilityEstimate: current,
                PotentialEstimate: potential,
                Recommendation: RecommendationFor(potential, entry.Rank),
                Details: new Dictionary<string, object?>
                {
                    ["inherited_from_previous_staff"] = true,
                    ["visible_to_new_gm"] = true,
                    ["scouting_year"] = startDate.Year - 1
                });
            report.Validate();
            reports.Add(report);
        }

        return reports;

        static ScoutedRatingRange RangeAround(int center, int uncertainty) =>
            new(Math.Clamp(center - uncertainty, 0, 100), Math.Clamp(center + uncertainty, 0, 100));

        static ScoutingRecommendation RecommendationFor(ScoutedRatingRange potential, int rank)
        {
            if (rank <= 5 || potential.High >= 78)
            {
                return ScoutingRecommendation.PriorityTarget;
            }

            if (rank <= 20 || potential.High >= 68)
            {
                return ScoutingRecommendation.Target;
            }

            return potential.High >= 58 ? ScoutingRecommendation.Consider : ScoutingRecommendation.Watch;
        }

        static string CurrentPictureFor(DraftBoardEntry entry, ScoutingConfidenceLevel confidence) =>
            confidence switch
            {
                ScoutingConfidenceLevel.High => $"clear read on a {entry.Bio!.Position} who already shows draftable junior tools",
                ScoutingConfidenceLevel.Medium => $"working read on a {entry.Bio!.Position}; present ability is useful but still being verified",
                _ => $"basic read on a {entry.Bio!.Position}; staff mostly know the bio and role projection"
            };
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
                IsImport: players[index].Identity.Nationality != "Canada",
                AcquisitionSource: AcquisitionSourceFor(settings.LeagueExperience, index))).Roster;
        }

        return roster;
    }

    private static PlayerAcquisitionSource AcquisitionSourceFor(LeagueExperience experience, int index) =>
        experience switch
        {
            LeagueExperience.Ahl when index < 10 => PlayerAcquisitionSource.AssignedFromParentClub,
            LeagueExperience.Ahl when index < 16 => PlayerAcquisitionSource.TwoWayContract,
            LeagueExperience.Ahl => PlayerAcquisitionSource.AhlContract,
            _ => PlayerAcquisitionSource.Unknown
        };

    private static DraftBoard CreateDraftBoard(
        NewGmScenarioSettings settings,
        IReadOnlyList<RecruitProfile> recruits)
    {
        var board = DraftBoard.Create(settings.DraftBoardId, settings.OrganizationId);
        for (var index = 0; index < recruits.Count; index++)
        {
            var position = PositionFor(index);
            board = board.AddProspect(new DraftBoardEntry(
                ProspectPersonId: recruits[index].RecruitPersonId,
                Rank: index + 1,
                ScoutingReportId: index < 45 ? $"scenario-report-{index + 1:000}" : null,
                ScoutingConfidence: index < 10 ? ScoutingConfidenceLevel.High : index < 45 ? ScoutingConfidenceLevel.Medium : ScoutingConfidenceLevel.Low,
                ProjectionText: ProjectionFor(position, index),
                IsStarred: index < 2,
                PersonalNotes: index < 3 ? "GM note: review again before draft day." : "",
                AnalyticsSummary: AnalyticsFor(position, index),
                Bio: BioFor(recruits[index], position, index)));
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

    private static IReadOnlyList<Person> CreateRosterPlayers(
        DateOnly startDate,
        string organizationId,
        NameGenerator nameGenerator,
        NameUniquenessRegistry nameRegistry,
        int seasonYear)
    {
        return Enumerable.Range(0, 26)
            .Select(index =>
            {
                var name = nameGenerator.Generate(nameRegistry, ClassScope(seasonYear, "roster"), RosterPlayerOrigins(index));
                return CreatePlayer(
                $"person-roster-{index + 1:000}",
                    name.FirstName,
                    name.LastName,
                    RosterBirthDateFor(index),
                    name.Nationality,
                    name.Birthplace,
                organizationId,
                startDate);
            })
            .ToArray();
    }

    private static DateOnly RosterBirthDateFor(int index)
    {
        if (index < 3)
        {
            return new DateOnly(2005, (index % 3) + 1, Math.Min(24, (index % 27) + 1));
        }

        if (index < 10)
        {
            return new DateOnly(2006, 9 + (index % 4), Math.Min(24, (index % 27) + 1));
        }

        var birthYear = index switch
        {
            < 18 => 2007,
            < 23 => 2008,
            _ => 2009
        };

        return new DateOnly(birthYear, (index % 12) + 1, Math.Min(24, (index % 27) + 1));
    }

    private static IReadOnlyList<Person> CreateRecruitPeople(
        DateOnly startDate,
        NameGenerator nameGenerator,
        NameUniquenessRegistry nameRegistry,
        int seasonYear)
    {
        return Enumerable.Range(0, 60)
            .Select(index =>
            {
                var name = nameGenerator.Generate(nameRegistry, ClassScope(seasonYear, "draft-class"), PlayerOrigins());
                return CreatePlayer(
                    $"person-recruit-{index + 1:000}",
                    name.FirstName,
                    name.LastName,
                    new DateOnly(2009, (index % 12) + 1, Math.Min(24, (index % 25) + 1)),
                    name.Nationality,
                    name.Birthplace,
                    "unassigned",
                    startDate);
            })
            .ToArray();
    }

    private static IReadOnlyList<Person> CreateFreeAgentPeople(
        DateOnly startDate,
        NameGenerator nameGenerator,
        NameUniquenessRegistry nameRegistry,
        int seasonYear) =>
        Enumerable.Range(0, 28)
            .Select(index =>
            {
                var name = nameGenerator.Generate(nameRegistry, ClassScope(seasonYear, "free-agent-market"), PlayerOrigins());
                var birthDate = index switch
                {
                    < 4 => startDate.AddYears(-20).AddDays(-(index * 47 + 12)),
                    < 12 => startDate.AddYears(-18).AddDays(-(index * 31 + 7)),
                    _ => startDate.AddYears(-17).AddDays(-(index * 29 + 5))
                };
                return CreatePerson(
                        $"person-free-agent-{index + 1:000}",
                        name.FirstName,
                        name.LastName,
                        Gender.Male,
                        birthDate,
                        name.Nationality,
                        name.Birthplace,
                        28 + index % 15,
                        18 + index % 12,
                        4 + index % 6)
                    .AddRole(new PersonRole($"role-free-agent-player-{index + 1:000}", PersonRoleType.Player, "free-agent-market", startDate.AddYears(-1), null, "Unsigned Player"));
            })
            .ToArray();

    private static string ClassScope(int seasonYear, string group) => $"new-gm-scenario:{seasonYear}:{group}";

    private static NameOrigin[] PlayerOrigins() =>
    [
        NameOrigin.CanadaEnglish,
        NameOrigin.CanadaEnglish,
        NameOrigin.CanadaEnglish,
        NameOrigin.CanadaFrench,
        NameOrigin.Usa,
        NameOrigin.Finland,
        NameOrigin.Sweden,
        NameOrigin.Czechia,
        NameOrigin.Slovakia,
        NameOrigin.Germany,
        NameOrigin.Switzerland,
        NameOrigin.Latvia,
        NameOrigin.GenericEuropean
    ];

    private static NameOrigin[] RosterPlayerOrigins(int index) =>
        index < 24
            ? [NameOrigin.CanadaEnglish, NameOrigin.CanadaEnglish, NameOrigin.CanadaFrench]
            : [NameOrigin.Usa, NameOrigin.Sweden, NameOrigin.Finland, NameOrigin.Czechia];

    private static NameOrigin[] StaffOrigins() =>
    [
        NameOrigin.CanadaEnglish,
        NameOrigin.CanadaFrench,
        NameOrigin.Usa,
        NameOrigin.Sweden,
        NameOrigin.Finland,
        NameOrigin.Czechia,
        NameOrigin.Germany,
        NameOrigin.Switzerland,
        NameOrigin.GenericEuropean
    ];

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
            CareerTimeline: Array.Empty<PeopleCareerTimelineEntry>());
        person.Validate();
        return person;
    }

    internal static Person CreateScenarioPersonForGeneratedSystems(
        string personId,
        string firstName,
        string lastName,
        DateOnly birthDate,
        string nationality,
        string birthplace,
        string organizationId) =>
        CreatePlayer(personId, firstName, lastName, birthDate, nationality, birthplace, organizationId, birthDate.AddYears(15));

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
            [RecruitPriority.FamilyComfort] = 60 + (index % 12),
            [RecruitPriority.TeamCulture] = 58 + (index % 15),
            [RecruitPriority.TrustInGm] = 62 + (index % 10),
            [RecruitPriority.PlayingRole] = 66 + (index % 16)
        };

    private static DraftProspectBio BioFor(RecruitProfile recruit, RosterPosition position, int index)
    {
        var personId = recruit.RecruitPersonId;
        var parts = personId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var seed = Math.Abs(HashCode.Combine(personId, position, index));
        var birthplace = BirthplacePartsFromPersonIdHint(index);
        var height = position switch
        {
            RosterPosition.Goalie => 71 + (seed % 10),
            RosterPosition.Defense => 70 + (seed % 10),
            _ => 68 + (seed % 10)
        };
        var weight = position switch
        {
            RosterPosition.Goalie => 170 + (seed % 81),
            RosterPosition.Defense => 175 + (seed % 66),
            _ => 160 + (seed % 66)
        };
        var league = LeagueFor(birthplace.Country, index);
        var role = position switch
        {
            RosterPosition.Goalie => "development goalie with starter upside",
            RosterPosition.Defense => index % 2 == 0 ? "top-four defense projection" : "mobile second-pair defense projection",
            RosterPosition.Center => "middle-six center projection",
            RosterPosition.LeftWing or RosterPosition.RightWing => "scoring-line winger projection",
            _ => "depth lineup projection"
        };

        return new DraftProspectBio(
            Position: position,
            ShootsCatches: position == RosterPosition.Goalie
                ? (seed % 2 == 0 ? "Catches L" : "Catches R")
                : (seed % 3 == 0 ? "Shoots R" : "Shoots L"),
            HeightInches: height,
            WeightPounds: weight,
            BirthYear: 2009,
            Hometown: birthplace.Hometown,
            ProvinceState: birthplace.Region,
            Country: birthplace.Country,
            CurrentTeam: $"{birthplace.Hometown} {TeamNickname(index)}",
            League: league,
            CharacterSummary: CharacterSummaryFor(index),
            PotentialLineupProjection: role);

        static (string Hometown, string Region, string Country) BirthplacePartsFromPersonIdHint(int value)
        {
            var hometowns = new[]
            {
                ("Saskatoon", "SK", "Canada"),
                ("Red Deer", "AB", "Canada"),
                ("Brandon", "MB", "Canada"),
                ("Kelowna", "BC", "Canada"),
                ("Regina", "SK", "Canada"),
                ("Winnipeg", "MB", "Canada"),
                ("Grand Forks", "ND", "USA"),
                ("Minneapolis", "MN", "USA"),
                ("Turku", "Varsinais-Suomi", "Finland"),
                ("Gothenburg", "Vastra Gotaland", "Sweden"),
                ("Brno", "South Moravia", "Czechia"),
                ("Zurich", "ZH", "Switzerland")
            };
            return hometowns[value % hometowns.Length];
        }

        static string TeamNickname(int value) =>
            new[] { "Raiders", "Blazers", "Kings", "Flyers", "Storm", "Royals", "Tigers", "Saints" }[value % 8];

        static string LeagueFor(string country, int value) =>
            country switch
            {
                "USA" => value % 2 == 0 ? "USHL Futures" : "Minnesota High School",
                "Finland" => "U18 SM-sarja",
                "Sweden" => "J18 Nationell",
                "Czechia" => "Czech U20",
                "Switzerland" => "U20-Elit",
                _ => value % 3 == 0 ? "CSSHL U18" : value % 3 == 1 ? "SMAAAHL" : "AEHL U18"
            };

        static string CharacterSummaryFor(int value) =>
            value switch
            {
                0 => "Highly competitive; staff like his practice pace and confidence.",
                1 => "Reliable habits; coaches describe him as low-maintenance and coachable.",
                2 => "Quiet worker with strong family support and room to grow confidence.",
                3 => "Driven player who wants a clear development plan and ice-time path.",
                _ when value % 5 == 0 => "High-energy personality; leadership traits are emerging.",
                _ => "Solid character profile with no major staff concerns."
            };
    }

    private static string ProjectionFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => index % 2 == 0
                ? "Goalie prospect with starter upside, uneven viewings, and strong technical growth indicators."
                : "Goalie prospect with backup-to-starter range and a development path tied to consistency.",
            RosterPosition.Defense => index % 2 == 0
                ? "Defense prospect with top-four upside, mobility, and enough puck-moving skill to keep tracking."
                : "Defense prospect with second-pair tools, reliable habits, and room to sharpen decision-making.",
            RosterPosition.Center => index % 2 == 0
                ? "Center prospect with middle-six upside, responsible habits, and a realistic junior scoring path."
                : "Center prospect whose faceoff detail, support play, and family comfort may decide recruitment.",
            RosterPosition.LeftWing or RosterPosition.RightWing => index % 2 == 0
                ? "Winger prospect with scoring-line traits, pace, and adjustment risk."
                : "Winger prospect with useful compete, finishing touch, and enough traits to keep tracking.",
            _ => "Draft option with incomplete information and enough traits to keep tracking."
        };

    private static string AnalyticsFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => index % 2 == 0
                ? "Analytics: volatile goalie sample, strong recovery trend."
                : "Analytics: goalie tracking shows stable workload response with rebound control still under review.",
            RosterPosition.Defense => index % 2 == 0
                ? "Analytics: low-risk transition profile with controlled defensive-zone exits."
                : "Analytics: defensive involvement is steady; offensive ceiling needs more viewings.",
            RosterPosition.Center => index % 2 == 0
                ? "Analytics: strong support-lane and puck-touch profile for a center."
                : "Analytics: two-way center indicators are useful, with faceoff sample still developing.",
            RosterPosition.LeftWing or RosterPosition.RightWing => index % 2 == 0
                ? "Analytics: winger chance-creation indicators are trending up."
                : "Analytics: wing profile is neutral; more scouting confidence needed.",
            _ => "Analytics: neutral profile; more scouting confidence needed."
        };

    private static void QueueScenarioEvent(
        EventEngine eventEngine,
        DateOnly date,
        string organizationId,
        string gmPersonId,
        DateOnly draftDate,
        LegacyEventType eventType = LegacyEventType.Generic,
        string title = "New GM scenario started",
        string? description = null)
    {
        var legacyEvent = eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description ?? $"The newly hired GM took over two weeks before the draft on {draftDate:yyyy-MM-dd}.",
            new LegacyEventContext(PrimaryPersonId: gmPersonId, OrganizationId: organizationId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_1_0_new_gm", ["draft_date"] = draftDate });

        eventEngine.QueueEvent(legacyEvent);
    }
}
