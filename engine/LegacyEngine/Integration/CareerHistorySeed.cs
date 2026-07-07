namespace LegacyEngine.Integration;

public sealed record CareerHistorySeed(
    CareerTimeline CareerTimeline,
    IReadOnlyList<DraftPickHistory> DraftPickHistory,
    IReadOnlyList<DraftClassHistory> DraftClassHistory,
    IReadOnlyList<StaffCareerHistory> StaffCareerHistory,
    GmCareerHistory GmCareerHistory,
    IReadOnlyList<OrganizationSeasonHistory> OrganizationSeasonHistory,
    IReadOnlyList<TransactionHistoryRecord> TransactionHistory)
{
    public void Validate()
    {
        CareerTimeline.Validate();
        foreach (var pick in DraftPickHistory)
        {
            pick.Validate();
        }

        foreach (var draftClass in DraftClassHistory)
        {
            draftClass.Validate();
        }

        foreach (var staff in StaffCareerHistory)
        {
            staff.Validate();
        }

        GmCareerHistory.Validate();
        foreach (var season in OrganizationSeasonHistory)
        {
            season.Validate();
        }

        foreach (var transaction in TransactionHistory)
        {
            transaction.Validate();
        }
    }
}
