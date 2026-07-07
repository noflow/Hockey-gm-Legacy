using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegacyEngine.Integration;

public sealed class SaveGameSerializer
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    public string Serialize(SaveGame saveGame)
    {
        ArgumentNullException.ThrowIfNull(saveGame);
        saveGame.Validate();
        return JsonSerializer.Serialize(saveGame, JsonOptions);
    }

    public SaveGame Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Save JSON is empty.", nameof(json));
        }

        var saveGame = JsonSerializer.Deserialize<SaveGame>(json, JsonOptions)
            ?? throw new InvalidOperationException("Save JSON did not contain a save game.");
        saveGame.Validate();
        return saveGame;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
