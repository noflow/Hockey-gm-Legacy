using LegacyEngine.Events;
using LegacyEngine.Names;
using LegacyEngine.People;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;
using PeopleCareerTimelineEntry = LegacyEngine.People.CareerTimelineEntry;

namespace LegacyEngine.Integration;

public sealed class ScoutingOperationsService
{
    public IReadOnlyList<ScoutingOperationScoutProfile> BuildScoutProfiles(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return ScoutStaff(scenario)
            .Select(member =>
            {
                var relationship = RelationshipWithGm(scenario, member.PersonId);
                var active = scenario.ScoutingOperations
                    .Where(assignment => assignment.ScoutPersonId == member.PersonId && assignment.IsOpen)
                    .ToArray();
                var strengths = Strengths(member).ToArray();
                var weaknesses = Weaknesses(member).ToArray();
                var warning = relationship < 40
                    ? "Relationship warning: trust/communication with the GM is poor."
                    : active.Length >= 3
                        ? "Workload warning: assignment quality may fall."
                        : "No major warning.";

                var profile = new ScoutingOperationScoutProfile(
                    ScoutPersonId: member.PersonId,
                    Name: PersonName(scenario, member.PersonId),
                    Role: member.CurrentRole.ToString(),
                    RegionSpecialty: RegionSpecialty(member),
                    Strengths: strengths.Length == 0 ? new[] { "Reliable hockey operations support" } : strengths,
                    Weaknesses: weaknesses.Length == 0 ? new[] { "No major weakness flagged" } : weaknesses,
                    Reputation: member.Profile.Reputation,
                    RelationshipWithGm: relationship,
                    CurrentAssignment: active.FirstOrDefault()?.TargetName ?? "Unassigned",
                    Workload: active.Length,
                    ConflictWarning: warning);
                profile.Validate();
                return profile;
            })
            .ToArray();
    }

    public ScoutingOperationResult AssignScoutToRegion(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string scoutPersonId,
        ScoutingRegionFocus region,
        ScoutingOperationPriority priority,
        string notes,
        DateOnly? expectedReportDate = null)
    {
        var member = FindScoutStaff(scenario, scoutPersonId);
        var assignment = BuildAssignment(
            scenario,
            member,
            ScoutingOperationAssignmentType.Region,
            region,
            targetPlayerId: null,
            targetName: Display(region),
            priority,
            notes,
            expectedReportDate);

        QueueEvent(registry, scenario, LegacyEventType.ScoutAssignedToRegion, "Scout assigned to region", $"{assignment.ScoutName} was assigned to {assignment.TargetName}.", scoutPersonId);
        return AssignmentResult(
            scenario with { ScoutingOperations = scenario.ScoutingOperations.Append(assignment).ToArray() },
            assignment,
            $"Assigned {assignment.ScoutName} to {assignment.TargetName}.",
            LegacyEventType.ScoutAssignedToRegion,
            "Scout region assignment",
            $"{assignment.ScoutName} is scouting {assignment.TargetName}. Priority: {priority}. Expected report: {assignment.ExpectedReportDate:yyyy-MM-dd}.");
    }

    public ScoutingOperationResult AssignScoutToPlayer(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string scoutPersonId,
        string playerPersonId,
        ScoutingOperationPriority priority,
        string notes,
        DateOnly? expectedReportDate = null)
    {
        var member = FindScoutStaff(scenario, scoutPersonId);
        var playerName = PersonName(scenario, playerPersonId);
        var assignment = BuildAssignment(
            scenario,
            member,
            ScoutingOperationAssignmentType.Player,
            targetRegion: null,
            playerPersonId,
            playerName,
            priority,
            notes,
            expectedReportDate);

        QueueEvent(registry, scenario, LegacyEventType.ScoutAssignedToPlayer, "Scout assigned to player", $"{assignment.ScoutName} was assigned to scout {assignment.TargetName}.", scoutPersonId, playerPersonId);
        return AssignmentResult(
            scenario with { ScoutingOperations = scenario.ScoutingOperations.Append(assignment).ToArray() },
            assignment,
            $"Assigned {assignment.ScoutName} to {assignment.TargetName}.",
            LegacyEventType.ScoutAssignedToPlayer,
            "Scout player assignment",
            $"{assignment.ScoutName} is scouting {assignment.TargetName}. Priority: {priority}. Expected report: {assignment.ExpectedReportDate:yyyy-MM-dd}.");
    }

    public ScoutingOperationResult AdvanceAssignments(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var updatedAssignments = new List<ScoutingOperationAssignment>();
        var reports = new List<ScoutingReport>(scenario.CompletedScoutingReports);
        var inbox = new List<AlphaInboxItem>();
        var completedAssignments = new List<ScoutingOperationAssignment>();
        var completed = 0;

        foreach (var assignment in scenario.ScoutingOperations)
        {
            if (!assignment.IsOpen)
            {
                updatedAssignments.Add(assignment);
                continue;
            }

            var progressed = assignment with { ProgressDays = assignment.ProgressDays + 1 };
            var activeForScout = scenario.ScoutingOperations.Count(item => item.ScoutPersonId == assignment.ScoutPersonId && item.IsOpen);
            if (scenario.CurrentDate < progressed.ExpectedReportDate)
            {
                updatedAssignments.Add(progressed);
                continue;
            }

            if (activeForScout >= 3 && progressed.Status == ScoutingOperationStatus.Active)
            {
                updatedAssignments.Add(progressed with
                {
                    Status = ScoutingOperationStatus.Delayed,
                    ExpectedReportDate = progressed.ExpectedReportDate.AddDays(1),
                    CommunicationQuality = Math.Max(0, progressed.CommunicationQuality - 8)
                });
                continue;
            }

            var report = GenerateReport(registry, scenario, progressed, activeForScout);
            reports.Add(report);
            var completedAssignment = progressed with
            {
                Status = ScoutingOperationStatus.Completed,
                CompletedOn = scenario.CurrentDate,
                ReportId = report.ReportId
            };
            completedAssignment.Validate();
            updatedAssignments.Add(completedAssignment);
            completedAssignments.Add(completedAssignment);
            completed++;

            QueueEvent(registry, scenario, LegacyEventType.ScoutAssignmentCompleted, "Scouting assignment completed", $"{assignment.ScoutName} completed a report on {assignment.TargetName}.", assignment.ScoutPersonId, report.PlayerId);
            QueueEvent(registry, scenario, LegacyEventType.ScoutingReportUpdated, "Scouting report updated", $"{assignment.TargetName} report confidence: {report.Confidence}.", assignment.ScoutPersonId, report.PlayerId);
            var subject = assignment.AssignmentType == ScoutingOperationAssignmentType.Player
                ? $"Scout Report Complete: {assignment.TargetName}"
                : $"{assignment.TargetName} Scouting Trip Complete";
            var duration = assignment.DurationDays <= 0 ? "same-day" : $"{assignment.DurationDays} day";
            if (assignment.DurationDays > 1)
            {
                duration += "s";
            }

            inbox.Add(Inbox(
                scenario,
                LegacyEventType.ScoutAssignmentCompleted,
                subject,
                $"{assignment.ScoutName} completed {assignment.TargetName} after a {duration} assignment. Confidence: {report.Confidence}. Recommendation: {report.Recommendation}."));
        }

        var updated = scenario with
        {
            ScoutingOperations = updatedAssignments.ToArray(),
            CompletedScoutingReports = reports
                .GroupBy(report => report.ReportId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(report => report.CreatedOn)
                .ThenBy(report => report.ReportId, StringComparer.Ordinal)
                .ToArray()
        };
        var intelligence = new ScoutingIntelligenceService();
        foreach (var assignment in completedAssignments)
        {
            var report = updated.CompletedScoutingReports.FirstOrDefault(item => item.ReportId == assignment.ReportId);
            if (report is null)
            {
                continue;
            }

            updated = intelligence.UpdateKnowledgeFromReport(updated, report, assignment).ScenarioSnapshot;
        }

        return Result(true, updated, null, null, inbox, completed == 0 ? "No scouting assignments completed today." : $"Completed {completed} scouting assignment(s).");
    }

    public ScoutingOperationResult GenerateStaffConflictWarning(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var warning = ScoutStaff(scenario)
            .Select(member => new { Member = member, Relationship = RelationshipWithGm(scenario, member.PersonId) })
            .OrderBy(item => item.Relationship)
            .FirstOrDefault(item => item.Relationship < 50);

        if (warning is null)
        {
            return Result(true, scenario, null, null, Array.Empty<AlphaInboxItem>(), "No staff relationship conflict warning generated.");
        }

        var name = PersonName(scenario, warning.Member.PersonId);
        QueueEvent(registry, scenario, LegacyEventType.StaffConflictWarning, "Staff conflict warning", $"{name} may not be fully aligned with the GM.", warning.Member.PersonId);
        var inbox = new[]
        {
            Inbox(scenario, LegacyEventType.StaffConflictWarning, "Staff relationship warning", $"{name} has a low GM relationship score ({warning.Relationship}). Communication quality may suffer.")
        };
        return Result(true, scenario, null, null, inbox, $"Staff relationship warning generated for {name}.");
    }

    public ScoutingOperationResult ReassignStaffRole(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        StaffRole newRole)
    {
        var member = scenario.StaffMembers.SingleOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Staff member was not found.", nameof(personId));
        var updatedMember = member.CurrentAssignment is null
            ? registry.StaffEngine.AssignRole(member, newRole, scenario.CurrentDate)
            : registry.StaffEngine.ReassignRole(member, newRole, scenario.CurrentDate);
        var updated = ReplaceStaffMember(scenario, updatedMember);
        QueueEvent(registry, updated, LegacyEventType.StaffRoleChanged, "Staff role changed", $"{PersonName(updated, personId)} was reassigned to {newRole}.", personId);
        return Result(
            true,
            updated,
            null,
            null,
            new[] { Inbox(updated, LegacyEventType.StaffRoleChanged, "Staff role changed", $"{PersonName(updated, personId)} was reassigned to {newRole}.") },
            $"{PersonName(updated, personId)} reassigned to {newRole}.");
    }

    public ScoutingOperationResult ReleaseStaff(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        string reason)
    {
        var member = scenario.StaffMembers.SingleOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Staff member was not found.", nameof(personId));
        var released = registry.StaffEngine.RemoveStaffMember(member, scenario.CurrentDate, reason);
        var updated = ReplaceStaffMember(scenario, released);
        return Result(
            true,
            updated,
            null,
            null,
            new[] { Inbox(updated, LegacyEventType.StaffReleased, "Staff released", $"{PersonName(updated, personId)} was released. Reason: {reason}") },
            $"{PersonName(updated, personId)} released.");
    }

    public ScoutingOperationResult HirePlaceholderStaffCandidate(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        StaffRole role)
    {
        var sequence = scenario.StaffMembers.Count + 1;
        var personId = $"person-placeholder-staff-{sequence:000}";
        var nameRegistry = new NameUniquenessRegistry();
        foreach (var existing in scenario.AlphaSnapshot.People)
        {
            nameRegistry.RegisterExisting($"scouting-placeholder-staff:{scenario.Season.Year}", existing.Identity.DisplayName);
        }

        var generatedName = new NameGenerator(NameGenerationSettings.CreateDefault(scenario.Season.Year + sequence + 91))
            .Generate(
                nameRegistry,
                $"scouting-placeholder-staff:{scenario.Season.Year}",
                NameOrigin.CanadaEnglish,
                NameOrigin.CanadaFrench,
                NameOrigin.Usa,
                NameOrigin.Sweden,
                NameOrigin.Finland,
                NameOrigin.Czechia,
                NameOrigin.GenericEuropean);
        var person = new Person(
            PersonId: personId,
            Identity: new PersonIdentity(generatedName.FirstName, generatedName.LastName, Gender.NonBinary, new DateOnly(1986, 4, 12), generatedName.Nationality, generatedName.Birthplace),
            Status: PersonStatus.Active,
            Roles: new[] { new PersonRole($"role-placeholder-staff-{sequence:000}", ToPersonRoleType(role), scenario.Organization.OrganizationId, scenario.CurrentDate, null, role.ToString()) },
            Reputation: new PersonReputation(32, 28, 10),
            Personality: new PersonalityProfile(58, 62, 54, 61, 70),
            CareerTimeline: Array.Empty<PeopleCareerTimelineEntry>());
        person.Validate();

        var attributes = StaffRoles.DepartmentFor(role) == StaffDepartment.Scouting
            ? StaffAttributes.ForScouting(new Dictionary<StaffScoutingAttribute, int>
            {
                [StaffScoutingAttribute.TalentEvaluation] = 58,
                [StaffScoutingAttribute.CharacterEvaluation] = 62,
                [StaffScoutingAttribute.RegionalKnowledge] = 60,
                [StaffScoutingAttribute.NorthAmericanKnowledge] = 57
            })
            : StaffAttributes.Empty;
        var member = registry.StaffEngine.CreateStaffMember(personId, scenario.Organization.OrganizationId, role, yearsExperience: 4, reputation: 42, attributes: attributes);
        var hired = registry.StaffEngine.AssignRole(registry.StaffEngine.Hire(member, scenario.CurrentDate), role, scenario.CurrentDate);
        var alpha = scenario.AlphaSnapshot with
        {
            People = scenario.AlphaSnapshot.People.Append(person).ToArray(),
            StaffMembers = scenario.AlphaSnapshot.StaffMembers.Append(hired).ToArray()
        };
        var updated = scenario with
        {
            AlphaSnapshot = alpha,
            StaffMembers = scenario.StaffMembers.Append(hired).ToArray()
        };
        updated.Validate();

        return Result(
            true,
            updated,
            null,
            null,
            new[] { Inbox(updated, LegacyEventType.StaffHired, "Placeholder staff hired", $"{person.Identity.DisplayName} was hired as {role}.") },
            $"{person.Identity.DisplayName} hired as {role}.");
    }

    private ScoutingOperationAssignment BuildAssignment(
        NewGmScenarioSnapshot scenario,
        StaffMember member,
        ScoutingOperationAssignmentType assignmentType,
        ScoutingRegionFocus? targetRegion,
        string? targetPlayerId,
        string targetName,
        ScoutingOperationPriority priority,
        string notes,
        DateOnly? expectedReportDate)
    {
        var workload = scenario.ScoutingOperations.Count(item => item.ScoutPersonId == member.PersonId && item.IsOpen) + 1;
        var relationship = RelationshipWithGm(scenario, member.PersonId);
        var due = expectedReportDate ?? scenario.CurrentDate.AddDays(priority switch
        {
            ScoutingOperationPriority.Urgent => 1,
            ScoutingOperationPriority.High => 2,
            ScoutingOperationPriority.Low => 5,
            _ => 3
        });
        var assignment = new ScoutingOperationAssignment(
            AssignmentId: $"scouting-op-{Guid.NewGuid():N}",
            ScoutPersonId: member.PersonId,
            ScoutName: PersonName(scenario, member.PersonId),
            AssignmentType: assignmentType,
            TargetRegion: targetRegion,
            TargetPlayerId: targetPlayerId,
            TargetName: targetName,
            StartDate: scenario.CurrentDate,
            ExpectedReportDate: due,
            Priority: priority,
            Notes: notes,
            Status: ScoutingOperationStatus.Active,
            WorkloadAtAssignment: workload,
            RelationshipQualityAtAssignment: relationship,
            CommunicationQuality: Math.Clamp((relationship + member.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation)) / 2, 0, 100),
            DurationDays: Math.Max(0, due.DayNumber - scenario.CurrentDate.DayNumber),
            ReturnDate: due);
        assignment.Validate();
        return assignment;
    }

    private ScoutingReport GenerateReport(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        ScoutingOperationAssignment assignment,
        int activeForScout)
    {
        var member = FindScoutStaff(scenario, assignment.ScoutPersonId);
        var scout = BuildScout(scenario, member, assignment, activeForScout);
        var playerId = assignment.AssignmentType == ScoutingOperationAssignmentType.Player
            ? assignment.TargetPlayerId!
            : scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First().ProspectPersonId;
        var player = BuildPlayerSnapshot(scenario, playerId);
        var legacyAssignment = new ScoutingAssignment(
            AssignmentId: assignment.AssignmentId,
            ScoutId: scout.ScoutId,
            AssignmentType: assignment.AssignmentType == ScoutingOperationAssignmentType.Player ? ScoutingAssignmentType.Player : ScoutingAssignmentType.League,
            TargetId: player.PlayerId,
            TargetName: player.Name,
            FocusAreas: FocusAreas(assignment).ToArray(),
            AssignedOn: assignment.StartDate,
            DueOn: assignment.ExpectedReportDate);

        return registry.ScoutingReportGenerator.GenerateReport(scout, legacyAssignment, player, scenario.CurrentDate);
    }

    private static Scout BuildScout(
        NewGmScenarioSnapshot scenario,
        StaffMember member,
        ScoutingOperationAssignment assignment,
        int activeForScout)
    {
        var baseAccuracy = member.PersonId == scenario.AlphaSnapshot.ScoutPerson.PersonId
            ? scenario.AlphaSnapshot.Scout.Accuracy
            : Math.Max(45, member.Attributes.ScoutingScore(StaffScoutingAttribute.TalentEvaluation));
        var fit = FocusAreas(assignment).Any(area => StaffSupports(member, area)) ? 10 : -10;
        var workloadPenalty = activeForScout >= 3 ? 18 : activeForScout == 2 ? 8 : 0;
        var relationshipPenalty = assignment.RelationshipQualityAtAssignment < 40 ? 14 : assignment.RelationshipQualityAtAssignment < 55 ? 6 : 0;
        var communicationPenalty = assignment.CommunicationQuality < 40 ? 8 : 0;
        var accuracy = Math.Clamp(baseAccuracy + fit - workloadPenalty - relationshipPenalty - communicationPenalty, 0, 100);
        var diligence = Math.Clamp(member.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge) + 8 - workloadPenalty / 2, 0, 100);
        var scout = new Scout(
            ScoutId: member.PersonId,
            Name: PersonName(scenario, member.PersonId),
            Specialties: SpecialtiesFor(member).ToArray(),
            Accuracy: accuracy,
            Diligence: diligence,
            ReportBias: scenario.AlphaSnapshot.Scout.ReportBias);
        scout.Validate();
        return scout;
    }

    private static PlayerScoutingSnapshot BuildPlayerSnapshot(NewGmScenarioSnapshot scenario, string personId)
    {
        var person = scenario.AlphaSnapshot.People.SingleOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Scouting target was not found.", nameof(personId));
        var development = scenario.AlphaSnapshot.DevelopmentProfiles.SingleOrDefault(profile => profile.PersonId == personId);
        var position = scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position.ToString()
            ?? PositionFromBoardRank(scenario, personId).ToString();
        var current = development?.CurrentAbility ?? 35 + Math.Abs(personId.GetHashCode()) % 20;
        var potential = development?.Potential ?? Math.Min(90, current + 18);
        var workEthic = development?.TraitValue(LegacyEngine.Development.DevelopmentAttribute.WorkEthic) ?? 60;
        var coachability = development?.TraitValue(LegacyEngine.Development.DevelopmentAttribute.Coachability) ?? 58;
        var injuryRisk = scenario.AlphaSnapshot.Injuries.Any(injury => injury.PersonId == personId && injury.IsActive) ? 72 : 25;
        var character = (workEthic + coachability) / 2;

        return new PlayerScoutingSnapshot(
            PlayerId: person.PersonId,
            Name: person.Identity.DisplayName,
            Age: Math.Max(15, person.CalculateAge(scenario.CurrentDate)),
            Position: position,
            Team: scenario.Organization.Name,
            CurrentAbility: current,
            Potential: potential,
            WorkEthic: workEthic,
            Coachability: coachability,
            InjuryRisk: injuryRisk,
            Character: character);
    }

    private static RosterPosition PositionFromBoardRank(NewGmScenarioSnapshot scenario, string personId)
    {
        var rank = scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(entry => entry.ProspectPersonId == personId)?.Rank ?? 0;
        return rank switch
        {
            2 or 6 => RosterPosition.Defense,
            3 => RosterPosition.Goalie,
            _ when rank % 3 == 0 => RosterPosition.Center,
            _ when rank % 3 == 1 => RosterPosition.LeftWing,
            _ => RosterPosition.RightWing
        };
    }

    private static IReadOnlyList<ScoutSpecialty> FocusAreas(ScoutingOperationAssignment assignment) =>
        assignment.TargetRegion switch
        {
            ScoutingRegionFocus.Goalies => new[] { ScoutSpecialty.Goalie },
            ScoutingRegionFocus.Defensemen => new[] { ScoutSpecialty.Defense },
            ScoutingRegionFocus.Forwards => new[] { ScoutSpecialty.Forward },
            ScoutingRegionFocus.Character => new[] { ScoutSpecialty.Character },
            ScoutingRegionFocus.Medical => new[] { ScoutSpecialty.Medical },
            _ => new[] { ScoutSpecialty.Regional, ScoutSpecialty.Amateur }
        };

    private static IReadOnlyList<ScoutSpecialty> SpecialtiesFor(StaffMember member)
    {
        var specialties = new List<ScoutSpecialty> { ScoutSpecialty.Amateur };
        if (member.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge) >= 60)
        {
            specialties.Add(ScoutSpecialty.Regional);
        }

        if (member.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation) >= 60)
        {
            specialties.Add(ScoutSpecialty.Character);
        }

        if (member.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation) >= 60)
        {
            specialties.Add(ScoutSpecialty.Goalie);
        }

        if (member.Attributes.ScoutingScore(StaffScoutingAttribute.EuropeanKnowledge) >= 60)
        {
            specialties.Add(ScoutSpecialty.Regional);
        }

        return specialties.Distinct().ToArray();
    }

    private static bool StaffSupports(StaffMember member, ScoutSpecialty specialty) =>
        specialty switch
        {
            ScoutSpecialty.Regional => member.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge) >= 60,
            ScoutSpecialty.Character => member.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation) >= 60,
            ScoutSpecialty.Goalie => member.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation) >= 60,
            ScoutSpecialty.Medical => false,
            _ => member.Attributes.ScoutingScore(StaffScoutingAttribute.TalentEvaluation) >= 60
        };

    private static IReadOnlyList<string> Strengths(StaffMember member) =>
        member.Attributes.ScoutingAttributes
            .Where(item => item.Value >= 70)
            .Select(item => item.Key.ToString())
            .ToArray();

    private static IReadOnlyList<string> Weaknesses(StaffMember member) =>
        member.Attributes.ScoutingAttributes
            .Where(item => item.Value <= 45)
            .Select(item => item.Key.ToString())
            .ToArray();

    private static string RegionSpecialty(StaffMember member)
    {
        if (member.Attributes.ScoutingScore(StaffScoutingAttribute.EuropeanKnowledge) >= 65)
        {
            return "Europe";
        }

        if (member.Attributes.ScoutingScore(StaffScoutingAttribute.NorthAmericanKnowledge) >= 65)
        {
            return "Western Canada / North America";
        }

        return member.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge) >= 65
            ? "Regional coverage"
            : "General scouting";
    }

    private static IEnumerable<StaffMember> ScoutStaff(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers.Where(member => member.Department == StaffDepartment.Scouting && member.EmploymentStatus == StaffEmploymentStatus.Employed);

    private static PersonRoleType ToPersonRoleType(StaffRole role) =>
        StaffRoles.DepartmentFor(role) switch
        {
            StaffDepartment.Scouting => PersonRoleType.Scout,
            StaffDepartment.Coaching => PersonRoleType.Coach,
            StaffDepartment.Medical => PersonRoleType.Doctor,
            _ => PersonRoleType.GeneralManager
        };

    private static NewGmScenarioSnapshot ReplaceStaffMember(NewGmScenarioSnapshot scenario, StaffMember member)
    {
        var staff = scenario.StaffMembers
            .Select(item => item.PersonId == member.PersonId ? member : item)
            .ToArray();
        var alpha = scenario.AlphaSnapshot with
        {
            StaffMembers = scenario.AlphaSnapshot.StaffMembers
                .Select(item => item.PersonId == member.PersonId ? member : item)
                .ToArray()
        };
        var updated = scenario with
        {
            AlphaSnapshot = alpha,
            StaffMembers = staff
        };
        updated.Validate();
        return updated;
    }

    private static StaffMember FindScoutStaff(NewGmScenarioSnapshot scenario, string scoutPersonId) =>
        ScoutStaff(scenario).SingleOrDefault(member => member.PersonId == scoutPersonId)
        ?? throw new ArgumentException("Scout staff member was not found.", nameof(scoutPersonId));

    private static int RelationshipWithGm(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == scenario.AlphaSnapshot.GeneralManager.PersonId && relationship.ToPersonId == personId)
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .DefaultIfEmpty(50)
            .First();

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;

    private static string Display(ScoutingRegionFocus region) =>
        region switch
        {
            ScoutingRegionFocus.WesternCanada => "Western Canada",
            ScoutingRegionFocus.EasternCanada => "Eastern Canada",
            ScoutingRegionFocus.Defensemen => "Defensemen",
            _ => region.ToString()
        };

    private static void QueueEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string description,
        string? primaryPersonId,
        string? secondaryPersonId = null)
    {
        var date = scenario.CurrentDate;
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 11, 0, 0, TimeSpan.Zero),
            eventType,
            eventType == LegacyEventType.StaffConflictWarning ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(primaryPersonId, secondaryPersonId, scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_1_9_staff_scouting_operations" });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary) =>
        new(
            InboxItemId: $"inbox:scouting-ops:{Guid.NewGuid():N}",
            Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 11, 30, 0, TimeSpan.Zero),
            EventType: eventType,
            Severity: eventType == LegacyEventType.StaffConflictWarning ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            Title: title,
            Summary: summary,
            PrimaryPersonId: null);

    private static ScoutingOperationResult AssignmentResult(
        NewGmScenarioSnapshot scenario,
        ScoutingOperationAssignment assignment,
        string message,
        LegacyEventType eventType,
        string inboxTitle,
        string inboxSummary) =>
        Result(true, scenario, assignment, null, Array.Empty<AlphaInboxItem>(), message);

    private static ScoutingOperationResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        ScoutingOperationAssignment? assignment,
        ScoutingReport? report,
        IReadOnlyList<AlphaInboxItem> inbox,
        string message)
    {
        var result = new ScoutingOperationResult(success, scenario, assignment, report, inbox, message);
        result.Validate();
        return result;
    }
}
