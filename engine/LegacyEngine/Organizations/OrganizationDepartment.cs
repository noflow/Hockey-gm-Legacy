using LegacyEngine.Staff;

namespace LegacyEngine.Organizations;

/// <summary>
/// A department within an organization. It may optionally map to a
/// <see cref="StaffDepartment"/> category from the Staff engine, but organizations
/// can also define departments that have no staff grouping.
/// </summary>
public sealed record OrganizationDepartment(
    string DepartmentId,
    string Name,
    StaffDepartment? Category = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DepartmentId))
        {
            throw new ArgumentException("Department id is required.", nameof(DepartmentId));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Department name is required.", nameof(Name));
        }
    }
}
