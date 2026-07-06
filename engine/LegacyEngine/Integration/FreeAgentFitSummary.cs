namespace LegacyEngine.Integration;

public sealed record FreeAgentFitSummary(
    string RosterNeed,
    string BudgetImpact,
    string StaffRecommendation,
    string RiskSummary,
    int FitScore)
{
    public void Validate()
    {
        if (FitScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(FitScore), "Free agent fit score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(RosterNeed)
            || string.IsNullOrWhiteSpace(BudgetImpact)
            || string.IsNullOrWhiteSpace(StaffRecommendation)
            || string.IsNullOrWhiteSpace(RiskSummary))
        {
            throw new ArgumentException("Free agent fit summary requires readable context fields.");
        }
    }
}
