namespace LegacyEngine.Injuries;

public sealed record InjuryRecoveryUpdate(
    DateOnly Date,
    int RecoveryProgressDelta,
    int GamesMissedIncrease = 0,
    InjuryStatus? Status = null,
    int RecurrenceRiskDelta = 0,
    int LongTermImpactDelta = 0,
    string? Notes = null)
{
    public void Validate()
    {
        if (RecoveryProgressDelta is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(RecoveryProgressDelta), "Recovery progress delta must be between -100 and 100.");
        }

        if (GamesMissedIncrease < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(GamesMissedIncrease), "Games missed increase cannot be negative.");
        }

        if (RecurrenceRiskDelta is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(RecurrenceRiskDelta), "Recurrence risk delta must be between -100 and 100.");
        }

        if (LongTermImpactDelta is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(LongTermImpactDelta), "Long-term impact delta must be between -100 and 100.");
        }
    }
}
