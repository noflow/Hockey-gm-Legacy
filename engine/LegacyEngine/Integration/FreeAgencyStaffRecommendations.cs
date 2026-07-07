namespace LegacyEngine.Integration;

public sealed record FreeAgencyStaffRecommendations(
    string PersonId,
    string HeadCoach,
    string Scout,
    string Medical,
    string Owner,
    string AssistantGm)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(HeadCoach)
            || string.IsNullOrWhiteSpace(Scout)
            || string.IsNullOrWhiteSpace(Medical)
            || string.IsNullOrWhiteSpace(Owner)
            || string.IsNullOrWhiteSpace(AssistantGm))
        {
            throw new ArgumentException("Free agency staff recommendations require all recommendation text.");
        }
    }
}
