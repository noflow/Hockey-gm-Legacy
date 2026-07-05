using LegacyEngine.Events;
using LegacyEngine.Organizations;
using LegacyEngine.Staff;

internal sealed class OrganizationEngineTests
{
    public void OrganizationCreation()
    {
        var organization = new OrganizationEngine().CreateOrganization(
            "org-001",
            OrganizationType.Team,
            Identity(),
            new DateOnly(2026, 7, 1));

        organization.Validate();
        Assert.Equal("org-001", organization.OrganizationId);
        Assert.Equal(OrganizationType.Team, organization.Type);
        Assert.Equal(OrganizationStatus.Active, organization.Status);
    }

    public void IdentityFieldsStored()
    {
        var organization = new OrganizationEngine().CreateOrganization(
            "org-001",
            OrganizationType.Team,
            new OrganizationIdentity("Kelowna Rockets", "Kelowna", "British Columbia", "Canada"),
            new DateOnly(2026, 7, 1));

        Assert.Equal("Kelowna Rockets", organization.Identity.Name);
        Assert.Equal("Kelowna", organization.Identity.City);
        Assert.Equal("British Columbia", organization.Identity.Region);
        Assert.Equal("Canada", organization.Identity.Country);
    }

    public void OwnerReferenceStored()
    {
        var organization = new OrganizationEngine().CreateOrganization(
            "org-001",
            OrganizationType.Team,
            Identity(),
            new DateOnly(2026, 7, 1),
            ownerPersonId: "person-owner-1");

        Assert.Equal("person-owner-1", organization.OwnerPersonId);
    }

    public void LeagueReferenceStored()
    {
        var organization = new OrganizationEngine().CreateOrganization(
            "org-001",
            OrganizationType.Team,
            Identity(),
            new DateOnly(2026, 7, 1),
            leagueId: "league-whl");

        Assert.Equal("league-whl", organization.LeagueId);
    }

    public void StaffMembershipAdded()
    {
        var engine = new OrganizationEngine();
        var organization = engine.CreateOrganization("org-001", OrganizationType.Team, Identity(), new DateOnly(2026, 7, 1));

        var updated = engine.AddStaff(
            organization,
            new OrganizationMembership("person-101", StaffRole.HeadCoach, "dept-coaching", new DateOnly(2026, 7, 2)),
            new DateOnly(2026, 7, 2));

        Assert.True(updated.HasStaffMember("person-101"), "Staff member should be added.");
        Assert.Equal(1, updated.StaffMemberships.Count);
    }

    public void StaffMembershipRemoved()
    {
        var engine = new OrganizationEngine();
        var organization = engine.CreateOrganization("org-001", OrganizationType.Team, Identity(), new DateOnly(2026, 7, 1));
        organization = engine.AddStaff(organization, new OrganizationMembership("person-101"), new DateOnly(2026, 7, 2));

        var updated = engine.RemoveStaff(organization, "person-101", new DateOnly(2026, 8, 1));

        Assert.False(updated.HasStaffMember("person-101"), "Staff member should be removed.");
        Assert.Equal(0, updated.StaffMemberships.Count);
    }

    public void DepartmentAdded()
    {
        var engine = new OrganizationEngine();
        var organization = engine.CreateOrganization("org-001", OrganizationType.Team, Identity(), new DateOnly(2026, 7, 1));

        var updated = engine.AddDepartment(
            organization,
            new OrganizationDepartment("dept-scouting", "Scouting", StaffDepartment.Scouting),
            new DateOnly(2026, 7, 3));

        Assert.True(updated.HasDepartment("dept-scouting"), "Department should be added.");
        Assert.Equal(StaffDepartment.Scouting, updated.Departments[0].Category);
    }

    public void DepartmentRemoved()
    {
        var engine = new OrganizationEngine();
        var organization = engine.CreateOrganization("org-001", OrganizationType.Team, Identity(), new DateOnly(2026, 7, 1));
        organization = engine.AddDepartment(organization, new OrganizationDepartment("dept-scouting", "Scouting"), new DateOnly(2026, 7, 3));

        var updated = engine.RemoveDepartment(organization, "dept-scouting", new DateOnly(2026, 8, 1));

        Assert.False(updated.HasDepartment("dept-scouting"), "Department should be removed.");
        Assert.Equal(0, updated.Departments.Count);
    }

    public void CultureValuesStored()
    {
        var culture = new OrganizationCulture(
            DevelopmentFocus: 80,
            WinningPressure: 60,
            FinancialDiscipline: 55,
            CommunityFocus: 70,
            Innovation: 65,
            Loyalty: 75);
        var organization = new OrganizationEngine().CreateOrganization(
            "org-001",
            OrganizationType.Team,
            Identity(),
            new DateOnly(2026, 7, 1),
            culture: culture);

        Assert.Equal(80, organization.Culture.DevelopmentFocus);
        Assert.Equal(60, organization.Culture.WinningPressure);
        Assert.Equal(55, organization.Culture.FinancialDiscipline);
        Assert.Equal(70, organization.Culture.CommunityFocus);
        Assert.Equal(65, organization.Culture.Innovation);
        Assert.Equal(75, organization.Culture.Loyalty);
    }

    public void ReputationValuesStored()
    {
        var reputation = new OrganizationReputation(Local: 72, League: 58, National: 40);
        var organization = new OrganizationEngine().CreateOrganization(
            "org-001",
            OrganizationType.Team,
            Identity(),
            new DateOnly(2026, 7, 1),
            reputation: reputation);

        Assert.Equal(72, organization.Reputation.Local);
        Assert.Equal(58, organization.Reputation.League);
        Assert.Equal(40, organization.Reputation.National);
    }

    public void StatusChange()
    {
        var engine = new OrganizationEngine();
        var organization = engine.CreateOrganization("org-001", OrganizationType.Team, Identity(), new DateOnly(2026, 7, 1));

        var relocated = engine.ChangeStatus(organization, OrganizationStatus.Relocated, new DateOnly(2027, 5, 1), "Moved to a new market.");

        Assert.Equal(OrganizationStatus.Relocated, relocated.Status);
        Assert.True(
            engine.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.OrganizationStatusChanged),
            "Status change should queue an OrganizationStatusChanged event.");
    }

    public void EventsCreated()
    {
        var eventEngine = new EventEngine();
        var engine = new OrganizationEngine(eventEngine);

        var organization = engine.CreateOrganization("org-001", OrganizationType.Team, Identity(), new DateOnly(2026, 7, 1));
        organization = engine.AddStaff(organization, new OrganizationMembership("person-101", StaffRole.HeadCoach), new DateOnly(2026, 7, 2));
        organization = engine.AddDepartment(organization, new OrganizationDepartment("dept-medical", "Medical", StaffDepartment.Medical), new DateOnly(2026, 7, 3));
        organization = engine.RemoveDepartment(organization, "dept-medical", new DateOnly(2026, 7, 4));
        organization = engine.RemoveStaff(organization, "person-101", new DateOnly(2026, 7, 5));
        engine.ChangeStatus(organization, OrganizationStatus.Inactive, new DateOnly(2026, 7, 6));

        var pending = eventEngine.Queue.PendingEvents;
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.OrganizationCreated), "OrganizationCreated event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.OrganizationStaffAdded), "OrganizationStaffAdded event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.OrganizationDepartmentAdded), "OrganizationDepartmentAdded event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.OrganizationDepartmentRemoved), "OrganizationDepartmentRemoved event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.OrganizationStaffRemoved), "OrganizationStaffRemoved event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.OrganizationStatusChanged), "OrganizationStatusChanged event should be queued.");
    }

    public void NoUiOrGodotDependencyExists()
    {
        var organizationFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Organizations"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in organizationFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Organization module should not reference Godot.");
            Assert.False(text.Contains("Control", StringComparison.Ordinal), "Organization module should not define UI controls.");
        }
    }

    private static OrganizationIdentity Identity() =>
        new("Kelowna Rockets", "Kelowna", "British Columbia", "Canada");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulebookPath = Path.Combine(directory.FullName, "data", "rulebooks", "junior_v1.json");
            if (File.Exists(rulebookPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
