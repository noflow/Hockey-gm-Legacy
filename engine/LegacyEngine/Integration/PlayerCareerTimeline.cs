namespace LegacyEngine.Integration;

public sealed record PlayerCareerTimeline(
    string PersonId,
    string PersonName,
    IReadOnlyList<string> Entries)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PersonName))
        {
            throw new ArgumentException("Career timeline requires person identity.");
        }

        if (Entries.Count == 0 || Entries.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Career timeline requires at least one readable entry.");
        }
    }
}
