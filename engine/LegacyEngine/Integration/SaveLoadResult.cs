namespace LegacyEngine.Integration;

public sealed record SaveLoadResult(
    bool Success,
    string Message,
    string? FilePath = null,
    SaveGame? SaveGame = null,
    EngineRegistry? Registry = null,
    string? CompatibilityWarning = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Save/load result message is required.", nameof(Message));
        }

        if (Success && SaveGame is null)
        {
            throw new ArgumentException("Successful save/load results must include a save game.", nameof(SaveGame));
        }
    }
}
