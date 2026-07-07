namespace LegacyEngine.Integration;

public sealed record LeagueAiReport(
    IReadOnlyList<OrganizationLeagueProfile> Profiles,
    IReadOnlyList<LeagueTransaction> LeagueNews,
    IReadOnlyList<string> HistoryNotes)
{
    public void Validate()
    {
        if (Profiles.Count == 0)
        {
            throw new ArgumentException("League AI report requires organization profiles.", nameof(Profiles));
        }

        foreach (var profile in Profiles)
        {
            profile.Validate();
        }

        foreach (var news in LeagueNews)
        {
            news.Validate();
        }

        if (HistoryNotes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("League AI history notes cannot be blank.", nameof(HistoryNotes));
        }
    }
}
