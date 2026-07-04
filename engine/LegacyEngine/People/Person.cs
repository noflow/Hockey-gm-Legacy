namespace LegacyEngine.People;

public sealed record Person(
    string PersonId,
    PersonIdentity Identity,
    PersonStatus Status,
    IReadOnlyList<PersonRole> Roles,
    PersonReputation Reputation,
    PersonalityProfile Personality,
    IReadOnlyList<CareerTimelineEntry> CareerTimeline)
{
    public int CalculateAge(DateOnly onDate) => Identity.CalculateAge(onDate);

    public Person AddRole(PersonRole role)
    {
        Validate();
        role.Validate();

        if (Roles.Any(existing => existing.RoleId == role.RoleId))
        {
            throw new ArgumentException("A person cannot hold two roles with the same role id.", nameof(role));
        }

        return WithTimeline(
            this with { Roles = Roles.Append(role).ToArray() },
            new CareerTimelineEntry(
                EntryId: $"role-started:{role.RoleId}",
                Date: role.StartDate,
                EntryType: CareerTimelineEntryType.RoleStarted,
                Summary: $"{Identity.DisplayName} started role: {role.Title}.",
                Details: new Dictionary<string, object?>
                {
                    ["role_id"] = role.RoleId,
                    ["role_type"] = role.RoleType.ToString(),
                    ["organization_id"] = role.OrganizationId
                }));
    }

    public Person EndRole(string roleId, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            throw new ArgumentException("Role id is required.", nameof(roleId));
        }

        var role = Roles.SingleOrDefault(existing => existing.RoleId == roleId);
        if (role is null)
        {
            throw new ArgumentException("Role was not found for this person.", nameof(roleId));
        }

        var endedRole = role.End(endDate);
        var roles = Roles.Select(existing => existing.RoleId == roleId ? endedRole : existing).ToArray();

        return WithTimeline(
            this with { Roles = roles },
            new CareerTimelineEntry(
                EntryId: $"role-ended:{roleId}:{endDate:yyyyMMdd}",
                Date: endDate,
                EntryType: CareerTimelineEntryType.RoleEnded,
                Summary: $"{Identity.DisplayName} ended role: {role.Title}.",
                Details: new Dictionary<string, object?>
                {
                    ["role_id"] = role.RoleId,
                    ["role_type"] = role.RoleType.ToString(),
                    ["organization_id"] = role.OrganizationId
                }));
    }

    public IReadOnlyList<PersonRole> ActiveRolesOn(DateOnly date) =>
        Roles.Where(role => role.IsActiveOn(date)).ToArray();

    public Person ChangeReputation(int localDelta, int leagueDelta, int nationalDelta, DateOnly date, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reputation change reason is required.", nameof(reason));
        }

        var reputation = Reputation.Change(localDelta, leagueDelta, nationalDelta);

        return WithTimeline(
            this with { Reputation = reputation },
            new CareerTimelineEntry(
                EntryId: $"reputation:{date:yyyyMMdd}:{CareerTimeline.Count + 1}",
                Date: date,
                EntryType: CareerTimelineEntryType.ReputationChanged,
                Summary: reason,
                Details: new Dictionary<string, object?>
                {
                    ["local_delta"] = localDelta,
                    ["league_delta"] = leagueDelta,
                    ["national_delta"] = nationalDelta,
                    ["local"] = reputation.Local,
                    ["league"] = reputation.League,
                    ["national"] = reputation.National
                }));
    }

    public Person ChangeStatus(PersonStatus status, DateOnly date, string reason)
    {
        if (Status == PersonStatus.Deceased && status != PersonStatus.Deceased)
        {
            throw new InvalidOperationException("A deceased person cannot return to active or retired status.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Status change reason is required.", nameof(reason));
        }

        return WithTimeline(
            this with { Status = status },
            new CareerTimelineEntry(
                EntryId: $"status:{status}:{date:yyyyMMdd}",
                Date: date,
                EntryType: CareerTimelineEntryType.StatusChanged,
                Summary: reason,
                Details: new Dictionary<string, object?> { ["status"] = status.ToString() }));
    }

    public Person AddCareerTimelineEntry(CareerTimelineEntry entry)
    {
        Validate();
        entry.Validate();

        if (CareerTimeline.Any(existing => existing.EntryId == entry.EntryId))
        {
            throw new ArgumentException("Timeline entry ids must be unique for a person.", nameof(entry));
        }

        return this with { CareerTimeline = CareerTimeline.Append(entry).OrderBy(item => item.Date).ToArray() };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        Identity.Validate();
        Reputation.Validate();
        Personality.Validate();

        foreach (var role in Roles)
        {
            role.Validate();
        }

        foreach (var entry in CareerTimeline)
        {
            entry.Validate();
        }

        if (CareerTimeline.Select(entry => entry.EntryId).Distinct().Count() != CareerTimeline.Count)
        {
            throw new ArgumentException("Timeline entry ids must be unique.", nameof(CareerTimeline));
        }
    }

    private Person WithTimeline(Person person, CareerTimelineEntry entry) => person.AddCareerTimelineEntry(entry);
}
