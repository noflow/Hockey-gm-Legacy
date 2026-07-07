namespace LegacyEngine.Integration;

public sealed record TeamNeed(
    PositionNeed Need,
    TradePriority Priority,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Team need requires a readable reason.", nameof(Reason));
        }
    }
}
