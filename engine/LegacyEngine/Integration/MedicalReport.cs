namespace LegacyEngine.Integration;

public sealed record MedicalReport(
    string ReportId,
    DateOnly CreatedOn,
    string PersonId,
    string PlayerName,
    string Position,
    HealthStatus HealthStatus,
    ConditioningStatus ConditioningStatus,
    string ExpectedReturn,
    string WhyItMatters,
    string StaffComment,
    string ReturnRecommendation,
    IReadOnlyList<ReturnToPlayOption> AvailableOptions)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReportId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Position)
            || string.IsNullOrWhiteSpace(ExpectedReturn)
            || string.IsNullOrWhiteSpace(WhyItMatters)
            || string.IsNullOrWhiteSpace(StaffComment)
            || string.IsNullOrWhiteSpace(ReturnRecommendation))
        {
            throw new ArgumentException("Medical report requires identity, explanation, and recommendation text.");
        }

        if (AvailableOptions.Count == 0)
        {
            throw new ArgumentException("Medical report requires available return-to-play options.", nameof(AvailableOptions));
        }
    }
}
