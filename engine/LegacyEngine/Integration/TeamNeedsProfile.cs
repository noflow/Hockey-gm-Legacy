namespace LegacyEngine.Integration;

public sealed record TeamNeedsProfile(
    string OrganizationId,
    string TeamName,
    TeamDirection Direction,
    TradeGmPersonality GmPersonality,
    IReadOnlyList<TeamNeed> Needs,
    IReadOnlyList<AssetPreference> AssetPreferences,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Team needs profile requires organization identity and summary.");
        }

        if (Needs.Count == 0 || AssetPreferences.Count == 0)
        {
            throw new ArgumentException("Team needs profile requires needs and asset preferences.");
        }

        foreach (var need in Needs)
        {
            need.Validate();
        }
    }
}
