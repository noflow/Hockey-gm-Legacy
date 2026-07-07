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
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayerPersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(TeamDraftedFrom)
            || string.IsNullOrWhiteSpace(ScoutingProjectionAtDraft)
            || string.IsNullOrWhiteSpace(GmNotesAtDraft)
            || string.IsNullOrWhiteSpace(CurrentStatus)
            || string.IsNullOrWhiteSpace(GoaltendingStats)
            || string.IsNullOrWhiteSpace(OutcomeSummary))
        {
            throw new ArgumentException("Draft pick history requires player identity and readable context.");
        }

        if (Year < 1 || Round < 1 || OverallPick < 1 || Position == RosterPosition.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(OverallPick), "Draft pick history requires valid year, pick, round, and known position.");
        }
    }
}
