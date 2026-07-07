namespace LegacyEngine.Integration;

public sealed record ScoutDiscovery(
    string PlayerId,
    string PlayerName,
    int Year,
    string Outcome,
    string Notes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayerId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Outcome)
            || string.IsNullOrWhiteSpace(Notes))
        {
            throw new ArgumentException("Scout discovery requires player identity and readable outcome.");
        }
    }
}
