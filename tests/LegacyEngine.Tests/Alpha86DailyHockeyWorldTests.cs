using LegacyEngine.Integration;
using LegacyEngine.Staff;

internal sealed class Alpha86DailyHockeyWorldTests
{
    public void MorningBriefingIsConciseAndActionable()
    {
        var world = BuildWorld();

        Assert.True(world.MorningBriefing.Count is > 0 and <= 3, "Morning briefing should contain no more than three recommendations.");
        Assert.True(world.MorningBriefing.All(line => !string.IsNullOrWhiteSpace(line)), "Morning briefing recommendations should be readable.");
    }

    public void OrganizationCardsCoverDailyClubContext()
    {
        var titles = BuildWorld().OrganizationCards.Select(card => card.Title).ToArray();

        Assert.Contains("Current record", titles);
        Assert.Contains("Next game", titles);
        Assert.Contains("Owner mood", titles);
        Assert.Contains("Cap space", titles);
        Assert.Contains("Prospects improving", titles);
        Assert.Contains("Injuries", titles);
        Assert.Contains("Expiring contracts", titles);
    }

    public void LeaguePulseAndSnapshotAreReadable()
    {
        var world = BuildWorld();

        Assert.True(world.LeaguePulseCards.Count >= 4, "League Pulse should summarize stories, transactions, standings, and players.");
        Assert.True(world.LeagueSnapshotCards.Count >= 4, "League Snapshot should include games, leaders, and transaction context.");
        Assert.True(world.LeaguePulseCards.All(card => card.Summary.Length <= 180), "League Pulse should remain short and readable.");
    }

    public void TodayActionsAreLimitedAndDoNotDuplicateActionCenter()
    {
        var world = BuildWorld();

        Assert.True(world.TodayActions.Count <= 3, "Daily Hockey World should summarize at most three actions.");
        Assert.True(world.TodayActions.Select(card => card.CardId).Distinct(StringComparer.Ordinal).Count() == world.TodayActions.Count, "Daily actions should not be duplicated.");
        Assert.True(world.TodayActions.All(card => card.Destination == "Dashboard/Action Center"), "Daily actions should route to the existing Action Center rather than duplicate its workflow.");
    }

    public void CoachScoutAndMedicalReportsAreAvailable()
    {
        var world = BuildWorld();

        Assert.False(string.IsNullOrWhiteSpace(world.CoachReport), "Coach report should be present.");
        Assert.False(string.IsNullOrWhiteSpace(world.ScoutReport), "Scout report should be present.");
        Assert.False(string.IsNullOrWhiteSpace(world.MedicalReport), "Medical report should be present.");
    }

    public void ProspectWatchTransactionWireScheduleAndCalendarHaveStates()
    {
        var world = BuildWorld();

        Assert.True(world.ProspectWatchCards.Count > 0, "Prospect Watch should show an update or a clear empty state.");
        Assert.True(world.TransactionWireCards.Count > 0, "Transaction Wire should show a summary or a clear empty state.");
        Assert.True(world.ScheduleCards.Count > 0, "Schedule Summary should show a schedule or a clear empty state.");
        Assert.True(world.CalendarCards.Count > 0, "Calendar should show upcoming season events.");
        Assert.False(string.IsNullOrWhiteSpace(world.PlayerOfTheDay.Title), "Player of the Day should be present.");
        Assert.False(string.IsNullOrWhiteSpace(world.TeamOfTheDay.Title), "Team of the Day should be present.");
    }

    public void DailyWorldCardsAreClickableAndDesktopOpensAfterAdvance()
    {
        var world = BuildWorld();
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(world.OrganizationCards.Concat(world.LeaguePulseCards).Concat(world.TodayActions).All(card => !string.IsNullOrWhiteSpace(card.Destination)), "Every Daily Hockey World card requires a destination.");
        Assert.True(source.Contains("Daily Hockey World", StringComparison.Ordinal), "Desktop should expose the Daily Hockey World workspace.");
        Assert.True(source.Contains("SelectWorkspaceScreen(\"Dashboard\", \"Daily Hockey World\")", StringComparison.Ordinal), "Advance Day should open the Daily Hockey World workspace.");
        Assert.True(source.Contains("NavigateDailyWorldCard", StringComparison.Ordinal), "Daily Hockey World cards should be clickable.");
        Assert.True(source.Contains("PushNavigationSnapshot();", StringComparison.Ordinal), "Advancing should preserve the previous workspace for Back navigation.");
        Assert.True(source.Contains("OpenDailyWorldAfterAdvance", StringComparison.Ordinal), "All advance paths should share Daily Hockey World routing.");
        Assert.True(source.Contains("Daily Briefings", StringComparison.Ordinal), "Reports and History should expose the briefing archive.");
        Assert.True(source.Contains("var contentHost = new ContentControl();", StringComparison.Ordinal), "Workspace screens should be attached by the selected sidebar item rather than attached twice at startup.");
        Assert.True(source.Contains("if (!ReferenceEquals(contentHost.Content, content))", StringComparison.Ordinal), "Workspace navigation should not reattach an already-parented visual.");
        Assert.True(source.Contains("Content = null;", StringComparison.Ordinal), "Starting or loading a career should disconnect the creation screen before the office layout is attached.");
        Assert.True(source.Contains("DetachFromLogicalParent(content);", StringComparison.Ordinal), "Workspace navigation should safely detach a stale visual before attaching it to the active content host.");
        Assert.True(source.Contains("Tag = screen.Label", StringComparison.Ordinal), "Workspace sidebar metadata should store a screen key, not a live visual element.");
        Assert.True(source.Contains("var screensByLabel", StringComparison.Ordinal), "Workspace screen controls should resolve from a local label lookup.");
        Assert.True(source.Contains("PrepareOfficeVisuals();", StringComparison.Ordinal), "A retry after a partial office build should detach persistent header visuals before rebuilding.");
    }

    public void MultiDayBriefingIsStoredWithoutDuplicatesAndPreservesUrgency()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var service = new DailyHockeyWorldService();
        var urgent = Action("urgent-offer-sheet", ActionCenterPriority.Urgent, "Offer-sheet response required by tomorrow.");
        var routine = Action("routine-scouting", ActionCenterPriority.Normal, "Routine viewing note.");
        var world = service.Build(ready.ScenarioSnapshot, new[] { urgent, routine }, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), ready.Registry.Rulebook);
        var advance = new FirstMonthAdvanceResult(ready.ScenarioSnapshot, 7, 0, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Stopped because monthly report is ready.", true);
        var briefing = service.CreateBriefing(ready.ScenarioSnapshot, ready.ScenarioSnapshot, advance, new[] { urgent, routine }, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>());
        var merged = service.MergeBriefing(ready.ScenarioSnapshot, briefing);
        merged = service.MergeBriefing(merged, briefing);

        Assert.Equal(7, briefing.DaysAdvanced);
        Assert.True(merged.DailyBriefings.Count == 1, "Loading or repeat rendering must not duplicate a stored daily briefing.");
        Assert.True(world.TodayActions.Single(card => card.CardId.Contains("urgent-offer-sheet", StringComparison.Ordinal)).IsUrgent, "Urgent actions should be marked for the Daily Hockey World banner.");
        Assert.False(world.TodayActions.Single(card => card.CardId.Contains("routine-scouting", StringComparison.Ordinal)).IsUrgent, "Routine events must not become urgent banners.");
    }

    public void DailyBriefingArchiveSurvivesSaveLoad()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var briefing = new DailyBriefingRecord(
            "daily-briefing:test",
            ready.ScenarioSnapshot.CurrentDate,
            ready.ScenarioSnapshot.CurrentDate,
            1,
            "Stopped: New Day",
            "0-0-0 | 0 pts",
            "No major headline.",
            0,
            new[] { "Advanced 1 Day" },
            Array.Empty<string>(),
            new DateTimeOffset(ready.ScenarioSnapshot.CurrentDate.Year, ready.ScenarioSnapshot.CurrentDate.Month, ready.ScenarioSnapshot.CurrentDate.Day, 8, 0, 0, TimeSpan.Zero));
        var scenario = ready.ScenarioSnapshot with { DailyBriefings = new[] { briefing } };
        var file = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha86-{Guid.NewGuid():N}.json");
        var saved = new SaveGameService().SaveCareer(
            scenario,
            Array.Empty<InboxMessage>(),
            Array.Empty<LeagueTransaction>(),
            new Dictionary<string, ActionCenterStatus>(),
            new BudgetOverviewService().Build(scenario, ready.Registry.Rulebook!),
            file);
        var loaded = new SaveGameService().LoadFromFile(file, ready.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(1, loaded.SaveGame!.ScenarioSnapshot.DailyBriefings.Count);
        Assert.Equal("daily-briefing:test", loaded.SaveGame.ScenarioSnapshot.DailyBriefings.Single().BriefingId);
    }

    private static ActionCenterItem Action(string id, ActionCenterPriority priority, string recommendation) =>
        new(id, id, ActionCenterCategory.Contracts, priority, null, null, null, null, null, "Test reason.", "Test consequence.", recommendation, null, null, null);

    private static DailyHockeyWorldSnapshot BuildWorld()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var inbox = new InboxManager();
        inbox.AddRange(ready.ScenarioSnapshot.FirstDayInbox);
        var actions = new ActionCenterService().BuildItems(
            ready.ScenarioSnapshot,
            inbox.AllMessages,
            new BudgetOverviewService().Build(ready.ScenarioSnapshot, ready.Registry.Rulebook!),
            new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot),
            new StaffOfficeService().BuildVacancies(ready.ScenarioSnapshot, ready.Registry.Rulebook!));
        return new DailyHockeyWorldService().Build(ready.ScenarioSnapshot, actions, inbox.AllMessages, Array.Empty<LeagueTransaction>(), ready.Registry.Rulebook);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HockeyGmLegacy.slnx"))){ return directory.FullName; }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
