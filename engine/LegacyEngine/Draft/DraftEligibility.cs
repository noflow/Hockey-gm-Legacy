namespace LegacyEngine.Draft;

public sealed record DraftEligibility(
    string ProspectPersonId,
    bool IsEligible,
    string Reason = "")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId))
        {
            throw new ArgumentException("Prospect person id is required.", nameof(ProspectPersonId));
        }
    }
}
