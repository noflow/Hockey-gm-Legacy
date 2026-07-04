namespace LegacyEngine.People;

public sealed record PersonRole(
    string RoleId,
    PersonRoleType RoleType,
    string? OrganizationId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Title)
{
    public bool IsActiveOn(DateOnly date) => StartDate <= date && (!EndDate.HasValue || EndDate.Value >= date);

    public PersonRole End(DateOnly endDate)
    {
        if (endDate < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "Role end date cannot be before start date.");
        }

        return this with { EndDate = endDate };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RoleId))
        {
            throw new ArgumentException("Role id is required.", nameof(RoleId));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Role title is required.", nameof(Title));
        }

        if (EndDate.HasValue && EndDate.Value < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(EndDate), "Role end date cannot be before start date.");
        }
    }
}
