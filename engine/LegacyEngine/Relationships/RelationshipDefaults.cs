namespace LegacyEngine.Relationships;

public sealed record RelationshipDefaults(
    int Trust,
    int Respect,
    int Confidence,
    int Loyalty,
    int Influence,
    int Friendship,
    int Rivalry)
{
    public static RelationshipDefaults Standard { get; } = new(
        Trust: 50,
        Respect: 50,
        Confidence: 50,
        Loyalty: 50,
        Influence: 50,
        Friendship: 0,
        Rivalry: 0);
}
