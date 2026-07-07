namespace LegacyEngine.Integration;

public sealed record OwnerLetter(
    string LetterId,
    DateOnly Date,
    string Subject,
    string Body,
    bool IsWarning)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LetterId)
            || string.IsNullOrWhiteSpace(Subject)
            || string.IsNullOrWhiteSpace(Body))
        {
            throw new ArgumentException("Owner letter requires id, subject, and body.");
        }
    }
}
