namespace LegacyEngine.Names;

public sealed class NameGenerator
{
    private readonly IReadOnlyDictionary<NameOrigin, NamePool> _pools;
    private readonly Random _random;
    private readonly NameGenerationSettings _settings;

    public NameGenerator(NameGenerationSettings? settings = null, IReadOnlyDictionary<NameOrigin, NamePool>? pools = null)
    {
        _settings = settings ?? NameGenerationSettings.CreateDefault();
        _settings.Validate();
        _pools = pools ?? NamePool.CreateDefaultPools();
        _random = new Random(_settings.Seed);
    }

    public GeneratedName Generate(NameUniquenessRegistry registry, string scope, params NameOrigin[] origins)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var activeOrigins = origins.Length > 0 ? origins : _settings.ActiveOrigins.ToArray();
        var allowDuplicateNow = _settings.AllowRareDuplicateNames && _random.NextDouble() < _settings.RareDuplicateChance;

        for (var attempt = 0; attempt < _settings.MaxAttempts; attempt++)
        {
            var name = GenerateCandidate(activeOrigins);
            var isDuplicate = registry.IsUsed(scope, name.DisplayName);
            if (!_settings.PreventDuplicateFullNamesWithinScope || allowDuplicateNow || !isDuplicate)
            {
                registry.TryRegister(scope, name);
                return name;
            }
        }

        if (_settings.AllowRareDuplicateNames)
        {
            var fallback = GenerateCandidate(activeOrigins);
            registry.TryRegister(scope, fallback);
            return fallback;
        }

        throw new InvalidOperationException($"Unable to generate a unique name for scope '{scope}'.");
    }

    private GeneratedName GenerateCandidate(IReadOnlyList<NameOrigin> origins)
    {
        var origin = origins[_random.Next(origins.Count)];
        if (!_pools.TryGetValue(origin, out var pool))
        {
            throw new ArgumentException($"No name pool exists for origin '{origin}'.", nameof(origins));
        }

        var name = new GeneratedName(
            FirstName: Pick(pool.FirstNames),
            LastName: Pick(pool.LastNames),
            Origin: origin,
            Nationality: pool.Nationality,
            Birthplace: Pick(pool.Birthplaces));
        name.Validate();
        return name;
    }

    private string Pick(IReadOnlyList<string> values) => values[_random.Next(values.Count)];
}
