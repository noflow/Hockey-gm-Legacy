using LegacyEngine.Scouting;

namespace LegacyEngine.Draft;

public sealed record DraftBoardEntry(
    string ProspectPersonId,
    int Rank,
    string? ScoutingReportId,
    ScoutingConfidenceLevel? ScoutingConfidence,
    string ProjectionText,
    bool IsStarred = false,
    string PersonalNotes = "",
    string AnalyticsSummary = "",
    DraftProspectBio? Bio = null,
    string RiskSummary = "",
    string ClassContextNote = "")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId))
        {
            throw new ArgumentException("Prospect person id is required.", nameof(ProspectPersonId));
        }

        if (Rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Rank), "Draft board rank must be positive.");
        }

        if (string.IsNullOrWhiteSpace(ProjectionText))
        {
            throw new ArgumentException("Projection text is required.", nameof(ProjectionText));
        }

        Bio?.Validate();
        if (RiskSummary is null || ClassContextNote is null)
        {
            throw new ArgumentException("Draft board risk and class context notes cannot be null.");
        }
    }
}
