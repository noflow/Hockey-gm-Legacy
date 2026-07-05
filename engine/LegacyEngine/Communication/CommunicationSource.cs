namespace LegacyEngine.Communication;

public sealed record CommunicationSource(
    string SourceId,
    string DisplayName,
    string? PersonId = null,
    string? OrganizationId = null,
    bool IsSystem = false)
{
    public static CommunicationSource System(string displayName = "League Office") =>
        new("system", displayName, IsSystem: true);

    public static CommunicationSource FromPerson(string personId, string displayName, string? organizationId = null) =>
        new(personId, displayName, PersonId: personId, OrganizationId: organizationId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceId))
        {
            throw new ArgumentException("Communication source id is required.", nameof(SourceId));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Communication source display name is required.", nameof(DisplayName));
        }
    }
}
