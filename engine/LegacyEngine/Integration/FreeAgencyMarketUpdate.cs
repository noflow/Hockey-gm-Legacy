namespace LegacyEngine.Integration;

public sealed record FreeAgencyMarketUpdate(
    string UpdateId,
    DateOnly Date,
    FreeAgencyPhase Phase,
    string Summary,
    bool IsInboxWorthy)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UpdateId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Free agency market update requires id and summary.");
        }
    }
}
