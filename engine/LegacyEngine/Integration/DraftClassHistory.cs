namespace LegacyEngine.Integration;

public sealed record DraftClassHistory(
    int Year,
    string OrganizationId,
    string OrganizationName,
    IReadOnlyList<DraftPickHistory> Picks,
    string Summary)
{
    public DraftClassProfile? ClassProfile { get; init; }

    public void Validate()
    {
        if (Year < 1
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft class history requires year, organization, and summary.");
        }

        ClassProfile?.Validate();
        foreach (var pick in Picks)
        {
            pick.Validate();
        }
    }
}
