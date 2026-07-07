using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed record ScoutingIntelligenceReport(
    string ReportId,
    string PlayerId,
    string PlayerName,
    string ScoutId,
    string ScoutName,
    string ScoutRole,
    DateOnly CreatedOn,
    ScoutingReportSource Source,
    ScoutingViewingType ViewingType,
    ScoutingTournamentType? Tournament,
    ScoutingRegionFocus Region,
    int Viewings,
    ScoutingConfidenceLevel Confidence,
    string ConfidenceStars,
    string CurrentPicture,
    string FutureProjection,
    string Recommendation,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Concerns,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<ScoutPersonalityTrait> ScoutTraits,
    string WorkloadNote,
    string BudgetNote)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReportId)
            || string.IsNullOrWhiteSpace(PlayerId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(ScoutId)
            || string.IsNullOrWhiteSpace(ScoutName)
            || string.IsNullOrWhiteSpace(ScoutRole)
            || string.IsNullOrWhiteSpace(ConfidenceStars)
            || string.IsNullOrWhiteSpace(CurrentPicture)
            || string.IsNullOrWhiteSpace(FutureProjection)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(WorkloadNote)
            || string.IsNullOrWhiteSpace(BudgetNote))
        {
            throw new ArgumentException("Scouting intelligence report requires readable scouting information.");
        }

        if (Viewings <= 0 || Evidence.Count == 0 || Concerns.Count == 0 || Unknowns.Count == 0 || ScoutTraits.Count == 0)
        {
            throw new ArgumentException("Scouting intelligence report requires viewings, evidence, concerns, unknowns, and scout traits.");
        }

        var joined = string.Join(" ", Evidence.Concat(Concerns).Concat(Unknowns).Append(CurrentPicture).Append(FutureProjection).Append(Recommendation));
        if (joined.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase)
            || joined.Contains("Potential =", StringComparison.OrdinalIgnoreCase)
            || joined.Contains("overall rating", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Scouting intelligence report cannot expose hidden ratings.");
        }
    }
}
