namespace LegacyEngine.Integration;

public sealed record OrganizationSeasonHistory(
    int SeasonYear,
    string OrganizationId,
    string OrganizationName,
    string Record,
    string PlayoffResult,
    string DraftClassSummary,
    string NotablePlayers,
    string StaffHistorySummary,
    string OwnerChanges,
    string Championships,
    string Summary)
{
    public void Validate()
    {
        if (SeasonYear < 1
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Record)
            || string.IsNullOrWhiteSpace(PlayoffResult)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Organization season history requires year, organization, record, and summary.");
        }
    }
}
