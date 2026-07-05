namespace LegacyEngine.Organizations;

/// <summary>
/// The identity of an organization: its name and where it is based. This is
/// descriptive data only and does not assume any single league or country.
/// </summary>
public sealed record OrganizationIdentity(
    string Name,
    string City,
    string Region,
    string Country)
{
    public string Location => $"{City}, {Region}, {Country}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Organization name is required.", nameof(Name));
        }

        if (string.IsNullOrWhiteSpace(City))
        {
            throw new ArgumentException("Organization city is required.", nameof(City));
        }

        if (string.IsNullOrWhiteSpace(Region))
        {
            throw new ArgumentException("Organization region is required.", nameof(Region));
        }

        if (string.IsNullOrWhiteSpace(Country))
        {
            throw new ArgumentException("Organization country is required.", nameof(Country));
        }
    }
}
