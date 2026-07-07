using LegacyEngine.Events;
using LegacyEngine.RuleEngine;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class SaveGameService
{
    private readonly SaveGameSerializer _serializer = new();

    public string DefaultSaveFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HockeyGmLegacy",
            "Saves");

    public SaveGame CreateSave(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<InboxMessage> inboxMessages,
        IReadOnlyList<LeagueTransaction> leagueTransactions,
        IReadOnlyDictionary<string, ActionCenterStatus> actionCenterStatuses,
        BudgetSnapshot budgetSnapshot,
        string? fileDisplayName = null,
        SaveGameMetadata? previousMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(inboxMessages);
        ArgumentNullException.ThrowIfNull(leagueTransactions);
        ArgumentNullException.ThrowIfNull(actionCenterStatuses);
        ArgumentNullException.ThrowIfNull(budgetSnapshot);

        var now = DateTimeOffset.UtcNow;
        var metadata = new SaveGameMetadata(
            SaveGameVersion.Current,
            previousMetadata?.CreatedAt ?? now,
            now,
            scenario.GeneralManagerProfile.Person.Identity.DisplayName,
            scenario.Organization.Name,
            scenario.LeagueProfile.Identity.LeagueId,
            scenario.LeagueProfile.Identity.Name,
            scenario.LeagueProfile.Rulebook.RulebookId,
            scenario.CurrentDate,
            scenario.Season.Year,
            string.IsNullOrWhiteSpace(fileDisplayName)
                ? $"{scenario.GeneralManagerProfile.Person.Identity.DisplayName} - {scenario.Organization.Name}"
                : fileDisplayName.Trim());

        var save = new SaveGame(
            metadata,
            scenario,
            inboxMessages,
            leagueTransactions,
            new Dictionary<string, ActionCenterStatus>(actionCenterStatuses, StringComparer.Ordinal),
            budgetSnapshot);
        save.Validate();
        return save;
    }

    public SaveLoadResult SaveToFile(SaveGame saveGame, string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Fail("Save path is required.", filePath);
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, _serializer.Serialize(saveGame));
            return Ok("Save successful.", filePath, saveGame);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return Fail($"Save failed: {ex.Message}", filePath);
        }
    }

    public SaveLoadResult SaveCareer(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<InboxMessage> inboxMessages,
        IReadOnlyList<LeagueTransaction> leagueTransactions,
        IReadOnlyDictionary<string, ActionCenterStatus> actionCenterStatuses,
        BudgetSnapshot budgetSnapshot,
        string? filePath = null,
        string? fileDisplayName = null,
        SaveGameMetadata? previousMetadata = null)
    {
        var path = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(DefaultSaveFolder, $"{SafeFileName(scenario.GeneralManagerProfile.Person.Identity.DisplayName)}-{scenario.Season.Year}.json")
            : filePath;
        var save = CreateSave(scenario, inboxMessages, leagueTransactions, actionCenterStatuses, budgetSnapshot, fileDisplayName, previousMetadata);
        return SaveToFile(save, path);
    }

    public SaveLoadResult LoadFromFile(string filePath, Rulebook? rulebook = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Fail("Load path is required.", filePath);
            }

            if (!File.Exists(filePath))
            {
                return Fail($"Save file was not found: {filePath}", filePath);
            }

            var save = _serializer.Deserialize(File.ReadAllText(filePath));
            if (!string.Equals(save.Metadata.Version.SaveFormatVersion, SaveGameVersion.CurrentSaveFormatVersion, StringComparison.Ordinal))
            {
                return new SaveLoadResult(
                    Success: false,
                    Message: $"Save format '{save.Metadata.Version.SaveFormatVersion}' is not compatible with '{SaveGameVersion.CurrentSaveFormatVersion}'.",
                    FilePath: filePath,
                    SaveGame: save,
                    CompatibilityWarning: "Version mismatch. The save was read safely but was not loaded.");
            }

            var registry = RestoreRegistry(save.ScenarioSnapshot, rulebook);
            return Ok("Load successful.", filePath, save, registry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return Fail($"Load failed: {ex.Message}", filePath);
        }
    }

    public EngineRegistry RestoreRegistry(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();
        var worldEngine = new WorldEngine(scenario.AlphaSnapshot.WorldState, new EventEngine());
        return EngineRegistry.Create(worldEngine, rulebook ?? scenario.LeagueProfile.Rulebook);
    }

    private static SaveLoadResult Ok(string message, string? filePath, SaveGame saveGame, EngineRegistry? registry = null)
    {
        var result = new SaveLoadResult(true, message, filePath, saveGame, registry);
        result.Validate();
        return result;
    }

    private static SaveLoadResult Fail(string message, string? filePath) =>
        new(false, message, filePath);

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "hockey-gm-career" : safe;
    }
}
