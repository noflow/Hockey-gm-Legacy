using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record TradeBlockEntry(
    string EntryId,
    string PersonId,
    string OrganizationId,
    string TeamName,
    string Name,
    RosterPosition Position,
    int Age,
    string PlayerType,
    string CurrentRole,
    string ContractStatus,
    decimal SalaryImpact,
    string AskingPriceSummary,
    string ReasonAvailable,
    TradeInterest InterestLevel,
    int AssetValue)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EntryId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(PlayerType)
            || string.IsNullOrWhiteSpace(CurrentRole)
            || string.IsNullOrWhiteSpace(ContractStatus)
            || string.IsNullOrWhiteSpace(AskingPriceSummary)
            || string.IsNullOrWhiteSpace(ReasonAvailable))
        {
            throw new ArgumentException("Trade block entry requires readable team, player, and availability details.");
        }

        if (Position == RosterPosition.Unknown || Age <= 0 || AssetValue < 0)
        {
            throw new ArgumentException("Trade block entry requires known position, realistic age, and non-negative value.");
        }
    }
}
