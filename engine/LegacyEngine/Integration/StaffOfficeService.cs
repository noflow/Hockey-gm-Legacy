using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class StaffOfficeService
{
    public IReadOnlyList<StaffOfficeProfile> BuildStaffProfiles(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers
            .Where(member => member.EmploymentStatus == StaffEmploymentStatus.Employed)
            .OrderBy(member => member.Department)
            .ThenBy(member => member.CurrentRole)
            .Select(member => BuildProfile(scenario, member))
            .ToArray();

    public StaffOfficeResult GenerateCandidatePool(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (scenario.StaffCandidates.Count > 0)
        {
            return Result(true, scenario, scenario.StaffCandidates.First(), null, null, null, Array.Empty<AlphaInboxItem>(), "Staff candidate pool already exists.");
        }

        var candidates = new[]
        {
            BuildCandidate(registry, scenario, "candidate-staff-001", "Mara", "Voss", StaffRole.DevelopmentCoach, 73, 76, 61, new[] { "player development", "communication" }, new[] { "limited head-coach experience" }, "Collaborative teacher with strong patience.", "Low chemistry risk; likely aligns with a development-first GM."),
            BuildCandidate(registry, scenario, "candidate-staff-002", "Owen", "Leclerc", StaffRole.AthleticTherapist, 68, 71, 56, new[] { "injury prevention", "recovery planning" }, new[] { "modest hockey operations background" }, "Calm medical communicator with practical habits.", "Low to moderate risk; needs clear role boundaries."),
            BuildCandidate(registry, scenario, "candidate-staff-003", "Priya", "Nandakumar", StaffRole.Scout, 78, 74, 58, new[] { "character reads", "regional coverage" }, new[] { "smaller network in Europe" }, "Detail-heavy scout who values evidence.", "Moderate risk; may push back if assignments are vague.")
        };

        var updatedPeople = scenario.AlphaSnapshot.People
            .Concat(candidates.Select(candidate => candidate.Person))
            .GroupBy(person => person.PersonId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        var updated = scenario with
        {
            StaffCandidates = candidates,
            AlphaSnapshot = scenario.AlphaSnapshot with { People = updatedPeople }
        };

        foreach (var candidate in candidates)
        {
            QueueEvent(registry, updated, LegacyEventType.StaffCandidateGenerated, "Staff candidate generated", $"{candidate.Person.Identity.DisplayName} is available for {StaffRoles.Title(candidate.StaffMember.CurrentRole)}.", candidate.Person.PersonId);
        }

        var recommended = candidates.OrderByDescending(candidate => candidate.RoleFit + candidate.DepartmentFit + candidate.Reputation).First();
        var inbox = new[]
        {
            Inbox(updated, LegacyEventType.StaffCandidateGenerated, "Recommended staff hire", $"{recommended.Person.Identity.DisplayName} is the strongest current candidate: {recommended.HiringRecommendation}", recommended.Person.PersonId)
        };

        return Result(true, updated, recommended, null, null, null, inbox, $"Generated {candidates.Length} staff candidate(s).");
    }

    public StaffOfficeResult HireCandidate(EngineRegistry registry, NewGmScenarioSnapshot scenario, string candidateId)
    {
        var candidate = scenario.StaffCandidates.SingleOrDefault(candidate => candidate.CandidateId == candidateId)
            ?? throw new ArgumentException("Staff candidate was not found.", nameof(candidateId));
        var role = candidate.StaffMember.CurrentRole;
        var hired = registry.StaffEngine.AssignRole(registry.StaffEngine.Hire(candidate.StaffMember, scenario.CurrentDate), role, scenario.CurrentDate);
        var people = scenario.AlphaSnapshot.People
            .Concat(new[] { candidate.Person })
            .GroupBy(person => person.PersonId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        var updated = scenario with
        {
            StaffCandidates = scenario.StaffCandidates.Where(item => item.CandidateId != candidateId).ToArray(),
            StaffMembers = scenario.StaffMembers.Append(hired).ToArray(),
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                People = people,
                StaffMembers = scenario.AlphaSnapshot.StaffMembers.Append(hired).ToArray()
            }
        };

        QueueEvent(registry, updated, LegacyEventType.StaffHired, "Staff hired", $"{candidate.Person.Identity.DisplayName} was hired as {StaffRoles.Title(role)}.", candidate.Person.PersonId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffHired, "Staff hired", $"{candidate.Person.Identity.DisplayName} joined the front office as {StaffRoles.Title(role)}.", candidate.Person.PersonId) };
        return Result(true, updated, candidate, null, null, null, inbox, $"{candidate.Person.Identity.DisplayName} hired as {StaffRoles.Title(role)}.");
    }

    public StaffOfficeResult ReassignStaffRole(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, StaffRole newRole)
    {
        var member = FindStaff(scenario, personId);
        var updatedMember = member.CurrentAssignment is null
            ? registry.StaffEngine.AssignRole(member, newRole, scenario.CurrentDate)
            : registry.StaffEngine.ReassignRole(member, newRole, scenario.CurrentDate);
        var updated = ReplaceStaff(scenario, updatedMember);

        QueueEvent(registry, updated, LegacyEventType.StaffRoleChanged, "Staff role changed", $"{Name(updated, personId)} was reassigned to {StaffRoles.Title(newRole)}.", personId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffRoleChanged, "Staff role changed", $"{Name(updated, personId)} is now {StaffRoles.Title(newRole)}.", personId) };
        return Result(true, updated, null, null, null, null, inbox, $"{Name(updated, personId)} reassigned to {StaffRoles.Title(newRole)}.");
    }

    public StaffOfficeResult ReleaseStaff(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, string reason)
    {
        var member = FindStaff(scenario, personId);
        var released = registry.StaffEngine.RemoveStaffMember(member, scenario.CurrentDate, reason);
        var updated = ReplaceStaff(scenario, released);

        QueueEvent(registry, updated, LegacyEventType.StaffReleased, "Staff released", $"{Name(updated, personId)} was released. Reason: {reason}", personId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffReleased, "Staff released", $"{Name(updated, personId)} was released. {reason}", personId) };
        return Result(true, updated, null, null, null, null, inbox, $"{Name(updated, personId)} released.");
    }

    public StaffOfficeResult SetDevelopmentCoachFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, DevelopmentCoachFocus focus)
    {
        var member = FindStaff(scenario, personId);
        if (member.Department != StaffDepartment.Coaching)
        {
            return Result(false, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "Development coach focus requires a coaching staff member.");
        }

        return SetFocus(registry, scenario, member, focus.ToString(), $"Development focus set to {focus}.");
    }

    public StaffOfficeResult SetMedicalStaffFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, MedicalStaffFocus focus)
    {
        var member = FindStaff(scenario, personId);
        if (member.Department != StaffDepartment.Medical)
        {
            return Result(false, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "Medical focus requires a medical staff member.");
        }

        return SetFocus(registry, scenario, member, focus.ToString(), $"Medical focus set to {focus}.");
    }

    public StaffOfficeResult SetScoutingDepartmentFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, ScoutingDepartmentFocus focus)
    {
        var member = FindStaff(scenario, personId);
        if (member.Department != StaffDepartment.Scouting)
        {
            return Result(false, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "Scouting department focus requires a scouting staff member.");
        }

        return SetFocus(registry, scenario, member, focus.ToString(), $"Scouting focus set to {focus}. Scouting Operations assignments remain compatible.");
    }

    public StaffOfficeResult GenerateStaffEvaluation(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var member = FindStaff(scenario, personId);
        var evaluation = registry.StaffEngine.EvaluateStaff(member, scenario.CurrentDate);
        var updatedMember = member.AddPerformanceReview(new StaffPerformance(
            ReviewId: $"staff-performance:{personId}:{scenario.CurrentDate:yyyyMMdd}:{member.PerformanceHistory.Count + 1}",
            PersonId: personId,
            OrganizationId: scenario.Organization.OrganizationId,
            ReviewDate: scenario.CurrentDate,
            Rating: evaluation.OverallEvaluation,
            Summary: evaluation.Summary,
            Metrics: new Dictionary<string, object?>
            {
                ["recommendation"] = evaluation.Recommendation.ToString(),
                ["strength_count"] = evaluation.Strengths.Count,
                ["weakness_count"] = evaluation.Weaknesses.Count
            }));
        var updated = ReplaceStaff(scenario, updatedMember) with
        {
            StaffEvaluations = scenario.StaffEvaluations.Append(evaluation).ToArray()
        };

        QueueEvent(registry, updated, LegacyEventType.StaffEvaluationCreated, "Staff evaluation created", evaluation.Summary, personId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffEvaluationCreated, "Staff evaluation", $"{Name(updated, personId)}: {evaluation.Summary}", personId) };
        return Result(true, updated, null, null, evaluation, null, inbox, evaluation.Summary);
    }

    public StaffOfficeResult GenerateChemistryWarning(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var reports = BuildStaffProfiles(scenario).Select(profile => profile.Chemistry).ToArray();
        var warning = reports
            .OrderBy(report => report.GmFit)
            .FirstOrDefault(report => report.ConflictWarnings.Count > 0);

        if (warning is null)
        {
            return Result(true, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "No staff chemistry warning generated.");
        }

        QueueEvent(registry, scenario, LegacyEventType.StaffConflictWarning, "Staff conflict warning", warning.Summary, warning.PersonId);
        var inbox = new[] { Inbox(scenario, LegacyEventType.StaffConflictWarning, "Staff chemistry warning", warning.Summary, warning.PersonId, LegacyEventSeverity.Warning) };
        return Result(true, scenario, null, null, null, warning, inbox, warning.Summary);
    }

    public StaffChemistryReport EvaluateChemistry(NewGmScenarioSnapshot scenario, string personId)
    {
        var member = FindStaff(scenario, personId);
        var name = Name(scenario, personId);
        var gmFit = RelationshipWithGm(scenario, personId);
        var departmentFit = DepartmentFit(member);
        var warnings = new List<string>();
        if (gmFit < 45)
        {
            warnings.Add("GM relationship is strained; communication quality may suffer.");
        }

        if (departmentFit < 45)
        {
            warnings.Add("Department fit is thin for the current role.");
        }

        var partnerships = scenario.StaffMembers
            .Where(other => other.PersonId != personId && other.Department == member.Department && other.EmploymentStatus == StaffEmploymentStatus.Employed)
            .Select(other => Name(scenario, other.PersonId))
            .Take(3)
            .ToArray();

        if (partnerships.Length == 0 && gmFit >= 60 && departmentFit >= 60)
        {
            partnerships = new[] { "Strong partnership potential with the GM." };
        }

        var summary = warnings.Count > 0
            ? $"{name}: {string.Join(" ", warnings)}"
            : $"{name}: staff chemistry is stable; GM fit {Label(gmFit)} and department fit {Label(departmentFit)}.";

        var report = new StaffChemistryReport(personId, name, gmFit, departmentFit, warnings, partnerships, summary);
        report.Validate();
        return report;
    }

    private StaffOfficeProfile BuildProfile(NewGmScenarioSnapshot scenario, StaffMember member)
    {
        var focus = scenario.StaffFocusAssignments
            .Where(item => item.PersonId == member.PersonId)
            .OrderBy(item => item.SetOn)
            .ThenBy(item => item.FocusId, StringComparer.Ordinal)
            .LastOrDefault();
        var contract = member.ContractId is null
            ? "No contract reference"
            : scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
                .Where(contract => contract.ContractId == member.ContractId)
                .Select(contract => $"{contract.Status}, {contract.Term.StartDate:yyyy-MM-dd} to {contract.Term.EndDate:yyyy-MM-dd}")
                .DefaultIfEmpty($"Reference {member.ContractId}")
                .First();

        var profile = new StaffOfficeProfile(
            PersonId: member.PersonId,
            Name: Name(scenario, member.PersonId),
            CurrentRole: member.CurrentRole,
            Department: member.Department,
            ContractStatus: contract,
            Strengths: Strengths(member),
            Weaknesses: Weaknesses(member),
            RelationshipWithGm: RelationshipWithGm(scenario, member.PersonId),
            Chemistry: EvaluateChemistry(scenario, member.PersonId),
            CurrentAssignment: member.CurrentAssignment is null
                ? "No active assignment"
                : $"{StaffRoles.Title(member.CurrentAssignment.Role)} since {member.CurrentAssignment.StartDate:yyyy-MM-dd}",
            CurrentFocus: focus is null ? "No focus set" : $"{focus.Focus} ({focus.SetOn:yyyy-MM-dd})");
        profile.Validate();
        return profile;
    }

    private StaffOfficeResult SetFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, StaffMember member, string focus, string notes)
    {
        var assignment = new StaffFocusAssignment(
            FocusId: $"staff-focus:{member.PersonId}:{scenario.CurrentDate:yyyyMMdd}:{scenario.StaffFocusAssignments.Count + 1}",
            PersonId: member.PersonId,
            Department: member.Department,
            Focus: focus,
            SetOn: scenario.CurrentDate,
            Notes: notes);
        assignment.Validate();

        var updated = scenario with
        {
            StaffFocusAssignments = scenario.StaffFocusAssignments
                .Where(item => item.PersonId != member.PersonId || item.Department != member.Department)
                .Append(assignment)
                .ToArray()
        };

        QueueEvent(registry, updated, LegacyEventType.StaffFocusChanged, "Staff focus changed", $"{Name(updated, member.PersonId)}: {notes}", member.PersonId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffFocusChanged, "Staff focus changed", $"{Name(updated, member.PersonId)}: {notes}", member.PersonId) };
        return Result(true, updated, null, assignment, null, null, inbox, $"{Name(updated, member.PersonId)} focus updated.");
    }

    private static StaffCandidate BuildCandidate(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string candidateId,
        string firstName,
        string lastName,
        StaffRole role,
        int roleFit,
        int departmentFit,
        int reputation,
        IReadOnlyList<string> strengths,
        IReadOnlyList<string> weaknesses,
        string personality,
        string chemistryRisk)
    {
        var person = new Person(
            PersonId: $"person-{candidateId}",
            Identity: new PersonIdentity(firstName, lastName, Gender.NonBinary, new DateOnly(1984 + (reputation % 9), 5, 10), "Canada", "Winnipeg, MB"),
            Status: PersonStatus.Active,
            Roles: Array.Empty<PersonRole>(),
            Reputation: new PersonReputation(reputation, Math.Max(20, reputation - 8), Math.Max(10, reputation - 20)),
            Personality: new PersonalityProfile(Ambition: 62, Loyalty: 66, Temperament: 58, Adaptability: 70, Professionalism: 72),
            CareerTimeline: Array.Empty<CareerTimelineEntry>());

        var attributes = AttributesFor(role, roleFit, departmentFit);
        var member = registry.StaffEngine.CreateStaffMember(
            person.PersonId,
            scenario.Organization.OrganizationId,
            role,
            yearsExperience: Math.Max(2, reputation / 7),
            reputation: reputation,
            attributes: attributes);
        var recommendation = roleFit + departmentFit + reputation >= 205
            ? "Recommended hire for the current staff plan."
            : "Useful candidate, but fit should be reviewed before hiring.";
        var candidate = new StaffCandidate(candidateId, person, member, roleFit, departmentFit, reputation, strengths, weaknesses, personality, chemistryRisk, recommendation);
        candidate.Validate();
        return candidate;
    }

    private static StaffAttributes AttributesFor(StaffRole role, int roleFit, int departmentFit) =>
        StaffRoles.DepartmentFor(role) switch
        {
            StaffDepartment.Coaching => StaffAttributes.ForCoaching(new Dictionary<StaffCoachingAttribute, int>
            {
                [StaffCoachingAttribute.Development] = roleFit,
                [StaffCoachingAttribute.Teaching] = departmentFit,
                [StaffCoachingAttribute.Communication] = Math.Max(45, (roleFit + departmentFit) / 2)
            }),
            StaffDepartment.Medical => StaffAttributes.ForMedical(new Dictionary<StaffMedicalAttribute, int>
            {
                [StaffMedicalAttribute.InjuryPrevention] = roleFit,
                [StaffMedicalAttribute.Rehabilitation] = departmentFit,
                [StaffMedicalAttribute.Diagnosis] = Math.Max(45, (roleFit + departmentFit) / 2)
            }),
            StaffDepartment.Scouting => StaffAttributes.ForScouting(new Dictionary<StaffScoutingAttribute, int>
            {
                [StaffScoutingAttribute.TalentEvaluation] = roleFit,
                [StaffScoutingAttribute.CharacterEvaluation] = departmentFit,
                [StaffScoutingAttribute.RegionalKnowledge] = Math.Max(45, (roleFit + departmentFit) / 2)
            }),
            _ => StaffAttributes.Empty
        };

    private static StaffMember FindStaff(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.StaffMembers.SingleOrDefault(member => member.PersonId == personId)
        ?? throw new ArgumentException("Staff member was not found.", nameof(personId));

    private static NewGmScenarioSnapshot ReplaceStaff(NewGmScenarioSnapshot scenario, StaffMember member)
    {
        var staff = scenario.StaffMembers
            .Select(existing => existing.PersonId == member.PersonId ? member : existing)
            .ToArray();
        var snapshotStaff = scenario.AlphaSnapshot.StaffMembers
            .Select(existing => existing.PersonId == member.PersonId ? member : existing)
            .ToArray();

        return scenario with
        {
            StaffMembers = staff,
            AlphaSnapshot = scenario.AlphaSnapshot with { StaffMembers = snapshotStaff }
        };
    }

    private static IReadOnlyList<string> Strengths(StaffMember member)
    {
        var values = RelevantValues(member).OrderByDescending(item => item.Value).Take(2).Select(item => item.Label).ToArray();
        return values.Length == 0 ? new[] { "professional habits" } : values;
    }

    private static IReadOnlyList<string> Weaknesses(StaffMember member)
    {
        var values = RelevantValues(member).OrderBy(item => item.Value).Take(2).Select(item => item.Label).ToArray();
        return values.Length == 0 ? new[] { "fit still being evaluated" } : values;
    }

    private static int DepartmentFit(StaffMember member)
    {
        var values = RelevantValues(member).ToArray();
        return values.Length == 0 ? member.Profile.Reputation : Math.Clamp((int)Math.Round(values.Average(item => item.Value)), 0, 100);
    }

    private static IEnumerable<(string Label, int Value)> RelevantValues(StaffMember member) =>
        member.Department switch
        {
            StaffDepartment.Coaching => member.Attributes.CoachingAttributes.Select(pair => (pair.Key.ToString(), pair.Value)),
            StaffDepartment.Scouting => member.Attributes.ScoutingAttributes.Select(pair => (pair.Key.ToString(), pair.Value)),
            StaffDepartment.Medical => member.Attributes.MedicalAttributes.Select(pair => (pair.Key.ToString(), pair.Value)),
            _ => Array.Empty<(string, int)>()
        };

    private static int RelationshipWithGm(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == scenario.AlphaSnapshot.GeneralManager.PersonId && relationship.ToPersonId == personId)
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .DefaultIfEmpty(50)
            .First();

    private static string Name(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;

    private static string Label(int value) =>
        value >= 70 ? "strong" :
        value >= 55 ? "positive" :
        value >= 40 ? "mixed" :
        "strained";

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string description, string? primaryPersonId)
    {
        var date = scenario.CurrentDate;
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 10, 0, 0, TimeSpan.Zero),
            eventType,
            eventType == LegacyEventType.StaffConflictWarning ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(primaryPersonId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_2_1_staff_control" });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, string? primaryPersonId, LegacyEventSeverity severity = LegacyEventSeverity.Notice) =>
        new(
            InboxItemId: $"inbox:staff-control:{Guid.NewGuid():N}",
            Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 10, 30, 0, TimeSpan.Zero),
            EventType: eventType,
            Severity: severity,
            Title: title,
            Summary: summary,
            PrimaryPersonId: primaryPersonId);

    private static StaffOfficeResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        StaffCandidate? candidate,
        StaffFocusAssignment? focus,
        StaffEvaluation? evaluation,
        StaffChemistryReport? chemistry,
        IReadOnlyList<AlphaInboxItem> inbox,
        string message)
    {
        var result = new StaffOfficeResult(success, scenario, candidate, focus, evaluation, chemistry, inbox, message);
        result.Validate();
        return result;
    }
}
