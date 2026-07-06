namespace LegacyEngine.Integration;

public sealed record DraftHistoryRecord(
    int SeasonYear,
    int Round,
    int Pick,
    string ProspectPersonId,
    string ProspectName,
    string OrganizationId,
    string OrganizationName,
    string OutcomeSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(OutcomeSummary))
        {
            throw new ArgumentException("Draft history requires prospect, organization, and outcome text.");
        }

        if (Round <= 0 || Pick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Pick), "Draft history round and pick must be positive.");
        }
    }
}
