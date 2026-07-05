using LegacyEngine.Events;

namespace LegacyEngine.Staff;

/// <summary>
/// The v1 Coaches &amp; Staff engine. It creates staff members, hires them, assigns
/// and reassigns roles, removes them, records performance reviews, and produces
/// evaluations. Every state change that matters flows through the Event Engine.
///
/// Out of scope for v1: firing logic, contract negotiation, roster interaction,
/// relationship changes, practices, tactics, morale, and any gameplay simulation.
/// </summary>
public sealed class StaffEngine
{
    private readonly EventEngine _eventEngine;

    public StaffEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public static StaffDepartment DepartmentFor(StaffRole role) => StaffRoles.DepartmentFor(role);

    /// <summary>Builds a staff member. Created members are Prospective until hired; no event is raised.</summary>
    public StaffMember CreateStaffMember(
        string personId,
        string organizationId,
        StaffRole role,
        int yearsExperience = 0,
        int reputation = 50,
        StaffAttributes? attributes = null,
        string? contractId = null)
    {
        var profile = new StaffProfile(
            PersonId: personId,
            OrganizationId: organizationId,
            CurrentRole: role,
            Department: StaffRoles.DepartmentFor(role),
            YearsExperience: yearsExperience,
            Reputation: reputation,
            ContractId: contractId,
            EmploymentStatus: StaffEmploymentStatus.Prospective);

        var member = new StaffMember(
            profile,
            attributes ?? StaffAttributes.Empty,
            Array.Empty<StaffAssignment>(),
            Array.Empty<StaffPerformance>());

        member.Validate();
        return member;
    }

    /// <summary>Hires a staff member into the organization and raises a StaffHired event.</summary>
    public StaffMember Hire(StaffMember member, DateOnly hireDate, StaffContractReference? contract = null)
    {
        member.Validate();
        contract?.Validate();

        if (member.EmploymentStatus == StaffEmploymentStatus.Employed)
        {
            throw new InvalidOperationException("Staff member is already employed.");
        }

        var profile = member.Profile with { EmploymentStatus = StaffEmploymentStatus.Employed };
        if (contract is not null)
        {
            profile = profile with { ContractId = contract.ContractId };
        }

        var hired = member.WithProfile(profile);
        hired.Validate();

        QueueStaffEvent(
            hired,
            LegacyEventType.StaffHired,
            hireDate,
            "Staff member hired",
            $"{StaffRoles.Title(hired.CurrentRole)} was hired.");

        return hired;
    }

    /// <summary>Assigns a role to a staff member with no active assignment and raises a StaffAssigned event.</summary>
    public StaffMember AssignRole(
        StaffMember member,
        StaffRole role,
        DateOnly effectiveDate,
        string? assignmentId = null)
    {
        member.Validate();

        if (member.CurrentAssignment is not null)
        {
            throw new InvalidOperationException("Staff member already has an active assignment; use ReassignRole.");
        }

        var assignment = BuildAssignment(member, role, effectiveDate, assignmentId);
        var assigned = ApplyAssignment(member, assignment);

        QueueStaffEvent(
            assigned,
            LegacyEventType.StaffAssigned,
            effectiveDate,
            "Staff member assigned",
            $"Assigned as {StaffRoles.Title(role)}.");

        return assigned;
    }

    /// <summary>Ends the current assignment, opens a new one, and raises a StaffReassigned event.</summary>
    public StaffMember ReassignRole(
        StaffMember member,
        StaffRole newRole,
        DateOnly effectiveDate,
        string? assignmentId = null)
    {
        member.Validate();

        var current = member.CurrentAssignment
            ?? throw new InvalidOperationException("Staff member has no active assignment to reassign; use AssignRole.");

        var previousRole = current.Role;
        var closed = CloseActiveAssignment(member, effectiveDate);
        var assignment = BuildAssignment(closed, newRole, effectiveDate, assignmentId);
        var reassigned = ApplyAssignment(closed, assignment);

        QueueStaffEvent(
            reassigned,
            LegacyEventType.StaffReassigned,
            effectiveDate,
            "Staff member reassigned",
            $"Reassigned from {StaffRoles.Title(previousRole)} to {StaffRoles.Title(newRole)}.");

        return reassigned;
    }

    /// <summary>
    /// Removes a staff member from the organization, ending any active assignment and
    /// raising a StaffReleased event. This is an administrative removal, not
    /// evaluation-driven firing (which is intentionally out of scope for v1).
    /// </summary>
    public StaffMember RemoveStaffMember(StaffMember member, DateOnly effectiveDate, string? reason = null)
    {
        member.Validate();

        if (member.EmploymentStatus == StaffEmploymentStatus.Released)
        {
            throw new InvalidOperationException("Staff member has already been released.");
        }

        var closed = member.CurrentAssignment is null ? member : CloseActiveAssignment(member, effectiveDate);
        var released = closed.WithEmploymentStatus(StaffEmploymentStatus.Released);
        released.Validate();

        QueueStaffEvent(
            released,
            LegacyEventType.StaffReleased,
            effectiveDate,
            "Staff member released",
            reason is null ? "Staff member was released." : $"Staff member was released: {reason}");

        return released;
    }

    /// <summary>Appends a performance review to the staff member. No event is raised for a review.</summary>
    public StaffMember RecordPerformanceReview(StaffMember member, StaffPerformance review)
    {
        member.Validate();
        return member.AddPerformanceReview(review);
    }

    /// <summary>Produces an evaluation report and raises a StaffEvaluated event.</summary>
    public StaffEvaluation EvaluateStaff(StaffMember member, DateOnly evaluatedOn)
    {
        member.Validate();

        var relevant = RelevantAttributes(member);
        var overall = CalculateOverall(member, relevant);
        var strengths = relevant.Where(item => item.Value >= 75).Select(item => item.Label).ToArray();
        var weaknesses = relevant.Where(item => item.Value <= 40).Select(item => item.Label).ToArray();
        var recommendation = RecommendationFor(overall);
        var suggestions = BuildDevelopmentSuggestions(weaknesses);

        var evaluation = new StaffEvaluation(
            PersonId: member.PersonId,
            OrganizationId: member.OrganizationId,
            Role: member.CurrentRole,
            EvaluatedOn: evaluatedOn,
            OverallEvaluation: overall,
            Recommendation: recommendation,
            Strengths: strengths,
            Weaknesses: weaknesses,
            DevelopmentSuggestions: suggestions,
            Summary: $"{StaffRoles.Title(member.CurrentRole)} evaluation: overall {overall}/100; recommendation {recommendation}.");

        evaluation.Validate();

        QueueStaffEvent(
            member,
            LegacyEventType.StaffEvaluated,
            evaluatedOn,
            "Staff member evaluated",
            evaluation.Summary,
            new Dictionary<string, object?>
            {
                ["overall_evaluation"] = overall,
                ["recommendation"] = recommendation.ToString(),
                ["strength_count"] = strengths.Length,
                ["weakness_count"] = weaknesses.Length
            });

        return evaluation;
    }

    private static StaffAssignment BuildAssignment(
        StaffMember member,
        StaffRole role,
        DateOnly effectiveDate,
        string? assignmentId) =>
        new(
            AssignmentId: assignmentId ?? CreateAssignmentId(),
            PersonId: member.PersonId,
            OrganizationId: member.OrganizationId,
            Role: role,
            Department: StaffRoles.DepartmentFor(role),
            StartDate: effectiveDate);

    private static StaffMember ApplyAssignment(StaffMember member, StaffAssignment assignment)
    {
        assignment.Validate();

        var profile = member.Profile with
        {
            CurrentRole = assignment.Role,
            Department = assignment.Department
        };

        var updated = member
            .WithProfile(profile)
            .WithAssignments(member.Assignments.Append(assignment).ToArray());

        updated.Validate();
        return updated;
    }

    private static StaffMember CloseActiveAssignment(StaffMember member, DateOnly effectiveDate)
    {
        var current = member.CurrentAssignment;
        if (current is null)
        {
            return member;
        }

        var assignments = member.Assignments
            .Select(assignment => assignment.AssignmentId == current.AssignmentId ? current.End(effectiveDate) : assignment)
            .ToArray();

        return member.WithAssignments(assignments);
    }

    private static int CalculateOverall(StaffMember member, IReadOnlyList<(string Label, int Value)> relevant)
    {
        var baseScore = relevant.Count > 0
            ? (int)Math.Round(relevant.Average(item => item.Value))
            : member.Profile.Reputation;

        var recentReview = member.PerformanceHistory
            .OrderBy(review => review.ReviewDate)
            .ThenBy(review => review.ReviewId, StringComparer.Ordinal)
            .LastOrDefault();

        var overall = recentReview is null
            ? baseScore
            : (int)Math.Round((baseScore + recentReview.Rating) / 2.0);

        return Math.Clamp(overall, 0, 100);
    }

    private static IReadOnlyList<(string Label, int Value)> RelevantAttributes(StaffMember member) =>
        member.Department switch
        {
            StaffDepartment.Coaching => member.Attributes.CoachingAttributes
                .Select(pair => (pair.Key.ToString(), pair.Value))
                .ToArray(),
            StaffDepartment.Scouting => member.Attributes.ScoutingAttributes
                .Select(pair => (pair.Key.ToString(), pair.Value))
                .ToArray(),
            StaffDepartment.Medical => member.Attributes.MedicalAttributes
                .Select(pair => (pair.Key.ToString(), pair.Value))
                .ToArray(),
            _ => Array.Empty<(string, int)>()
        };

    private static StaffRecommendation RecommendationFor(int overall) => overall switch
    {
        >= 80 => StaffRecommendation.Promote,
        >= 60 => StaffRecommendation.Retain,
        >= 45 => StaffRecommendation.Develop,
        _ => StaffRecommendation.Monitor
    };

    private static IReadOnlyList<string> BuildDevelopmentSuggestions(IReadOnlyList<string> weaknesses) =>
        weaknesses.Count == 0
            ? new[] { "Maintain current strengths and continue the existing development plan." }
            : weaknesses.Select(label => $"Develop {label} through targeted coaching and mentorship.").ToArray();

    private LegacyEvent QueueStaffEvent(
        StaffMember member,
        LegacyEventType eventType,
        DateOnly date,
        string title,
        string description,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var details = metadata ?? new Dictionary<string, object?>();
        var merged = new Dictionary<string, object?>(details)
        {
            ["person_id"] = member.PersonId,
            ["organization_id"] = member.OrganizationId,
            ["role"] = member.CurrentRole.ToString(),
            ["department"] = member.Department.ToString(),
            ["employment_status"] = member.EmploymentStatus.ToString()
        };

        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: member.PersonId,
                OrganizationId: member.OrganizationId),
            merged);

        return _eventEngine.QueueEvent(legacyEvent);
    }

    private static string CreateAssignmentId() => $"staff-assignment-{Guid.NewGuid():N}";
}
