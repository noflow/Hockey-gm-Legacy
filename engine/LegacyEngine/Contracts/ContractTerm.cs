namespace LegacyEngine.Contracts;

public sealed record ContractTerm(
    DateOnly StartDate,
    DateOnly EndDate)
{
    public int LengthInDays => EndDate.DayNumber - StartDate.DayNumber + 1;

    public void Validate()
    {
        if (EndDate < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(EndDate), "Contract end date cannot be before start date.");
        }
    }
}
