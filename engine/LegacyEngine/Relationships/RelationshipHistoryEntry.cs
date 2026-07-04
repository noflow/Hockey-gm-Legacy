namespace LegacyEngine.Relationships;

public sealed record RelationshipHistoryEntry(
    string EntryId,
    string RelationshipId,
    DateOnly Date,
    string Title,
    string Description,
    RelationshipDimension DimensionChanged,
    int AmountChanged,
    int OldValue,
    int NewValue,
    string? RelatedEventId)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EntryId))
        {
            throw new ArgumentException("Relationship history entry id is required.", nameof(EntryId));
        }

        if (string.IsNullOrWhiteSpace(RelationshipId))
        {
            throw new ArgumentException("Relationship id is required.", nameof(RelationshipId));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Relationship history entry title is required.", nameof(Title));
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Relationship history entry description is required.", nameof(Description));
        }

        ValidateScore(OldValue, nameof(OldValue));
        ValidateScore(NewValue, nameof(NewValue));
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Relationship history values must be between 0 and 100.");
        }
    }
}
