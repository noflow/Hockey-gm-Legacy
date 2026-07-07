namespace LegacyEngine.Integration;

public sealed record GmCareerHistory(
    string GmPersonId,
    string GmName,
    string OrganizationId,
    string OrganizationName,
    DateOnly HireDate,
    int SeasonsCompleted,
    string Record,
    string PlayoffRecordPlaceholder,
    int DraftPicksMade,
    int TradesMade,
    int FreeAgentsSigned,
    int StaffHired,
    IReadOnlyList<string> OwnerConfidenceHistory,
    IReadOnlyList<string> CareerNotes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GmPersonId)
            || string.IsNullOrWhiteSpace(GmName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Record)
            || string.IsNullOrWhiteSpace(PlayoffRecordPlaceholder))
        {
            throw new ArgumentException("GM career history requires identity and record summaries.");
        }
    }
}
