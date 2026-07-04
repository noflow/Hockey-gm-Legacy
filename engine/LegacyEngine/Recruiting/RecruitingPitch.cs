namespace LegacyEngine.Recruiting;

public sealed record RecruitingPitch(
    string PitchId,
    string OrganizationId,
    DateOnly Date,
    IReadOnlyDictionary<RecruitPriority, int> PriorityFits,
    string Message)
{
    public int AverageFit => PriorityFits.Count == 0
        ? 0
        : (int)Math.Round(PriorityFits.Values.Average());

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PitchId))
        {
            throw new ArgumentException("Pitch id is required.", nameof(PitchId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Pitch message is required.", nameof(Message));
        }

        if (PriorityFits.Count == 0)
        {
            throw new ArgumentException("Recruiting pitch must include at least one priority fit.", nameof(PriorityFits));
        }

        foreach (var value in PriorityFits.Values)
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(PriorityFits), "Priority fit values must be between 0 and 100.");
            }
        }
    }
}
