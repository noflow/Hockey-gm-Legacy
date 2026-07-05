namespace LegacyEngine.Integration;

public sealed record SeasonReadinessReport(
    bool IsReady,
    bool CanBeginSeason,
    string RosterStatus,
    OpeningRosterReport RosterReport,
    IReadOnlyList<OpeningChecklistItem> ChecklistItems,
    string OrganizationHealth,
    string OwnerSatisfaction,
    string OwnerReview,
    string HeadCoachSummary,
    string HeadScoutSummary,
    string StaffRecommendations,
    string TrainingCampStatus,
    string BlockedReason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RosterStatus))
        {
            throw new ArgumentException("Roster status is required.", nameof(RosterStatus));
        }

        foreach (var item in ChecklistItems)
        {
            item.Validate();
        }
    }
}
