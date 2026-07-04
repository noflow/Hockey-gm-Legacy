namespace LegacyEngine.Draft;

public sealed record DraftSelection(
    string ProspectPersonId,
    DateTimeOffset SelectedAt)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId))
        {
            throw new ArgumentException("Prospect person id is required.", nameof(ProspectPersonId));
        }
    }
}
