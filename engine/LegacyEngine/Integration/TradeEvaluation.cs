namespace LegacyEngine.Integration;

public sealed record TradeEvaluation(
    TradeOfferStatus Decision,
    int Score,
    string Explanation,
    IReadOnlyList<string> Reasons,
    decimal BudgetImpact,
    int RosterImpact)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("Trade evaluation explanation is required.", nameof(Explanation));
        }

        if (Reasons.Count == 0 || Reasons.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Trade evaluation requires readable reasons.", nameof(Reasons));
        }
    }
}
