namespace LegacyEngine.Organizations;

/// <summary>
/// The shared organization foundation: identity, type, status, culture, reputation,
/// staff memberships, departments, and references to an owner, league, roster,
/// budgets, and facilities. All references are ids only; the Organization engine
/// never owns rosters, budgets, or facilities. Mutations are immutable
/// <c>with</c> copies; Event Engine integration lives in <see cref="OrganizationEngine"/>.
/// </summary>
public sealed record Organization(
    string OrganizationId,
    OrganizationType Type,
    OrganizationIdentity Identity,
    OrganizationStatus Status,
    string? OwnerPersonId,
    string? LeagueId,
    string? RosterId,
    OrganizationCulture Culture,
    OrganizationReputation Reputation,
    IReadOnlyList<OrganizationMembership> StaffMemberships,
    IReadOnlyList<OrganizationDepartment> Departments,
    IReadOnlyList<OrganizationBudgetReference> BudgetReferences,
    IReadOnlyList<OrganizationFacilityReference> FacilityReferences,
    string? ParentOrganizationId = null,
    string? AffiliateOrganizationId = null)
{
    public string Name => Identity.Name;

    public bool HasStaffMember(string personId) =>
        StaffMemberships.Any(membership => membership.PersonId == personId);

    public bool HasDepartment(string departmentId) =>
        Departments.Any(department => department.DepartmentId == departmentId);

    public Organization AddStaffMembership(OrganizationMembership membership)
    {
        membership.Validate();

        if (HasStaffMember(membership.PersonId))
        {
            throw new ArgumentException("Person is already a staff member of this organization.", nameof(membership));
        }

        return this with { StaffMemberships = StaffMemberships.Append(membership).ToArray() };
    }

    public Organization RemoveStaffMembership(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        if (!HasStaffMember(personId))
        {
            throw new ArgumentException("Person is not a staff member of this organization.", nameof(personId));
        }

        return this with { StaffMemberships = StaffMemberships.Where(membership => membership.PersonId != personId).ToArray() };
    }

    public Organization AddDepartment(OrganizationDepartment department)
    {
        department.Validate();

        if (HasDepartment(department.DepartmentId))
        {
            throw new ArgumentException("Department already exists in this organization.", nameof(department));
        }

        return this with { Departments = Departments.Append(department).ToArray() };
    }

    public Organization RemoveDepartment(string departmentId)
    {
        if (string.IsNullOrWhiteSpace(departmentId))
        {
            throw new ArgumentException("Department id is required.", nameof(departmentId));
        }

        if (!HasDepartment(departmentId))
        {
            throw new ArgumentException("Department does not exist in this organization.", nameof(departmentId));
        }

        return this with { Departments = Departments.Where(department => department.DepartmentId != departmentId).ToArray() };
    }

    public Organization ChangeStatus(OrganizationStatus status) => this with { Status = status };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (ParentOrganizationId == OrganizationId)
        {
            throw new ArgumentException("Organization cannot be its own parent organization.", nameof(ParentOrganizationId));
        }

        if (AffiliateOrganizationId == OrganizationId)
        {
            throw new ArgumentException("Organization cannot be its own affiliate organization.", nameof(AffiliateOrganizationId));
        }

        Identity.Validate();
        Culture.Validate();
        Reputation.Validate();

        foreach (var membership in StaffMemberships)
        {
            membership.Validate();
        }

        if (StaffMemberships.Select(membership => membership.PersonId).Distinct().Count() != StaffMemberships.Count)
        {
            throw new ArgumentException("Staff membership person ids must be unique.", nameof(StaffMemberships));
        }

        foreach (var department in Departments)
        {
            department.Validate();
        }

        if (Departments.Select(department => department.DepartmentId).Distinct().Count() != Departments.Count)
        {
            throw new ArgumentException("Department ids must be unique.", nameof(Departments));
        }

        foreach (var budgetReference in BudgetReferences)
        {
            budgetReference.Validate();
        }

        foreach (var facilityReference in FacilityReferences)
        {
            facilityReference.Validate();
        }
    }
}
