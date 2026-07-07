using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record TradeAsset(
    TradeAssetType AssetType,
    TradeSide Side,
    string OrganizationId,
    string OrganizationName,
    string AssetId,
    string DisplayName,
    RosterPosition? Position = null,
    int? Age = null,
    decimal SalaryImpact = 0m,
    int Value = 0,
    string Summary = "")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(AssetId)
            || string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Trade asset requires organization, id, and display name.");
        }

        if (Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Value), "Trade asset value cannot be negative.");
        }
    }
}
