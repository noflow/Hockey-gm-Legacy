namespace LegacyEngine.Relationships;

public sealed record RelationshipChange(
    RelationshipDimension Dimension,
    int Amount,
    string Reason,
    DateOnly Date,
    string? RelatedEventId = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Relationship change reason is required.", nameof(Reason));
        }
    }
}
