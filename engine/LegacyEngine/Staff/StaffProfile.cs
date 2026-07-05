namespace LegacyEngine.Staff;

/// <summary>
/// The descriptive record for a staff member: who they are, where they work, their
/// current role and department, experience, reputation, contract reference, and
/// employment status. The <see cref="ContractId"/> is a reference only.
/// </summary>
public sealed record StaffProfile(
    string PersonId,
    string OrganizationId,
    StaffRole CurrentRole,
    StaffDepartment Department,
    int YearsExperience,
    int Reputation,
    string? ContractId,
    StaffEmploymentStatus EmploymentStatus)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (Department != StaffRoles.DepartmentFor(CurrentRole))
        {
            throw new ArgumentException("Profile department must match the current role's department.", nameof(Department));
        }

        if (YearsExperience < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(YearsExperience), "Years of experience cannot be negative.");
        }

        if (Reputation is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Reputation), "Reputation must be between 0 and 100.");
        }
    }
}
