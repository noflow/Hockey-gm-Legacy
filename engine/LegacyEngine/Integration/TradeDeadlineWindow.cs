namespace LegacyEngine.Integration;

public sealed record TradeDeadlineWindow(
    DateOnly DeadlineDate,
    int DaysRemaining,
    TradeDeadlineStatus Status,
    bool TradesAllowed,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Deadline window summary is required.", nameof(Summary));
        }
    }
}
