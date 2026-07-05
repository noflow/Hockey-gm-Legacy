using LegacyEngine.RuleEngine;

namespace LegacyEngine.Seasons;

/// <summary>
/// The timing configuration for a season: where the season starts and how many days
/// after that start each milestone falls. Timing is data, not hardcoded engine
/// logic: <see cref="FromRulebook"/> reads it from a rulebook's season rules, and
/// <see cref="Default"/> is only a neutral fallback when no rules are supplied.
/// </summary>
public sealed record SeasonSettings(
    int SeasonStartMonth,
    int SeasonStartDay,
    IReadOnlyDictionary<SeasonMilestoneType, int> MilestoneOffsets)
{
    public static SeasonSettings Default { get; } = new(9, 1, DefaultOffsets());

    public static SeasonSettings FromRulebook(Rulebook rulebook)
    {
        ArgumentNullException.ThrowIfNull(rulebook);

        if (rulebook.SeasonRules is not { } rules)
        {
            return Default;
        }

        var settings = new SeasonSettings(
            rules.SeasonStartMonth,
            rules.SeasonStartDay,
            new Dictionary<SeasonMilestoneType, int>
            {
                [SeasonMilestoneType.TrainingCampOpens] = rules.TrainingCampOffsetDays,
                [SeasonMilestoneType.SeasonBegins] = rules.SeasonBeginOffsetDays,
                [SeasonMilestoneType.TradeDeadline] = rules.TradeDeadlineOffsetDays,
                [SeasonMilestoneType.PlayoffsBegin] = rules.PlayoffsBeginOffsetDays,
                [SeasonMilestoneType.Championship] = rules.ChampionshipOffsetDays,
                [SeasonMilestoneType.Awards] = rules.AwardsOffsetDays,
                [SeasonMilestoneType.RecruitingOpens] = rules.RecruitingOpenOffsetDays,
                [SeasonMilestoneType.RecruitingCloses] = rules.RecruitingCloseOffsetDays,
                [SeasonMilestoneType.DraftLottery] = rules.DraftLotteryOffsetDays,
                [SeasonMilestoneType.Draft] = rules.DraftOffsetDays,
                [SeasonMilestoneType.FreeAgencyOpens] = rules.FreeAgencyOpenOffsetDays,
                [SeasonMilestoneType.FreeAgencyEnds] = rules.FreeAgencyCloseOffsetDays
            });

        settings.Validate();
        return settings;
    }

    public int OffsetFor(SeasonMilestoneType type) =>
        MilestoneOffsets.TryGetValue(type, out var offset)
            ? offset
            : throw new ArgumentException($"Season settings are missing an offset for '{type}'.", nameof(type));

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

        foreach (var type in SeasonMilestones.InScheduleOrder)
        {
            if (!MilestoneOffsets.TryGetValue(type, out var offset))
            {
                throw new ArgumentException($"Season settings are missing an offset for '{type}'.", nameof(MilestoneOffsets));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MilestoneOffsets), $"Milestone offset for '{type}' cannot be negative.");
            }
        }
    }

    private static Dictionary<SeasonMilestoneType, int> DefaultOffsets() => new()
    {
        [SeasonMilestoneType.TrainingCampOpens] = 0,
        [SeasonMilestoneType.SeasonBegins] = 21,
        [SeasonMilestoneType.TradeDeadline] = 140,
        [SeasonMilestoneType.PlayoffsBegin] = 210,
        [SeasonMilestoneType.Championship] = 250,
        [SeasonMilestoneType.Awards] = 258,
        [SeasonMilestoneType.RecruitingOpens] = 265,
        [SeasonMilestoneType.RecruitingCloses] = 290,
        [SeasonMilestoneType.DraftLottery] = 292,
        [SeasonMilestoneType.Draft] = 300,
        [SeasonMilestoneType.FreeAgencyOpens] = 310,
        [SeasonMilestoneType.FreeAgencyEnds] = 330
    };
}
