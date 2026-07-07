namespace LegacyEngine.Integration;

public sealed record FreeAgentMotivationScore(
    FreeAgentMotivation Motivation,
    int Importance,
    string Explanation)
{
    public void Validate()
    {
        if (Importance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Importance), "Motivation importance must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("Motivation explanation is required.", nameof(Explanation));
        }
    }
}
