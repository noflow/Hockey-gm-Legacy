namespace LegacyEngine.Integration;

public sealed record SaveGameMetadata(
    SaveGameVersion Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSavedAt,
    string GmName,
    string TeamName,
    DateOnly CurrentDate,
    int SeasonYear,
    string FileDisplayName)
{
    public void Validate()
    {
        Version.Validate();
        if (string.IsNullOrWhiteSpace(GmName)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(FileDisplayName)
            || SeasonYear < 1)
        {
            throw new ArgumentException("Save metadata requires GM, team, season, and display name.");
        }
    }
}
