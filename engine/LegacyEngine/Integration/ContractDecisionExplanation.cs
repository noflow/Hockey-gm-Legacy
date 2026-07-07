namespace LegacyEngine.Integration;

public sealed record ContractDecisionExplanation(
    ContractOfferDecision Decision,
    string Summary,
    IReadOnlyList<string> Reasons)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary) || Reasons.Count == 0)
        {
            throw new ArgumentException("Contract decision explanation requires summary and reasons.");
        }

        foreach (var reason in Reasons)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Contract decision reasons cannot be blank.", nameof(Reasons));
            }
        }
    }
}
