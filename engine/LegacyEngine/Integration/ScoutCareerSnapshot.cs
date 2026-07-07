namespace LegacyEngine.Integration;

public sealed record ScoutCareerSnapshot(
    string ScoutId,
    string ScoutName,
    IReadOnlyList<ScoutDiscovery> DiscoveredPlayers,
    int ExperiencePoints,
    string ReputationTrend,
    IReadOnlyList<string> Specializations,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScoutId)
            || string.IsNullOrWhiteSpace(ScoutName)
            || string.IsNullOrWhiteSpace(ReputationTrend)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Scout career snapshot requires scout identity and summary.");
        }

        foreach (var discovery in DiscoveredPlayers)
        {
            discovery.Validate();
        }
    }
}
