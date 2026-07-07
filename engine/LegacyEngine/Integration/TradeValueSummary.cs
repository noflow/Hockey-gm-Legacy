namespace LegacyEngine.Integration;

public sealed record TradeValueSummary(
    string PersonId,
    string PlayerName,
    int Age,
    string Role,
    string Contract,
    decimal BudgetImpact,
    int YearsRemaining,
    string DevelopmentSummary,
    string ProspectValue,
    string EstimatedLeagueValue,
    string Opinion)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Role)
            || string.IsNullOrWhiteSpace(Contract)
            || string.IsNullOrWhiteSpace(DevelopmentSummary)
            || string.IsNullOrWhiteSpace(ProspectValue)
            || string.IsNullOrWhiteSpace(EstimatedLeagueValue)
            || string.IsNullOrWhiteSpace(Opinion))
        {
            throw new ArgumentException("Trade value summary requires readable player context.");
        }
    }
}
