namespace LegacyEngine.World;

public sealed record WorldId(string Value)
{
    public static WorldId New() => new($"world-{Guid.NewGuid():N}");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException("World id is required.", nameof(Value));
        }
    }
}
