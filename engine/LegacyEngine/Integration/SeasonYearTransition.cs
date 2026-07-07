namespace LegacyEngine.Integration;

public sealed record SeasonYearTransition(
    int FromSeasonYear,
    int ToSeasonYear,
    string FromSeasonId,
    string ToSeasonId,
    DateOnly TransitionDate,
    DateOnly NextDraftDate,
    string Summary)
{
    public void Validate()
    {
        if (FromSeasonYear < 1 || ToSeasonYear < 1 || ToSeasonYear <= FromSeasonYear)
        {
            throw new ArgumentOutOfRangeException(nameof(ToSeasonYear), "Season transition must move forward to a later year.");
        }

        if (string.IsNullOrWhiteSpace(FromSeasonId)
            || string.IsNullOrWhiteSpace(ToSeasonId)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Season transition requires season ids and summary.");
        }
    }
}
