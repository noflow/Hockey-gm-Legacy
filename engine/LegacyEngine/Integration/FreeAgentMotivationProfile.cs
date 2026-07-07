namespace LegacyEngine.Integration;

public sealed record FreeAgentMotivationProfile(
    string PersonId,
    IReadOnlyList<FreeAgentMotivationScore> Motivations)
{
    public IReadOnlyList<FreeAgentMotivationScore> TopMotivations =>
        Motivations
            .OrderByDescending(item => item.Importance)
            .ThenBy(item => item.Motivation)
            .Take(3)
            .ToArray();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Motivation profile person id is required.", nameof(PersonId));
        }

        if (Motivations.Count == 0)
        {
            throw new ArgumentException("Free agent motivations are required.", nameof(Motivations));
        }

        foreach (var motivation in Motivations)
        {
            motivation.Validate();
        }
    }
}
