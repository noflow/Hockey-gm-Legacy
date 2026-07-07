using LegacyEngine.Events;
using LegacyEngine.Integration;

internal sealed class Alpha50PlayabilityPolishTests
{
    public void RoutineMessagesRouteToJournalNotInbox()
    {
        var service = new PlayabilityPolishService();
        var routine = new AlphaInboxItem(
            "routine-1",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            LegacyEventType.ScoutAssigned,
            LegacyEventSeverity.Notice,
            "Scout assigned",
            "Scout assigned to Eastern Canada.",
            "person-1");

        Assert.False(PlayabilityPolishService.ShouldShowInInbox(routine), "Routine assignment notice should not interrupt the GM inbox.");
        Assert.Equal(0, service.FilterInboxItems(new[] { routine }).Count);
        Assert.Equal(1, service.BuildJournalEntries(new[] { routine }).Count);
    }

    public void ImportantMessagesRemainInInbox()
    {
        var service = new PlayabilityPolishService();
        var important = new AlphaInboxItem(
            "injury-1",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            LegacyEventType.PlayerInjured,
            LegacyEventSeverity.Warning,
            "Medical update: Mason Clark, C",
            "Mason Clark, C, is expected to miss time and needs GM review.",
            "player-1");

        Assert.True(PlayabilityPolishService.ShouldShowInInbox(important), "Important medical updates should remain in the GM inbox.");
        Assert.Equal(1, service.FilterInboxItems(new[] { important }).Count);
        Assert.Equal(0, service.BuildJournalEntries(new[] { important }).Count);
    }

    public void InboxFilterDedupesRepeatedMessages()
    {
        var service = new PlayabilityPolishService();
        var older = new AlphaInboxItem("a", DateTimeOffset.Parse("2026-09-01T09:00:00Z"), LegacyEventType.PlayerInjured, LegacyEventSeverity.Warning, "Medical update", "Needs review.", "player-1");
        var newer = older with { InboxItemId = "b", Date = DateTimeOffset.Parse("2026-09-01T11:00:00Z") };

        var filtered = service.FilterInboxItems(new[] { older, newer });

        Assert.Equal(1, filtered.Count);
        Assert.Equal("b", filtered[0].InboxItemId);
    }

    public void ActionCenterRemovesClosedAndDuplicateItems()
    {
        var service = new PlayabilityPolishService();
        var item = ActionItem("a", ActionCenterStatus.Open, ActionCenterPriority.Normal);
        var duplicate = ActionItem("b", ActionCenterStatus.Open, ActionCenterPriority.Urgent);
        var closed = ActionItem("c", ActionCenterStatus.Resolved, ActionCenterPriority.Urgent);

        var cleaned = service.CleanActionCenterItems(new[] { item, duplicate, closed });

        Assert.Equal(1, cleaned.Count);
        Assert.Equal("b", cleaned[0].ActionCenterItemId);
        Assert.Equal(ActionCenterPriority.Urgent, cleaned[0].Priority);
    }

    public void GlobalSearchFindsPeopleAndHistory()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new PlayabilityPolishService();
        var player = scenario.ScenarioSnapshot.AlphaSnapshot.Players[0];

        var results = service.Search(
            scenario.ScenarioSnapshot,
            Array.Empty<InboxMessage>(),
            Array.Empty<LeagueTransaction>(),
            Array.Empty<JournalEntry>(),
            player.Identity.DisplayName.Split(' ')[0]);

        Assert.True(results.Any(result => result.TargetPersonId == player.PersonId), "Search should return player/person results.");
        Assert.True(results.All(result => !string.IsNullOrWhiteSpace(result.TargetWorkspace)), "Search results should explain where to go.");
    }

    public void PlaytestChecklistGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new PlayabilityPolishService();

        var checklist = service.BuildPlaytestChecklist(scenario.ScenarioSnapshot, Array.Empty<ActionCenterItem>());

        Assert.True(checklist.Count >= 6, "Playtest checklist should cover the core first-month flow.");
        Assert.True(checklist.Any(item => item.Area == "Inbox"), "Checklist should include inbox focus.");
    }

    public void AlphaDesktopExposesPolishSurfaces()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("new WorkspaceScreen(\"Journal\", CreateTextScreen(\"Journal\"))", StringComparison.Ordinal), "Reports should expose Journal.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Global Search\", CreateTextScreen(\"Global Search\"))", StringComparison.Ordinal), "Reports should expose Global Search.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Playtest Checklist\", CreateTextScreen(\"Playtest Checklist\"))", StringComparison.Ordinal), "Reports should expose Playtest Checklist.");
        Assert.True(source.Contains("State.InboxFocusSummary", StringComparison.Ordinal), "Dashboard should explain inbox focus.");
        Assert.True(source.Contains("League News", StringComparison.Ordinal), "League News should remain visible.");
    }

    private static ActionCenterItem ActionItem(string id, ActionCenterStatus status, ActionCenterPriority priority) =>
        new(
            id,
            "Review roster decision",
            ActionCenterCategory.Roster,
            priority,
            DateOnly.Parse("2026-09-03"),
            "person-1",
            "Mason Clark",
            "team-1",
            "Prairie Falcons",
            "Roster needs review.",
            "Ignoring it may leave opening night unresolved.",
            "Review the decision.",
            null,
            null,
            null,
            status);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HockeyGmLegacy.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
