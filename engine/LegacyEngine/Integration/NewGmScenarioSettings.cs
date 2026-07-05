using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed record NewGmScenarioSettings(
    string WorldName = "Hockey GM Legacy Alpha 1.6",
    string LeagueId = "junior-league-alpha",
    string SeasonId = "season-junior-2025",
    int SeasonYear = 2025,
    string OrganizationId = "org-prairie-falcons",
    string RosterId = "roster-prairie-falcons-2026",
    string DraftBoardId = "draft-board-prairie-falcons-2026",
    string PlayerGmPersonId = "person-player-gm-001")
{
    public SeasonSettings SeasonSettings { get; init; } = CreateDefaultSeasonSettings();

    public GmProfileCreationSettings? GmCreationSettings { get; init; }

    public static SeasonSettings CreateDefaultSeasonSettings() =>
        new(
            SeasonStartMonth: 9,
            SeasonStartDay: 1,
            MilestoneOffsets: new Dictionary<SeasonMilestoneType, int>
            {
                [SeasonMilestoneType.TrainingCampOpens] = 0,
                [SeasonMilestoneType.SeasonBegins] = 21,
                [SeasonMilestoneType.TradeDeadline] = 140,
                [SeasonMilestoneType.PlayoffsBegin] = 210,
                [SeasonMilestoneType.Championship] = 250,
                [SeasonMilestoneType.Awards] = 258,
                [SeasonMilestoneType.RecruitingOpens] = 265,
                [SeasonMilestoneType.RecruitingCloses] = 276,
                [SeasonMilestoneType.DraftLottery] = 282,
                [SeasonMilestoneType.Draft] = 301,
                [SeasonMilestoneType.FreeAgencyOpens] = 310,
                [SeasonMilestoneType.FreeAgencyEnds] = 330
            });

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorldName))
        {
            throw new ArgumentException("World name is required.", nameof(WorldName));
        }

        if (string.IsNullOrWhiteSpace(LeagueId))
        {
            throw new ArgumentException("League id is required.", nameof(LeagueId));
        }

        if (string.IsNullOrWhiteSpace(SeasonId))
        {
            throw new ArgumentException("Season id is required.", nameof(SeasonId));
        }

        if (SeasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Season year must be positive.");
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (string.IsNullOrWhiteSpace(RosterId))
        {
            throw new ArgumentException("Roster id is required.", nameof(RosterId));
        }

        if (string.IsNullOrWhiteSpace(DraftBoardId))
        {
            throw new ArgumentException("Draft board id is required.", nameof(DraftBoardId));
        }

        if (string.IsNullOrWhiteSpace(PlayerGmPersonId))
        {
            throw new ArgumentException("Player GM person id is required.", nameof(PlayerGmPersonId));
        }

        SeasonSettings.Validate();
    }
}
