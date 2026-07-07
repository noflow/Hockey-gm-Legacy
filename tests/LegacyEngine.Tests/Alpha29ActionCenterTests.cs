using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

internal sealed class Alpha29ActionCenterTests
{
    public void ActionCenterPullsPendingGmActions()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var pending = new PendingGmActionService().CreateForDraftPickReady(ready.Registry, ready.ScenarioSnapshot, prospect);
        var items = BuildItems(ready.Registry, pending.ScenarioSnapshot);

        Assert.True(items.Any(item => item.SourcePendingActionId == pending.Action!.ActionId), "Pending GM action should appear in Action Center.");
    }

    public void ActionCenterPullsRosterWarnings()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var report = ReadinessWithRosterWarning();
        var items = new ActionCenterService().BuildItems(
            ready.ScenarioSnapshot,
            Array.Empty<InboxMessage>(),
            Budget(ready.ScenarioSnapshot, BudgetStatus.UnderBudget),
            report,
            Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Roster), "Roster warning should create Action Center item.");
    }

    public void ActionCenterPullsStaffVacancies()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var vacancies = new StaffOfficeService().BuildVacancies(ready.ScenarioSnapshot, ready.Registry.Rulebook!);
        var items = BuildItems(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(vacancies.Count > 0, "Scenario should have vacancies for this test.");
        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Staff), "Staff vacancy should create Action Center item.");
    }

    public void ActionCenterPullsBudgetWarnings()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var items = new ActionCenterService().BuildItems(
            ready.ScenarioSnapshot,
            Array.Empty<InboxMessage>(),
            Budget(ready.ScenarioSnapshot, BudgetStatus.NearLimit),
            new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot),
            Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Budget), "Budget warning should create Action Center item.");
    }

    public void ActionCenterPullsScoutingCompletion()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var completed = CompletedScoutingAssignment(ready.ScenarioSnapshot);
        var snapshot = ready.ScenarioSnapshot with { ScoutingOperations = ready.ScenarioSnapshot.ScoutingOperations.Append(completed).ToArray() };
        var items = BuildItems(ready.Registry, snapshot);

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Scouting && item.Title.Contains("Scout report returned", StringComparison.Ordinal)), "Completed scouting assignment should create Action Center item.");
    }

    public void MedicalInboxActionIncludesPlayerNameAndPosition()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var injury = ready.ScenarioSnapshot.AlphaSnapshot.Injuries.First(injury => injury.IsActive);
        var player = ready.ScenarioSnapshot.AlphaSnapshot.Players.First(person => person.PersonId == injury.PersonId);
        var position = ready.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(injury.PersonId)!.Position.ToString();
        var medicalInbox = ready.ScenarioSnapshot.FirstDayInbox.First(item => item.EventType == LegacyEventType.PlayerInjured);
        var items = BuildItems(ready.Registry, ready.ScenarioSnapshot);
        var medicalAction = items.First(item => item.Category == ActionCenterCategory.Medical && item.SourceInboxItemId == medicalInbox.InboxItemId);

        Assert.True(medicalInbox.Title.Contains(player.Identity.DisplayName, StringComparison.Ordinal), "Medical title should include injured player name.");
        Assert.True(medicalInbox.Title.Contains(position, StringComparison.Ordinal), "Medical title should include injured player position.");
        Assert.True(medicalInbox.Summary.Contains(player.Identity.DisplayName, StringComparison.Ordinal), "Medical summary should include injured player name.");
        Assert.True(medicalInbox.Summary.Contains(position, StringComparison.Ordinal), "Medical summary should include injured player position.");
        Assert.False(medicalInbox.Summary.Contains("One roster player", StringComparison.Ordinal), "Medical summary should not use vague roster-player wording.");
        Assert.Equal(player.Identity.DisplayName, medicalAction.RelatedPersonName);
        Assert.True(medicalAction.Reason.Contains(player.Identity.DisplayName, StringComparison.Ordinal), "Action reason should include injured player name.");
        Assert.True(medicalAction.Reason.Contains(position, StringComparison.Ordinal), "Action reason should include injured player position.");
    }

    public void ActionItemHasRequiredFields()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var item = BuildItems(ready.Registry, ready.ScenarioSnapshot).First();

        Assert.False(string.IsNullOrWhiteSpace(item.Title), "Action item title is required.");
        Assert.False(string.IsNullOrWhiteSpace(item.Reason), "Action item reason is required.");
        Assert.False(string.IsNullOrWhiteSpace(item.Consequence), "Action item consequence is required.");
        Assert.False(string.IsNullOrWhiteSpace(item.RecommendedAction), "Action item recommendation is required.");
    }

    public void DailyAgendaGenerated()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var service = new ActionCenterService();
        var items = BuildItems(ready.Registry, ready.ScenarioSnapshot);
        var agenda = service.BuildDailyAgenda(ready.ScenarioSnapshot, items, Budget(ready.ScenarioSnapshot, BudgetStatus.UnderBudget));

        Assert.True(agenda.Count > 0, "Daily agenda should be generated.");
        Assert.True(agenda.First().Contains("Good morning", StringComparison.Ordinal), "Daily agenda should greet the GM.");
    }

    public void AssistantGmRecommendationsGenerated()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var service = new ActionCenterService();
        var items = BuildItems(ready.Registry, ready.ScenarioSnapshot);
        var recommendations = service.BuildAssistantGmRecommendations(ready.ScenarioSnapshot, items, Budget(ready.ScenarioSnapshot, BudgetStatus.NearLimit));

        Assert.True(recommendations.Count > 0, "Assistant GM recommendations should be generated.");
    }

    public void ActionStatusCanChange()
    {
        var ready = NewGmScenarioBootstrapper.CreateScenario();
        var service = new ActionCenterService();
        var item = BuildItems(ready.Registry, ready.ScenarioSnapshot).First();

        Assert.Equal(ActionCenterStatus.Resolved, service.ApplyStatus(item, ActionCenterStatus.Resolved).Status);
        Assert.Equal(ActionCenterStatus.Deferred, service.ApplyStatus(item, ActionCenterStatus.Deferred).Status);
        Assert.Equal(ActionCenterStatus.Dismissed, service.ApplyStatus(item, ActionCenterStatus.Dismissed).Status);
    }

    public void AlphaDesktopDashboardExposesActionCenterCounts()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("Open Actions", StringComparison.Ordinal), "Dashboard should show open action count.");
        Assert.True(source.Contains("Urgent Actions", StringComparison.Ordinal), "Dashboard should show urgent action count.");
        Assert.True(source.Contains("Daily Agenda", StringComparison.Ordinal), "Dashboard should show Daily Agenda.");
        Assert.True(source.Contains("Assistant GM Recommendations", StringComparison.Ordinal), "Dashboard should show assistant recommendations.");
        Assert.True(source.Contains("Last advance result", StringComparison.Ordinal), "Dashboard should show advance stop reason.");
        Assert.True(source.Contains("Next recommended action", StringComparison.Ordinal), "Dashboard should show next recommended action.");
    }

    public void Alpha29HasNoGodotSaveOrGameSimulationChanges()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "ActionCenter*.cs")
            .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") });
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Action Center must not add Godot.");
        Assert.False(text.Contains("GameSimulation", StringComparison.OrdinalIgnoreCase), "Action Center must not add game simulation changes.");
    }

    private static IReadOnlyList<ActionCenterItem> BuildItems(EngineRegistry registry, NewGmScenarioSnapshot snapshot)
    {
        var service = new ActionCenterService();
        var inbox = new InboxManager();
        inbox.AddRange(snapshot.FirstDayInbox);
        return service.BuildItems(
            snapshot,
            inbox.AllMessages,
            Budget(snapshot, BudgetStatus.UnderBudget),
            new SeasonReadinessService().Evaluate(registry, snapshot),
            new StaffOfficeService().BuildVacancies(snapshot, registry.Rulebook!));
    }

    private static BudgetSnapshot Budget(NewGmScenarioSnapshot snapshot, BudgetStatus status) =>
        new(1_000_000m, status == BudgetStatus.OverBudget ? 1_100_000m : 900_000m, status == BudgetStatus.OverBudget ? -100_000m : 100_000m, 0m, 0m, 0m, 0m, status, $"Owner budget status: {status}");

    private static SeasonReadinessReport ReadinessWithRosterWarning() =>
        new(
            IsReady: false,
            CanBeginSeason: false,
            RosterStatus: "Roster is not compliant",
            RosterReport: new OpeningRosterReport(29, 26, 2, 9, 18, 4, 1, 0, 3, RosterValidationResult.Failure("ROSTER_TOO_LARGE", "Roster is over the opening limit.")),
            ChecklistItems: new[] { new OpeningChecklistItem("roster", "Set opening roster", false) },
            OrganizationHealth: "Needs attention",
            OwnerSatisfaction: "Watchful",
            OwnerReview: "Owner wants a compliant roster.",
            HeadCoachSummary: "Coach wants clarity.",
            HeadScoutSummary: "Scout is waiting.",
            StaffRecommendations: "Cut down roster.",
            TrainingCampStatus: "Open",
            BlockedReason: "Roster is over limit.");

    private static ScoutingOperationAssignment CompletedScoutingAssignment(NewGmScenarioSnapshot snapshot) =>
        new(
            AssignmentId: "test-completed-scouting",
            ScoutPersonId: snapshot.AlphaSnapshot.ScoutPerson.PersonId,
            ScoutName: snapshot.AlphaSnapshot.Scout.Name,
            AssignmentType: ScoutingOperationAssignmentType.Region,
            TargetRegion: ScoutingRegionFocus.Europe,
            TargetPlayerId: null,
            TargetName: "Europe",
            StartDate: snapshot.CurrentDate.AddDays(-7),
            ExpectedReportDate: snapshot.CurrentDate,
            Priority: ScoutingOperationPriority.High,
            Notes: "Test completed assignment.",
            Status: ScoutingOperationStatus.Completed,
            WorkloadAtAssignment: 1,
            RelationshipQualityAtAssignment: 70,
            CommunicationQuality: 70,
            DurationDays: 7,
            ReturnDate: snapshot.CurrentDate,
            ProgressDays: 7,
            CompletedOn: snapshot.CurrentDate,
            ReportId: "test-report-001");

    private static string ReadAlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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
