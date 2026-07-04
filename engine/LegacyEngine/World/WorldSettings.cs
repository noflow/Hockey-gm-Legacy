namespace LegacyEngine.World;

public sealed record WorldSettings(
    int SeasonStartMonth = 9,
    int SeasonStartDay = 1,
    string TimeZoneId = "UTC")
{
    public void Validate()
    {
        if (SeasonStartMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonStartMonth), "Season start month must be between 1 and 12.");
        }

        if (SeasonStartDay < 1 || SeasonStartDay > DateTime.DaysInMonth(2000, SeasonStartMonth))
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonStartDay), "Season start day is not valid for the configured month.");
        }

        if (string.IsNullOrWhiteSpace(TimeZoneId))
        {
            throw new ArgumentException("Time zone id is required.", nameof(TimeZoneId));
        }
    }

    public int GetSeasonYear(WorldDate date)
    {
        Validate();

        var seasonStart = new DateOnly(date.Year, SeasonStartMonth, SeasonStartDay);
        return date.Value >= seasonStart ? date.Year : date.Year - 1;
    }
}
