namespace LegacyEngine.Owners;

public sealed record OwnerSeasonPerformance(
    decimal WinPercentage,
    bool MadePlayoffs,
    bool WonChampionship,
    int ProspectsDeveloped,
    bool FinancialTargetMet,
    int CommunityTrustChange,
    decimal BudgetSpent)
{
    public void Validate()
    {
        if (WinPercentage is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(WinPercentage), "Win percentage must be between 0 and 1.");
        }

        if (ProspectsDeveloped < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ProspectsDeveloped), "Prospects developed cannot be negative.");
        }

        if (BudgetSpent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BudgetSpent), "Budget spent cannot be negative.");
        }
    }
}
