namespace LegacyEngine.Injuries;

public sealed record InjuryRecoveryPlan(
    string InjuryId,
    DateOnly StartDate,
    DateOnly ExpectedReturnDate,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InjuryId))
        {
            throw new ArgumentException("Injury id is required.", nameof(InjuryId));
        }

        if (ExpectedReturnDate < StartDate)
        {
            throw new ArgumentException("Expected return date cannot be before recovery start date.", nameof(ExpectedReturnDate));
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Recovery plan summary is required.", nameof(Summary));
        }
    }
}
