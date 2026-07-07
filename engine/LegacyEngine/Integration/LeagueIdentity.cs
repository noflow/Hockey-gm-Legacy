namespace LegacyEngine.Integration;

public sealed record LeagueIdentity(
    string LeagueId,
    string Name,
    string ShortName,
    string Description,
    string Difficulty,
    IReadOnlyList<string> PrimaryGameplayFocus,
    string CurrentChampion,
    string HistorySummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeagueId)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(ShortName)
            || string.IsNullOrWhiteSpace(Description)
            || string.IsNullOrWhiteSpace(Difficulty)
            || string.IsNullOrWhiteSpace(CurrentChampion)
            || string.IsNullOrWhiteSpace(HistorySummary)
            || PrimaryGameplayFocus.Count == 0)
        {
            throw new ArgumentException("League identity requires id, name, description, difficulty, focus, champion, and history.");
        }
    }
}
