namespace LegacyEngine.Integration;

public sealed record StaffChemistryLink(
    string FromPersonId,
    string FromName,
    string ToPersonId,
    string ToName,
    int Trust,
    int Respect,
    int Confidence,
    string Summary,
    bool IsWarning)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FromPersonId)
            || string.IsNullOrWhiteSpace(FromName)
            || string.IsNullOrWhiteSpace(ToPersonId)
            || string.IsNullOrWhiteSpace(ToName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff chemistry link requires identity and summary text.");
        }
    }
}
