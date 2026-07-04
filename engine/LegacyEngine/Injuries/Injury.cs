namespace LegacyEngine.Injuries;

public sealed record Injury(
    string InjuryId,
    string PersonId,
    DateOnly InjuryDate,
    InjuryBodyPart BodyPart,
    InjuryType InjuryType,
    InjurySeverity Severity,
    DateOnly ExpectedReturnDate,
    DateOnly? ActualReturnDate,
    int GamesMissed,
    InjuryStatus Status,
    int LongTermImpact,
    int RecurrenceRisk,
    int RecoveryProgress,
    InjuryRecoveryPlan RecoveryPlan)
{
    public bool IsActive => Status is InjuryStatus.Active or InjuryStatus.Recovering or InjuryStatus.ReAggravated or InjuryStatus.CareerThreatening;

    public int DevelopmentPenalty => Math.Clamp(SeverityPenalty() + (RecurrenceRisk / 4) + (LongTermImpact / 2), 0, 100);

    public bool ShouldBeConsideredUnavailableForRoster => IsActive;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InjuryId))
        {
            throw new ArgumentException("Injury id is required.", nameof(InjuryId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        if (ExpectedReturnDate < InjuryDate)
        {
            throw new ArgumentException("Expected return date cannot be before injury date.", nameof(ExpectedReturnDate));
        }

        if (ActualReturnDate is not null && ActualReturnDate < InjuryDate)
        {
            throw new ArgumentException("Actual return date cannot be before injury date.", nameof(ActualReturnDate));
        }

        if (GamesMissed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(GamesMissed), "Games missed cannot be negative.");
        }

        InjuryRiskProfile.ValidateScore(LongTermImpact, nameof(LongTermImpact));
        InjuryRiskProfile.ValidateScore(RecurrenceRisk, nameof(RecurrenceRisk));
        InjuryRiskProfile.ValidateScore(RecoveryProgress, nameof(RecoveryProgress));
        RecoveryPlan.Validate();
    }

    private int SeverityPenalty() => Severity switch
    {
        InjurySeverity.Minor => 5,
        InjurySeverity.Moderate => 15,
        InjurySeverity.Major => 30,
        InjurySeverity.Severe => 45,
        InjurySeverity.CareerThreatening => 70,
        _ => 0
    };
}
