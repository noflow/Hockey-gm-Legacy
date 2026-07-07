namespace LegacyEngine.Integration;

public sealed record FreeAgencyWindow(
    DateOnly OpensOn,
    DateOnly ClosesOn,
    FreeAgencyPhase Phase)
{
    public int DaysOpen(DateOnly date) => Math.Max(0, date.DayNumber - OpensOn.DayNumber);

    public int DaysUntilClose(DateOnly date) => ClosesOn.DayNumber - date.DayNumber;

    public void Validate()
    {
        if (ClosesOn < OpensOn)
        {
            throw new ArgumentOutOfRangeException(nameof(ClosesOn), "Free agency close date cannot be before open date.");
        }
    }
}
