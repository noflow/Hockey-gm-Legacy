namespace LegacyEngine.Integration;

public sealed record TradeDeadlineSettings(
    DateOnly DeadlineDate,
    bool AllowPostDeadlineTrades,
    int ApproachingWindowDays = 30,
    int DeadlineWeekDays = 7)
{
    public void Validate()
    {
        if (ApproachingWindowDays < DeadlineWeekDays || DeadlineWeekDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ApproachingWindowDays), "Deadline windows must be ordered and positive.");
        }
    }
}
