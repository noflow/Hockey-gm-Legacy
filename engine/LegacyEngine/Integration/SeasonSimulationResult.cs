namespace LegacyEngine.Integration;

public sealed record SeasonSimulationResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    IReadOnlyList<ScheduledGame> SimulatedGames,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        foreach (var game in SimulatedGames)
        {
            game.Validate();
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Season simulation summary is required.", nameof(Summary));
        }
    }
}
