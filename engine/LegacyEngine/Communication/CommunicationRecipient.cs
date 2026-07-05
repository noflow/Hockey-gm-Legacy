namespace LegacyEngine.Communication;

public sealed record CommunicationRecipient(
    string RecipientId,
    string DisplayName,
    string? PersonId = null,
    string? OrganizationId = null)
{
    public static CommunicationRecipient Person(string personId, string displayName, string? organizationId = null) =>
        new(personId, displayName, PersonId: personId, OrganizationId: organizationId);

    public static CommunicationRecipient Organization(string organizationId, string displayName) =>
        new(organizationId, displayName, OrganizationId: organizationId);

    public bool Matches(string personOrOrganizationId) =>
        !string.IsNullOrWhiteSpace(personOrOrganizationId)
        && (RecipientId == personOrOrganizationId
            || PersonId == personOrOrganizationId
            || OrganizationId == personOrOrganizationId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecipientId))
        {
            throw new ArgumentException("Communication recipient id is required.", nameof(RecipientId));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Communication recipient display name is required.", nameof(DisplayName));
        }
    }
}
