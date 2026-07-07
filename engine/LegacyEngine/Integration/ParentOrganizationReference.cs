namespace LegacyEngine.Integration;

public sealed record ParentOrganizationReference(string OrganizationId, string TeamName)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName))
        {
            throw new ArgumentException("Parent organization reference requires organization id and team name.");
        }
    }
}
