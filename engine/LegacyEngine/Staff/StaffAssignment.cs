namespace LegacyEngine.Staff;

/// <summary>
/// A single posting of a staff member to a role within a department. Reassignments
/// end the current assignment and open a new one, forming an assignment history.
/// </summary>
public sealed record StaffAssignment(
    string AssignmentId,
    string PersonId,
    string OrganizationId,
    StaffRole Role,
    StaffDepartment Department,
    DateOnly StartDate,
    DateOnly? EndDate = null)
{
    public bool IsActive => EndDate is null;

    public bool IsActiveOn(DateOnly date) =>
        StartDate <= date && (!EndDate.HasValue || EndDate.Value >= date);

    public StaffAssignment End(DateOnly endDate)
    {
        if (endDate < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "Assignment end date cannot be before start date.");
        }

        return this with { EndDate = endDate };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AssignmentId))
        {
            throw new ArgumentException("Assignment id is required.", nameof(AssignmentId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (Department != StaffRoles.DepartmentFor(Role))
        {
            throw new ArgumentException("Assignment department must match the role's department.", nameof(Department));
        }

        if (EndDate.HasValue && EndDate.Value < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(EndDate), "Assignment end date cannot be before start date.");
        }
    }
}
