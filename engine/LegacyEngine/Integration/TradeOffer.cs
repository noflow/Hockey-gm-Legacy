namespace LegacyEngine.Integration;

public sealed record TradeOffer(
    string TradeOfferId,
    DateOnly ProposedOn,
    string OtherOrganizationId,
    string OtherOrganizationName,
    TradeOfferStatus Status,
    IReadOnlyList<TradeAsset> PlayerGives,
    IReadOnlyList<TradeAsset> PlayerReceives,
    TradeEvaluation? Evaluation = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TradeOfferId)
            || string.IsNullOrWhiteSpace(OtherOrganizationId)
            || string.IsNullOrWhiteSpace(OtherOrganizationName))
        {
            throw new ArgumentException("Trade offer requires ids and other organization identity.");
        }

        foreach (var asset in PlayerGives.Concat(PlayerReceives))
        {
            asset.Validate();
        }

        Evaluation?.Validate();
    }
}
