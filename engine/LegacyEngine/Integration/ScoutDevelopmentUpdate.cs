namespace LegacyEngine.Integration;

public sealed record ScoutDevelopmentUpdate(
    string ScoutId,
    string ScoutName,
    int ExperienceGained,
    string ReputationChange,
    string NewSpecialization,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScoutId)
            || string.IsNullOrWhiteSpace(ScoutName)
            || string.IsNullOrWhiteSpace(ReputationChange)
            || string.IsNullOrWhiteSpace(NewSpecialization)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Scout development update requires readable scout information.");
        }

        if (ExperienceGained < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ExperienceGained), "Scout experience gained cannot be negative.");
        }
    }
}
