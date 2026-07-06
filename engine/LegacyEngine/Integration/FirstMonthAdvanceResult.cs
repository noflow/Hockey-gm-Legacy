namespace LegacyEngine.Integration;

public sealed record FirstMonthAdvanceResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    int DaysAdvanced,
    int ProcessedEventCount,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string StopReason,
    bool StoppedForAttention,
    MonthlyGmSummary? MonthlySummary = null)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        if (DaysAdvanced < 0 || ProcessedEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DaysAdvanced), "Advance counters cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(StopReason))
        {
            throw new ArgumentException("Advance result requires a stop reason.", nameof(StopReason));
        }

        MonthlySummary?.Validate();
    }
}
