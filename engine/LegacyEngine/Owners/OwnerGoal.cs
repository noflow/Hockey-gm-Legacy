namespace LegacyEngine.Owners;

public sealed record OwnerGoal(
    OwnerGoalType GoalType,
    int Priority,
    string Description)
{
    public void Validate()
    {
        if (Priority is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(Priority), "Owner goal priority must be between 1 and 5.");
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Owner goal description is required.", nameof(Description));
        }
    }
}
