namespace LegacyEngine.Integration;

public sealed record PlayerDossierSection(
    string Title,
    IReadOnlyList<string> Lines)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Dossier section title is required.", nameof(Title));
        }

        foreach (var line in Lines)
        {
            if (line is null)
            {
                throw new ArgumentException("Dossier section lines cannot be null.", nameof(Lines));
            }
        }
    }
}
