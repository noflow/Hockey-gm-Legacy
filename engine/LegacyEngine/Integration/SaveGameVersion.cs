namespace LegacyEngine.Integration;

public sealed record SaveGameVersion(string SaveFormatVersion, string GameVersionLabel)
{
    public const string CurrentSaveFormatVersion = "alpha-save-1";
    public const string CurrentGameVersionLabel = "Alpha 3.5";

    public static SaveGameVersion Current { get; } = new(CurrentSaveFormatVersion, CurrentGameVersionLabel);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SaveFormatVersion) || string.IsNullOrWhiteSpace(GameVersionLabel))
        {
            throw new ArgumentException("Save game version requires format and game labels.");
        }
    }
}
