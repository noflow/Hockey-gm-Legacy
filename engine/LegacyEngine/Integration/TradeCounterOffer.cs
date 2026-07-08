namespace LegacyEngine.Integration;

public sealed record TradeCounterOffer(
    string TradeOfferId,
    string Message,
    IReadOnlyList<TradeAsset> RequestedAssets,
    string Reason)
{
    public IReadOnlyList<TradeAsset> RevisedPlayerGives { get; init; } = Array.Empty<TradeAsset>();

    public IReadOnlyList<TradeAsset> RevisedPlayerReceives { get; init; } = Array.Empty<TradeAsset>();

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

        foreach (var asset in RevisedPlayerGives.Concat(RevisedPlayerReceives))
        {
            asset.Validate();
        }
    }
}
