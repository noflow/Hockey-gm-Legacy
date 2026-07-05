using LegacyEngine.Staff;

namespace LegacyEngine.Organizations;

/// <summary>
/// A person's membership in an organization's staff group. It references a Person id
/// only; the Staff engine owns the full staff member. An optional <see cref="StaffRole"/>
/// and department id record where the person sits within the organization.
/// </summary>
public sealed record OrganizationMembership(
    string PersonId,
    StaffRole? Role = null,
    string? DepartmentId = null,
    DateOnly? JoinedOn = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }
    }
}
