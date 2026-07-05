namespace LegacyEngine.Names;

public sealed record NameGenerationSettings(
    int Seed = 2301,
    IReadOnlyList<NameOrigin>? Origins = null,
    bool PreventDuplicateFullNamesWithinScope = true,
    bool AllowRareDuplicateNames = true,
    double RareDuplicateChance = 0.01,
    int MaxAttempts = 500)
{
    public IReadOnlyList<NameOrigin> ActiveOrigins =>
        Origins is { Count: > 0 } ? Origins : Enum.GetValues<NameOrigin>();

    public static NameGenerationSettings CreateDefault(int seed = 2301) => new(seed);

    public NameGenerationSettings WithOrigins(params NameOrigin[] origins) =>
        this with { Origins = origins };

    public void Validate()
    {
        if (MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "Name generation must allow at least one attempt.");
        }

        if (RareDuplicateChance < 0 || RareDuplicateChance > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RareDuplicateChance), "Rare duplicate chance must be between 0 and 1.");
        }

        if (ActiveOrigins.Count == 0)
        {
            throw new ArgumentException("At least one name origin is required.", nameof(Origins));
        }
    }
}
