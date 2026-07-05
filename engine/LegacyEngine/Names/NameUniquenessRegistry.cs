namespace LegacyEngine.Names;

public sealed class NameUniquenessRegistry
{
    private readonly Dictionary<string, HashSet<string>> _namesByScope = new(StringComparer.OrdinalIgnoreCase);

    public bool IsUsed(string scope, string displayName) =>
        _namesByScope.TryGetValue(NormalizeScope(scope), out var names)
        && names.Contains(NormalizeName(displayName));

    public bool TryRegister(string scope, GeneratedName name)
    {
        name.Validate();
        var names = NamesFor(scope);
        return names.Add(NormalizeName(name.DisplayName));
    }

    public void RegisterExisting(string scope, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        NamesFor(scope).Add(NormalizeName(displayName));
    }

    public int Count(string scope) =>
        _namesByScope.TryGetValue(NormalizeScope(scope), out var names) ? names.Count : 0;

    private HashSet<string> NamesFor(string scope)
    {
        var normalizedScope = NormalizeScope(scope);
        if (!_namesByScope.TryGetValue(normalizedScope, out var names))
        {
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _namesByScope[normalizedScope] = names;
        }

        return names;
    }

    private static string NormalizeScope(string scope) =>
        string.IsNullOrWhiteSpace(scope) ? "global" : scope.Trim();

    private static string NormalizeName(string displayName) => displayName.Trim();
}
