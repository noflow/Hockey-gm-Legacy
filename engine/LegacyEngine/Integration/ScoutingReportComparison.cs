namespace LegacyEngine.Integration;

public sealed record ScoutingReportComparison(
    string PlayerId,
    string PlayerName,
    IReadOnlyList<ScoutingIntelligenceReport> Reports,
    IReadOnlyList<string> Agreements,
    IReadOnlyList<string> Disagreements,
    string ConfidenceSummary,
    string RecommendationSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayerId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(ConfidenceSummary)
            || string.IsNullOrWhiteSpace(RecommendationSummary))
        {
            throw new ArgumentException("Scouting report comparison requires readable player and summary text.");
        }

        foreach (var report in Reports)
        {
            report.Validate();
        }

        if (Agreements.Count == 0 || Disagreements.Count == 0)
        {
            throw new ArgumentException("Scouting report comparison requires agreement and disagreement notes.");
        }
    }
}
