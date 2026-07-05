using LegacyEngine.Events;

namespace LegacyEngine.Organizations;

/// <summary>
/// The v1 Organization engine. It creates organizations and maintains their staff
/// memberships, departments, and status. Every meaningful change flows through the
/// Event Engine.
///
/// Out of scope for v1: facilities, finance, schedule generation, standings, game
/// simulation, save/load, UI, and any game-client integration. Budgets and
/// facilities are referenced by id only.
/// </summary>
public sealed class OrganizationEngine
{
    private readonly EventEngine _eventEngine;

    public OrganizationEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public Organization CreateOrganization(
        string organizationId,
        OrganizationType type,
        OrganizationIdentity identity,
        DateOnly foundedOn,
        string? ownerPersonId = null,
        string? leagueId = null,
        string? rosterId = null,
        OrganizationCulture? culture = null,
        OrganizationReputation? reputation = null,
        IEnumerable<OrganizationMembership>? staffMemberships = null,
        IEnumerable<OrganizationDepartment>? departments = null,
        IEnumerable<OrganizationBudgetReference>? budgetReferences = null,
        IEnumerable<OrganizationFacilityReference>? facilityReferences = null)
    {
        var organization = new Organization(
            OrganizationId: organizationId,
            Type: type,
            Identity: identity,
            Status: OrganizationStatus.Active,
            OwnerPersonId: ownerPersonId,
            LeagueId: leagueId,
            RosterId: rosterId,
            Culture: culture ?? OrganizationCulture.Balanced,
            Reputation: reputation ?? OrganizationReputation.Neutral,
            StaffMemberships: staffMemberships?.ToArray() ?? Array.Empty<OrganizationMembership>(),
            Departments: departments?.ToArray() ?? Array.Empty<OrganizationDepartment>(),
            BudgetReferences: budgetReferences?.ToArray() ?? Array.Empty<OrganizationBudgetReference>(),
            FacilityReferences: facilityReferences?.ToArray() ?? Array.Empty<OrganizationFacilityReference>());

        organization.Validate();

        QueueOrganizationEvent(
            organization,
            LegacyEventType.OrganizationCreated,
            foundedOn,
            "Organization created",
            $"{organization.Name} was created.",
            organization.OwnerPersonId);

        return organization;
    }

    public Organization AddStaff(Organization organization, OrganizationMembership membership, DateOnly date)
    {
        organization.Validate();
        var updated = organization.AddStaffMembership(membership);

        QueueOrganizationEvent(
            updated,
            LegacyEventType.OrganizationStaffAdded,
            date,
            "Staff added to organization",
            $"A staff member joined {updated.Name}.",
            membership.PersonId,
            new Dictionary<string, object?>
            {
                ["staff_person_id"] = membership.PersonId,
                ["staff_role"] = membership.Role?.ToString(),
                ["department_id"] = membership.DepartmentId
            });

        return updated;
    }

    public Organization RemoveStaff(Organization organization, string personId, DateOnly date)
    {
        organization.Validate();
        var updated = organization.RemoveStaffMembership(personId);

        QueueOrganizationEvent(
            updated,
            LegacyEventType.OrganizationStaffRemoved,
            date,
            "Staff removed from organization",
            $"A staff member left {updated.Name}.",
            personId,
            new Dictionary<string, object?>
            {
                ["staff_person_id"] = personId
            });

        return updated;
    }

    public Organization AddDepartment(Organization organization, OrganizationDepartment department, DateOnly date)
    {
        organization.Validate();
        var updated = organization.AddDepartment(department);

        QueueOrganizationEvent(
            updated,
            LegacyEventType.OrganizationDepartmentAdded,
            date,
            "Department added",
            $"Department '{department.Name}' was added to {updated.Name}.",
            metadata: new Dictionary<string, object?>
            {
                ["department_id"] = department.DepartmentId,
                ["department_name"] = department.Name,
                ["department_category"] = department.Category?.ToString()
            });

        return updated;
    }

    public Organization RemoveDepartment(Organization organization, string departmentId, DateOnly date)
    {
        organization.Validate();
        var updated = organization.RemoveDepartment(departmentId);

        QueueOrganizationEvent(
            updated,
            LegacyEventType.OrganizationDepartmentRemoved,
            date,
            "Department removed",
            $"A department was removed from {updated.Name}.",
            metadata: new Dictionary<string, object?>
            {
                ["department_id"] = departmentId
            });

        return updated;
    }

    public Organization ChangeStatus(Organization organization, OrganizationStatus status, DateOnly date, string? reason = null)
    {
        organization.Validate();

        var previousStatus = organization.Status;
        var updated = organization.ChangeStatus(status);
        updated.Validate();

        QueueOrganizationEvent(
            updated,
            LegacyEventType.OrganizationStatusChanged,
            date,
            "Organization status changed",
            reason is null
                ? $"{updated.Name} status changed from {previousStatus} to {status}."
                : $"{updated.Name} status changed from {previousStatus} to {status}: {reason}",
            metadata: new Dictionary<string, object?>
            {
                ["previous_status"] = previousStatus.ToString(),
                ["new_status"] = status.ToString()
            });

        return updated;
    }

    private LegacyEvent QueueOrganizationEvent(
        Organization organization,
        LegacyEventType eventType,
        DateOnly date,
        string title,
        string description,
        string? primaryPersonId = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var details = metadata ?? new Dictionary<string, object?>();
        var merged = new Dictionary<string, object?>(details)
        {
            ["organization_id"] = organization.OrganizationId,
            ["organization_type"] = organization.Type.ToString(),
            ["status"] = organization.Status.ToString()
        };

        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: primaryPersonId,
                OrganizationId: organization.OrganizationId),
            merged);

        return _eventEngine.QueueEvent(legacyEvent);
    }
}
