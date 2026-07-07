namespace LegacyEngine.Integration;

public sealed record AffiliateOrganizationReference(string OrganizationId, string TeamName)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName))
        {
            throw new ArgumentException("Affiliate organization reference requires organization id and team name.");
        }
    }
}
