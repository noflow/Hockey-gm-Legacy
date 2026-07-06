namespace LegacyEngine.Integration;

public sealed record FreeAgentInterest(
    int PlayerOrganizationInterest,
    string CompetingInterest,
    string MotivationSummary)
{
    public void Validate()
    {
        if (PlayerOrganizationInterest is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerOrganizationInterest), "Free agent interest must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(CompetingInterest) || string.IsNullOrWhiteSpace(MotivationSummary))
        {
            throw new ArgumentException("Free agent interest requires competing interest and motivation summary.");
        }
    }
}
