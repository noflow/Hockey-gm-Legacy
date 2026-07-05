using LegacyEngine.Events;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.World;

internal sealed class SeasonEngineTests
{
    private static readonly DateOnly DefaultStart = new(2026, 9, 1);

    public void SeasonCreation()
    {
        var season = new SeasonEngine().CreateSeason("season-2026", "league-1", 2026);

        season.Validate();
        Assert.Equal("season-2026", season.SeasonId);
        Assert.Equal("league-1", season.LeagueId);
        Assert.Equal(2026, season.Year);
        Assert.Equal(SeasonStatus.Upcoming, season.Status);
        Assert.Equal(12, season.Calendar.Milestones.Count);
    }

    public void PhaseChanges()
    {
        var engine = new SeasonEngine();
        var season = engine.CreateSeason("season-2026", "league-1", 2026);

        var result = engine.AdvanceTo(season, DefaultStart.AddDays(21));

        Assert.Equal(SeasonPhase.RegularSeason, result.Season.CurrentPhase);
        Assert.True(result.PhaseChanged, "Advancing into the season should change phase.");
    }

    public void DateAdvancement()
    {
        var engine = new SeasonEngine();
        var season = engine.CreateSeason("season-2026", "league-1", 2026);

        var result = engine.AdvanceDays(season, 10);

        // Season is created on the eve of training camp (start - 1 day).
        Assert.Equal(DefaultStart.AddDays(9), result.Season.CurrentDate.Value);
    }

    public void MilestoneDetection()
    {
        var engine = new SeasonEngine();
        var season = engine.CreateSeason("season-2026", "league-1", 2026);

        var result = engine.AdvanceTo(season, DefaultStart.AddDays(140));

        Assert.True(
            result.MilestonesReached.Any(milestone => milestone.Type == SeasonMilestoneType.TradeDeadline),
            "Advancing to the trade deadline date should detect the trade deadline milestone.");
        Assert.Equal(SeasonPhase.TradeDeadline, result.Season.CurrentPhase);
    }

    public void IndependentLeagueCalendars()
    {
        var engine = new SeasonEngine();
        var leagueA = engine.CreateSeason("season-a", "league-a", 2026);

        var customSettings = new SeasonSettings(
            SeasonStartMonth: 1,
            SeasonStartDay: 15,
            MilestoneOffsets: SeasonSettings.Default.MilestoneOffsets);
        var leagueB = engine.CreateSeason("season-b", "league-b", 2026, customSettings);

        Assert.False(
            leagueA.Calendar.SeasonStart.Value == leagueB.Calendar.SeasonStart.Value,
            "Independent leagues should have independent season starts.");

        var advancedA = engine.AdvanceTo(leagueA, DefaultStart.AddDays(21));

        // Advancing league A must not affect league B, which was never advanced.
        Assert.Equal(SeasonPhase.RegularSeason, advancedA.Season.CurrentPhase);
        Assert.Equal(SeasonStatus.Upcoming, leagueB.Status);
    }

    public void EventGeneration()
    {
        var eventEngine = new EventEngine();
        var engine = new SeasonEngine(eventEngine);
        var season = engine.CreateSeason("season-2026", "league-1", 2026);

        engine.AdvanceTo(season, DefaultStart.AddDays(330));

        var pending = eventEngine.Queue.PendingEvents;
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.SeasonCreated), "SeasonCreated event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.PhaseChanged), "PhaseChanged event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.MilestoneReached), "MilestoneReached event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.RecruitingOpened), "RecruitingOpened event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.RecruitingClosed), "RecruitingClosed event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.DraftOpened), "DraftOpened event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.DraftClosed), "DraftClosed event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.FreeAgencyOpened), "FreeAgencyOpened event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.FreeAgencyClosed), "FreeAgencyClosed event should be queued.");
    }

    public void RuleDrivenDates()
    {
        var rulebook = new Rulebook
        {
            RulebookId = "custom_season",
            LeagueType = "custom",
            Version = "1.0",
            SeasonRules = new SeasonRules
            {
                SeasonStartMonth = 10,
                SeasonStartDay = 1,
                TrainingCampOffsetDays = 0,
                SeasonBeginOffsetDays = 30,
                TradeDeadlineOffsetDays = 100,
                PlayoffsBeginOffsetDays = 180,
                ChampionshipOffsetDays = 210,
                AwardsOffsetDays = 215,
                RecruitingOpenOffsetDays = 220,
                RecruitingCloseOffsetDays = 240,
                DraftLotteryOffsetDays = 245,
                DraftOffsetDays = 250,
                FreeAgencyOpenOffsetDays = 260,
                FreeAgencyCloseOffsetDays = 280
            }
        };

        var season = new SeasonEngine().CreateSeason("season-2026", "league-1", 2026, rulebook: rulebook);

        Assert.Equal(10, season.Settings.SeasonStartMonth);
        Assert.Equal(new DateOnly(2026, 10, 1), season.Calendar.SeasonStart.Value);

        var tradeDeadline = season.Calendar.Milestones.Single(milestone => milestone.Type == SeasonMilestoneType.TradeDeadline);
        Assert.Equal(new DateOnly(2026, 10, 1).AddDays(100), tradeDeadline.Date.Value);
    }

    public void SeasonResultContents()
    {
        var engine = new SeasonEngine();
        var season = engine.CreateSeason("season-2026", "league-1", 2026);

        var result = engine.AdvanceTo(season, DefaultStart.AddDays(21));

        Assert.Equal(SeasonPhase.Offseason, result.PreviousPhase);
        Assert.Equal(SeasonPhase.RegularSeason, result.CurrentPhase);
        Assert.True(result.PhaseChanged, "SeasonResult should report the phase change.");
        Assert.Equal(DefaultStart.AddDays(21), result.CurrentDate.Value);
        Assert.True(result.MilestonesReached.Count >= 2, "SeasonResult should list milestones reached.");
        Assert.True(result.Transitions.Count >= 2, "SeasonResult should list phase transitions.");
        Assert.True(result.CreatedEvents.Count > 0, "SeasonResult should list created events.");
        Assert.True(result.Summary.Length > 0, "SeasonResult should include a summary.");
    }

    public void WorldEngineCanAskSeasonPhase()
    {
        var season = new SeasonEngine().CreateSeason("season-2026", "league-1", 2026);
        var world = WorldEngine.CreateWorld("Test World", DefaultStart.AddDays(21));

        Assert.Equal(SeasonPhase.RegularSeason, world.CurrentSeasonPhase(season));
    }

    public void NoUiOrGodotDependencyExists()
    {
        var seasonFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Seasons"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in seasonFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Season module should not reference Godot.");
            Assert.False(text.Contains("Control", StringComparison.Ordinal), "Season module should not define UI controls.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulebookPath = Path.Combine(directory.FullName, "data", "rulebooks", "junior_v1.json");
            if (File.Exists(rulebookPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
