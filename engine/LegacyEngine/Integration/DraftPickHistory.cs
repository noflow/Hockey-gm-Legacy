using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed record DraftPickHistory(
    int Year,
    int Round,
    int OverallPick,
    string PlayerPersonId,
    string PlayerName,
    RosterPosition Position,
    string TeamDraftedFrom,
    string ScoutingProjectionAtDraft,
    ScoutingConfidenceLevel? ScoutConfidenceAtDraft,
    string GmNotesAtDraft,
    string CurrentStatus,
    int CareerGames,
    int CareerPoints,
    string GoaltendingStats,
    DraftPickOutcome Outcome,
    string OutcomeSummary)
{
    public int OriginalBoardRank { get; init; }

    public int ScoutBoardRank { get; init; }

    public int ConsensusBoardRank { get; init; }

    public string OverallEstimateAtDraft { get; init; } = "OVR estimate not recorded.";

    public string PotentialEstimateAtDraft { get; init; } = "POT estimate not recorded.";

    public string AttributeConfidenceAtDraft { get; init; } = "Attribute confidence not recorded.";

    public string ScoutNotesAtDraft { get; init; } = "Scout notes not recorded.";

    public string TeamNeedsAtDraft { get; init; } = "Team needs not recorded.";

    public string DraftClassContext { get; init; } = "Draft class context not recorded.";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayerPersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(TeamDraftedFrom)
            || string.IsNullOrWhiteSpace(ScoutingProjectionAtDraft)
            || string.IsNullOrWhiteSpace(GmNotesAtDraft)
            || string.IsNullOrWhiteSpace(CurrentStatus)
            || string.IsNullOrWhiteSpace(GoaltendingStats)
            || string.IsNullOrWhiteSpace(OutcomeSummary)
            || string.IsNullOrWhiteSpace(OverallEstimateAtDraft)
            || string.IsNullOrWhiteSpace(PotentialEstimateAtDraft)
            || string.IsNullOrWhiteSpace(AttributeConfidenceAtDraft)
            || string.IsNullOrWhiteSpace(ScoutNotesAtDraft)
            || string.IsNullOrWhiteSpace(TeamNeedsAtDraft)
            || string.IsNullOrWhiteSpace(DraftClassContext))
        {
            throw new ArgumentException("Draft pick history requires player identity and readable context.");
        }

        if (Year < 1 || Round < 1 || OverallPick < 1 || Position == RosterPosition.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(OverallPick), "Draft pick history requires valid year, pick, round, and known position.");
        }

        if (OriginalBoardRank < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OriginalBoardRank), "Original board rank cannot be negative.");
        }

        if (ScoutBoardRank < 0 || ConsensusBoardRank < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ScoutBoardRank), "Draft board ranks cannot be negative.");
        }
    }
}
