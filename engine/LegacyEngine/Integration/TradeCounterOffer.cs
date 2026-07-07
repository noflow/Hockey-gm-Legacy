namespace LegacyEngine.Integration;

public sealed record TradeCounterOffer(
    string TradeOfferId,
    string Message,
    IReadOnlyList<TradeAsset> RequestedAssets,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TradeOfferId)
            || string.IsNullOrWhiteSpace(Message)
            || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Trade counter offer requires id, message, and reason.");
        }

        foreach (var asset in RequestedAssets)
        {
            asset.Validate();
        }
    }
}
