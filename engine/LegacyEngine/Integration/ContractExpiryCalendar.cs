using LegacyEngine.Contracts;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public static class ContractExpiryCalendar
{
    public const int DefaultDaysBeforeDraft = 3;
    public const int MinimumDaysToFirstExpiry = 90;

    public static ContractTerm TermForYears(DateOnly startDate, SeasonSettings settings, int years, int daysBeforeDraft = DefaultDaysBeforeDraft) =>
        new(startDate, ExpiryDateForTerm(startDate, settings, years, daysBeforeDraft));

    public static DateOnly ExpiryDateForTerm(DateOnly startDate, SeasonSettings settings, int years, int daysBeforeDraft = DefaultDaysBeforeDraft)
    {
        if (years <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(years), "Contract term length must be at least one year.");
        }

        var firstExpirySeasonYear = NextExpirySeasonYearForTerm(startDate, settings, daysBeforeDraft);
        return CommonExpiryDate(firstExpirySeasonYear + years - 1, settings, daysBeforeDraft);
    }

    public static DateOnly CommonExpiryDate(int seasonYear, SeasonSettings settings, int daysBeforeDraft = DefaultDaysBeforeDraft)
    {
        settings.Validate();
        if (daysBeforeDraft < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysBeforeDraft), "Days before draft cannot be negative.");
        }

        var draft = SeasonCalendar.Build(seasonYear, settings)
            .Milestones
            .Single(milestone => milestone.Type == SeasonMilestoneType.Draft)
            .Date
            .Value;
        return draft.AddDays(-daysBeforeDraft);
    }

    private static int NextExpirySeasonYearForTerm(DateOnly date, SeasonSettings settings, int daysBeforeDraft)
    {
        for (var seasonYear = date.Year - 2; seasonYear <= date.Year + 12; seasonYear++)
        {
            var expiry = CommonExpiryDate(seasonYear, settings, daysBeforeDraft);
            if (expiry >= date && expiry.DayNumber - date.DayNumber >= MinimumDaysToFirstExpiry)
            {
                return seasonYear;
            }
        }

        throw new InvalidOperationException("Could not find the next contract expiry season year.");
    }
}
