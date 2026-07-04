namespace LegacyEngine.World;

public sealed record WorldClock(WorldDate CurrentDate)
{
    public WorldClock AdvanceDays(int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "World clock cannot advance a negative number of days.");
        }

        return this with { CurrentDate = CurrentDate.AddDays(days) };
    }
}
