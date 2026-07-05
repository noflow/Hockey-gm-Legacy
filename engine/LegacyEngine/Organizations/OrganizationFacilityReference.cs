namespace LegacyEngine.Organizations;

/// <summary>
/// A reference to a facility owned by a future Facilities engine. The Organization
/// engine stores the reference only; it does not model facilities themselves.
/// </summary>
public sealed record OrganizationFacilityReference(
    string FacilityId,
    string OrganizationId,
    string? Name = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FacilityId))
        {
            throw new ArgumentException("Facility id is required.", nameof(FacilityId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }
    }
}
