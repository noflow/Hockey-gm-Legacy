namespace LegacyEngine.Integration;

public sealed record DeadlineTeamStrategy(
    string OrganizationId,
    string TeamName,
    TeamTradeDirection Direction,
    string NeedSummary,
    string AssetPreference,
    int Aggressiveness)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(NeedSummary)
            || string.IsNullOrWhiteSpace(AssetPreference))
        {
            throw new ArgumentException("Deadline team strategy requires identity and summaries.");
        }

        if (Aggressiveness is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Aggressiveness), "Aggressiveness must be between 0 and 100.");
        }
    }
}
