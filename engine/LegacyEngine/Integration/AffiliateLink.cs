namespace LegacyEngine.Integration;

public sealed record AffiliateLink(
    string ParentOrganizationId,
    string ParentTeamName,
    string AffiliateOrganizationId,
    string AffiliateTeamName)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ParentOrganizationId)
            || string.IsNullOrWhiteSpace(ParentTeamName)
            || string.IsNullOrWhiteSpace(AffiliateOrganizationId)
            || string.IsNullOrWhiteSpace(AffiliateTeamName))
        {
            throw new ArgumentException("Affiliate link requires parent and affiliate organization identities.");
        }

        if (ParentOrganizationId == AffiliateOrganizationId)
        {
            throw new ArgumentException("Affiliate link cannot point an organization to itself.");
        }
    }
}
