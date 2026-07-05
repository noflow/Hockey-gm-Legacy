namespace LegacyEngine.Staff;

/// <summary>
/// The staff aggregate: a profile plus attributes, an assignment history, and
/// accumulated performance reviews. All mutations are immutable <c>with</c> copies;
/// orchestration and Event Engine integration live in <see cref="StaffEngine"/>.
/// </summary>
public sealed record StaffMember(
    StaffProfile Profile,
    StaffAttributes Attributes,
    IReadOnlyList<StaffAssignment> Assignments,
    IReadOnlyList<StaffPerformance> PerformanceHistory)
{
    public string PersonId => Profile.PersonId;

    public string OrganizationId => Profile.OrganizationId;

    public StaffRole CurrentRole => Profile.CurrentRole;

    public StaffDepartment Department => Profile.Department;

    public StaffEmploymentStatus EmploymentStatus => Profile.EmploymentStatus;

    public string? ContractId => Profile.ContractId;

    public StaffAssignment? CurrentAssignment =>
        Assignments.SingleOrDefault(assignment => assignment.IsActive);

    public StaffMember WithProfile(StaffProfile profile) => this with { Profile = profile };

    public StaffMember WithEmploymentStatus(StaffEmploymentStatus status) =>
        this with { Profile = Profile with { EmploymentStatus = status } };

    public StaffMember WithAssignments(IReadOnlyList<StaffAssignment> assignments) =>
        this with { Assignments = assignments };

    public StaffMember AddPerformanceReview(StaffPerformance review)
    {
        review.Validate();

        if (review.PersonId != PersonId)
        {
            throw new ArgumentException("Performance review must belong to this staff member.", nameof(review));
        }

        if (PerformanceHistory.Any(existing => existing.ReviewId == review.ReviewId))
        {
            throw new ArgumentException("Performance review ids must be unique for a staff member.", nameof(review));
        }

        return this with
        {
            PerformanceHistory = PerformanceHistory
                .Append(review)
                .OrderBy(item => item.ReviewDate)
                .ThenBy(item => item.ReviewId, StringComparer.Ordinal)
                .ToArray()
        };
    }

    public void Validate()
    {
        Profile.Validate();
        Attributes.Validate();

        foreach (var assignment in Assignments)
        {
            assignment.Validate();

            if (assignment.PersonId != PersonId)
            {
                throw new ArgumentException("Every assignment must belong to this staff member.", nameof(Assignments));
            }
        }

        if (Assignments.Select(assignment => assignment.AssignmentId).Distinct().Count() != Assignments.Count)
        {
            throw new ArgumentException("Assignment ids must be unique.", nameof(Assignments));
        }

        if (Assignments.Count(assignment => assignment.IsActive) > 1)
        {
            throw new ArgumentException("A staff member cannot hold more than one active assignment.", nameof(Assignments));
        }

        foreach (var review in PerformanceHistory)
        {
            review.Validate();
        }

        if (PerformanceHistory.Select(review => review.ReviewId).Distinct().Count() != PerformanceHistory.Count)
        {
            throw new ArgumentException("Performance review ids must be unique.", nameof(PerformanceHistory));
        }
    }
}
