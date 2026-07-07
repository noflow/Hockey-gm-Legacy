namespace LegacyEngine.Integration;

public sealed record BuyerSellerAssessment(
    DateOnly Date,
    DeadlineTeamStrategy PlayerTeamStrategy,
    IReadOnlyList<DeadlineTeamStrategy> LeagueStrategies,
    string Summary)
{
    public void Validate()
    {
        PlayerTeamStrategy.Validate();
        foreach (var strategy in LeagueStrategies)
        {
            strategy.Validate();
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Buyer/seller assessment summary is required.", nameof(Summary));
        }
    }
}
