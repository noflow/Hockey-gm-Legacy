namespace LegacyEngine.Integration;

public sealed record TeamSelectionOption(
    string OrganizationId,
    string TeamName,
    string City,
    string Region,
    string Country,
    string LogoPlaceholder,
    string PreviousRecord,
    string OwnerExpectations,
    decimal Budget,
    int CurrentGmReputation,
    string ProspectStrength,
    string Difficulty,
    string RosterQuality,
    string? ParentOrganizationId = null,
    string? AffiliateOrganizationId = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(City)
            || string.IsNullOrWhiteSpace(Region)
            || string.IsNullOrWhiteSpace(Country)
            || string.IsNullOrWhiteSpace(LogoPlaceholder)
            || string.IsNullOrWhiteSpace(PreviousRecord)
            || string.IsNullOrWhiteSpace(OwnerExpectations)
            || string.IsNullOrWhiteSpace(ProspectStrength)
            || string.IsNullOrWhiteSpace(Difficulty)
            || string.IsNullOrWhiteSpace(RosterQuality)
            || Budget <= 0
            || CurrentGmReputation is < 0 or > 100)
        {
            throw new ArgumentException("Team selection requires identity, record, expectations, budget, reputation, pipeline, difficulty, and roster quality.");
        }

        if (ParentOrganizationId == OrganizationId || AffiliateOrganizationId == OrganizationId)
        {
            throw new ArgumentException("Team cannot reference itself as parent or affiliate.");
        }
    }
}
