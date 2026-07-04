using LegacyEngine.People;

internal sealed class PersonEngineTests
{
    public void CreatingAPerson()
    {
        var person = BuildPerson();

        person.Validate();
        Assert.Equal("person-001", person.PersonId);
        Assert.Equal("Mika Tanaka", person.Identity.DisplayName);
        Assert.Equal(Gender.Female, person.Identity.Gender);
        Assert.Equal("Canada", person.Identity.Nationality);
        Assert.Equal("Vancouver, British Columbia", person.Identity.Birthplace);
        Assert.Equal(PersonStatus.Active, person.Status);
        Assert.Equal(50, person.Reputation.Local);
        Assert.Equal(70, person.Personality.Professionalism);
    }

    public void CalculatingAge()
    {
        var person = BuildPerson();

        Assert.Equal(17, person.CalculateAge(new DateOnly(2026, 1, 14)));
        Assert.Equal(18, person.CalculateAge(new DateOnly(2026, 1, 15)));
        Assert.Throws<ArgumentOutOfRangeException>(() => person.CalculateAge(new DateOnly(2007, 1, 14)));
    }

    public void AddingARole()
    {
        var person = BuildPerson();
        var role = BuildRole("role-player", PersonRoleType.Player, "Player", new DateOnly(2026, 8, 1));
        var updated = person.AddRole(role);

        Assert.Equal(1, updated.Roles.Count);
        Assert.Equal("role-player", updated.Roles[0].RoleId);
        Assert.Equal(1, updated.CareerTimeline.Count);
        Assert.Equal(CareerTimelineEntryType.RoleStarted, updated.CareerTimeline[0].EntryType);
        Assert.Throws<ArgumentException>(() => updated.AddRole(role));
    }

    public void EndingARole()
    {
        var person = BuildPerson().AddRole(BuildRole("role-player", PersonRoleType.Player, "Player", new DateOnly(2026, 8, 1)));
        var updated = person.EndRole("role-player", new DateOnly(2027, 5, 1));

        Assert.Equal(new DateOnly(2027, 5, 1), updated.Roles[0].EndDate);
        Assert.Equal(0, updated.ActiveRolesOn(new DateOnly(2027, 5, 2)).Count);
        Assert.Equal(2, updated.CareerTimeline.Count);
        Assert.Equal(CareerTimelineEntryType.RoleEnded, updated.CareerTimeline[1].EntryType);
        Assert.Throws<ArgumentException>(() => updated.EndRole("missing-role", new DateOnly(2027, 5, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => person.EndRole("role-player", new DateOnly(2026, 7, 31)));
    }

    public void HoldingMultipleRoles()
    {
        var person = BuildPerson()
            .AddRole(BuildRole("role-player", PersonRoleType.Player, "Player", new DateOnly(2026, 8, 1)))
            .AddRole(BuildRole("role-captain", PersonRoleType.Coach, "Player mentor", new DateOnly(2026, 9, 1)));

        var activeRoles = person.ActiveRolesOn(new DateOnly(2026, 10, 1));

        Assert.Equal(2, person.Roles.Count);
        Assert.Equal(2, activeRoles.Count);
        Assert.True(activeRoles.Any(role => role.RoleType == PersonRoleType.Player), "Person should hold the player role.");
        Assert.True(activeRoles.Any(role => role.RoleType == PersonRoleType.Coach), "Person should hold the mentor role.");
    }

    public void ReputationChanges()
    {
        var person = BuildPerson();
        var updated = person.ChangeReputation(
            localDelta: 60,
            leagueDelta: 8,
            nationalDelta: -20,
            date: new DateOnly(2026, 11, 2),
            reason: "Breakout tournament raised league reputation.");

        Assert.Equal(100, updated.Reputation.Local);
        Assert.Equal(48, updated.Reputation.League);
        Assert.Equal(0, updated.Reputation.National);
        Assert.Equal(1, updated.CareerTimeline.Count);
        Assert.Equal(CareerTimelineEntryType.ReputationChanged, updated.CareerTimeline[0].EntryType);
        Assert.Throws<ArgumentException>(() => person.ChangeReputation(1, 1, 1, new DateOnly(2026, 11, 2), ""));
    }

    public void StatusChanges()
    {
        var person = BuildPerson();
        var retired = person.ChangeStatus(PersonStatus.Retired, new DateOnly(2042, 6, 1), "Retired from active hockey work.");
        var deceased = retired.ChangeStatus(PersonStatus.Deceased, new DateOnly(2084, 3, 12), "Recorded as deceased.");

        Assert.Equal(PersonStatus.Retired, retired.Status);
        Assert.Equal(PersonStatus.Deceased, deceased.Status);
        Assert.Equal(2, deceased.CareerTimeline.Count);
        Assert.Equal(CareerTimelineEntryType.StatusChanged, deceased.CareerTimeline[0].EntryType);
        Assert.Throws<InvalidOperationException>(() => deceased.ChangeStatus(PersonStatus.Active, new DateOnly(2085, 1, 1), "Impossible status change."));
    }

    public void CareerTimelineEntries()
    {
        var person = BuildPerson()
            .AddCareerTimelineEntry(new CareerTimelineEntry(
                EntryId: "milestone-002",
                Date: new DateOnly(2028, 5, 3),
                EntryType: CareerTimelineEntryType.Milestone,
                Summary: "Won a junior championship.",
                Details: new Dictionary<string, object?> { ["team"] = "Vancouver Juniors" }))
            .AddCareerTimelineEntry(new CareerTimelineEntry(
                EntryId: "milestone-001",
                Date: new DateOnly(2026, 8, 1),
                EntryType: CareerTimelineEntryType.Note,
                Summary: "Entered the organization.",
                Details: new Dictionary<string, object?>()));

        Assert.Equal(2, person.CareerTimeline.Count);
        Assert.Equal("milestone-001", person.CareerTimeline[0].EntryId);
        Assert.Equal("milestone-002", person.CareerTimeline[1].EntryId);
        Assert.Throws<ArgumentException>(() => person.AddCareerTimelineEntry(person.CareerTimeline[0]));
    }

    private static Person BuildPerson() =>
        new(
            PersonId: "person-001",
            Identity: new PersonIdentity(
                FirstName: "Mika",
                LastName: "Tanaka",
                Gender: Gender.Female,
                BirthDate: new DateOnly(2008, 1, 15),
                Nationality: "Canada",
                Birthplace: "Vancouver, British Columbia"),
            Status: PersonStatus.Active,
            Roles: Array.Empty<PersonRole>(),
            Reputation: new PersonReputation(Local: 50, League: 40, National: 20),
            Personality: new PersonalityProfile(
                Ambition: 72,
                Loyalty: 65,
                Temperament: 58,
                Adaptability: 80,
                Professionalism: 70),
            CareerTimeline: Array.Empty<CareerTimelineEntry>());

    private static PersonRole BuildRole(string roleId, PersonRoleType roleType, string title, DateOnly startDate) =>
        new(
            RoleId: roleId,
            RoleType: roleType,
            OrganizationId: "vancouver-juniors",
            StartDate: startDate,
            EndDate: null,
            Title: title);
}
