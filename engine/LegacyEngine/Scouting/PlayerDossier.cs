namespace LegacyEngine.Scouting;

public sealed record PlayerDossier(
    string PlayerId,
    string Overview,
    IReadOnlyList<string> Facts,
    IReadOnlyList<ScoutingReport> ScoutingReports,
    IReadOnlyList<string> Analytics,
    IReadOnlyList<string> Medical,
    IReadOnlyList<string> Character,
    IReadOnlyList<string> Relationships,
    IReadOnlyList<string> Career,
    IReadOnlyList<string> History,
    IReadOnlyList<string> GmNotebook)
{
    public static PlayerDossier CreateEmpty(string playerId, string overview) =>
        new(
            PlayerId: playerId,
            Overview: overview,
            Facts: Array.Empty<string>(),
            ScoutingReports: Array.Empty<ScoutingReport>(),
            Analytics: Array.Empty<string>(),
            Medical: Array.Empty<string>(),
            Character: Array.Empty<string>(),
            Relationships: Array.Empty<string>(),
            Career: Array.Empty<string>(),
            History: Array.Empty<string>(),
            GmNotebook: Array.Empty<string>());

    public PlayerDossier AddReport(ScoutingReport report)
    {
        report.Validate();

        if (report.PlayerId != PlayerId)
        {
            throw new ArgumentException("Report player id must match dossier player id.", nameof(report));
        }

        return this with { ScoutingReports = ScoutingReports.Append(report).ToArray() };
    }

    public PlayerDossier AddGmNotebookEntry(string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("GM notebook entry is required.", nameof(note));
        }

        return this with { GmNotebook = GmNotebook.Append(note).ToArray() };
    }
}
