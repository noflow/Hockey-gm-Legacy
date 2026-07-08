using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record WhereAreTheyNowRecord(
    string PersonId,
    string PlayerName,
    int DraftYear,
    int Round,
    int Pick,
    RosterPosition Position,
    string CurrentTeamOrStatus,
    string CurrentRole,
    string LatestStats,
    string DevelopmentTrend,
    string InjuryStatus,
    string StaffOpinion,
    DraftPickOutcome OutcomeSoFar)
{
    public string DraftClassContext { get; init; } = "Draft class context not recorded.";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(CurrentTeamOrStatus)
            || string.IsNullOrWhiteSpace(CurrentRole)
            || string.IsNullOrWhiteSpace(LatestStats)
            || string.IsNullOrWhiteSpace(DevelopmentTrend)
            || string.IsNullOrWhiteSpace(InjuryStatus)
            || string.IsNullOrWhiteSpace(StaffOpinion)
            || string.IsNullOrWhiteSpace(DraftClassContext))
        {
            throw new ArgumentException("Where Are They Now record requires readable player context.");
        }
    }
}
