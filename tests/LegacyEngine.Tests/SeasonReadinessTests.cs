using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.World;

internal sealed class SeasonReadinessTests
{
    public void OpeningRosterValidationPassesWhenCompliant()
    {
        var ready = ReadyScenario();
        var report = new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(report.RosterReport.ValidationResult.IsValid, report.RosterReport.ValidationResult.Message);
        Assert.Equal("Ready", report.RosterStatus);
    }

    public void RosterOverLimitIsRejected()
    {
        var ready = ReadyScenarioWithRosterRule(activeDelta: -1);
        var report = new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot);

        Assert.Equal(RuleErrorCodes.ActiveRosterTooLarge, report.RosterReport.ValidationResult.RuleCode);
        Assert.False(report.CanBeginSeason, "Over-limit roster should block season start.");
    }

    public void RosterUnderLimitIsRejected()
    {
        var ready = ReadyScenarioWithRosterRule(minDelta: 1, activeDelta: 1, maxDelta: 5);
        var report = new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot);

        Assert.Equal(RuleErrorCodes.RosterTooSmall, report.RosterReport.ValidationResult.RuleCode);
        Assert.False(report.CanBeginSeason, "Under-limit roster should block season start.");
    }

    public void GoalieRequirementIsRejected()
    {
        var ready = ReadyScenarioWithRosterRule(goalieDelta: 1);
        var report = new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot);

        Assert.Equal(RuleErrorCodes.NotEnoughGoalies, report.RosterReport.ValidationResult.RuleCode);
        Assert.False(report.CanBeginSeason, "Goalie shortage should block season start.");
    }

    public void PendingActionsPreventSeasonStart()
    {
        var ready = ReadyScenario();
        var action = new PendingGmAction(
            "pending-opening-test",
            PendingGmActionType.AddToRoster,
            PendingGmActionStatus.Pending,
            ready.ScenarioSnapshot.CurrentDate,
            "person-opening-test",
            "Test Player",
            ready.ScenarioSnapshot.Organization.OrganizationId,
            "Opening roster decision needed",
            "A final roster choice is still unresolved.",
            "Approve or decline before opening night.",
            RosterPosition.Center);
        var scenario = ready.ScenarioSnapshot with { PendingActions = new[] { action } };

        var report = new SeasonReadinessService().Evaluate(ready.Registry, scenario);

        Assert.False(report.CanBeginSeason, "Open pending actions should block season start.");
        Assert.True(report.ChecklistItems.Any(item => item.Code == "pending-actions" && !item.IsComplete), "Pending action checklist item should be incomplete.");
    }

    public void OwnerCoachAndScoutReviewsAreGenerated()
    {
        var readyBase = ReadyScenario();
        var ready = readyBase with
        {
            ScenarioSnapshot = readyBase.ScenarioSnapshot with { SeasonReadiness = new SeasonReadinessState() }
        };
        var result = new SeasonReadinessService().GenerateReviews(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.SeasonReadiness.ReviewsGenerated, "Reviews flag should be set.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.OwnerOffseasonReview), "Owner review inbox item should be generated.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.CoachRosterReview), "Coach review inbox item should be generated.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.ScoutOffseasonReview), "Scout review inbox item should be generated.");
    }

    public void SeasonReadinessCanBeReady()
    {
        var ready = ReadyScenario();
        var report = new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(report.IsReady, report.BlockedReason);
        Assert.True(report.CanBeginSeason, report.BlockedReason);
    }

    public void BeginSeasonIsBlockedWhenNotReady()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var result = new SeasonReadinessService().BeginSeason(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.False(result.Success, "Default scenario should not begin before roster cutdown and reviews.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.OpeningRosterRejected), "Blocked begin should create a rejection message.");
    }

    public void BeginSeasonIsEnabledWhenReady()
    {
        var ready = ReadyScenario();
        var result = new SeasonReadinessService().BeginSeason(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.SeasonReadiness.SeasonBegun, "Season begun flag should be set.");
        Assert.Equal(SeasonPhase.RegularSeason, result.ScenarioSnapshot.Season.CurrentPhase);
        Assert.Equal(WorldPhase.RegularSeason, result.ScenarioSnapshot.AlphaSnapshot.WorldState.CurrentPhase);
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.SeasonReady), "Season ready message should be generated.");
    }

    public void AlphaDesktopExposesSeasonReadinessSurface()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Season Readiness", StringComparison.Ordinal), "AlphaDesktop should expose a Season Readiness screen.");
        Assert.True(text.Contains("Begin Season", StringComparison.Ordinal), "AlphaDesktop should expose Begin Season.");
        Assert.True(text.Contains("Organization Health", StringComparison.Ordinal), "Season Readiness should show organization health.");
        Assert.True(text.Contains("Roster Compliance", StringComparison.Ordinal), "Season Readiness should show roster compliance.");
    }

    public void SeasonReadinessHasNoGodotSaveOrGameSimulationDependency()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "SeasonReadiness*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Opening*.cs"));
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Season readiness should not depend on Godot.");
        Assert.False(text.Contains("Save", StringComparison.Ordinal), "Season readiness should not implement save/load.");
        Assert.False(text.Contains("GameSimulation", StringComparison.Ordinal), "Season readiness should not implement game simulation.");
        Assert.False(text.Contains("Standings", StringComparison.Ordinal), "Season readiness should not implement standings.");
        Assert.False(text.Contains("Playoffs", StringComparison.Ordinal), "Season readiness should not implement playoffs.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyScenario() =>
        ReadyScenarioWithRosterRule();

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyScenarioWithRosterRule(
        int minDelta = 0,
        int maxDelta = 0,
        int activeDelta = 0,
        int goalieDelta = 0)
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var roster = scenario.ScenarioSnapshot.AlphaSnapshot.Roster;
        var total = roster.CurrentPlayers.Count;
        var active = roster.ActivePlayers.Count;
        var goalies = roster.CurrentPlayers.Count(player => player.IsGoalie);
        var overage = roster.CurrentPlayers.Count(player => player.IsOverage());
        var imports = roster.CurrentPlayers.Count(player => player.IsImport);
        var rulebook = WithRosterRules(
            RulebookPresets.Create(DraftLeaguePreset.JuniorMajor),
            new RosterRules
            {
                MinRoster = total + minDelta,
                MaxRoster = total + maxDelta,
                ActiveRoster = active + activeDelta,
                GoaliesRequired = goalies + goalieDelta,
                OverageSlots = Math.Max(3, overage),
                ImportSlots = Math.Max(2, imports),
                InjuredReserveEnabled = true,
                ReserveListEnabled = true
            });

        var registry = scenario.Registry with { Rulebook = rulebook };
        var camp = new TrainingCamp(
            "camp-opening-ready",
            scenario.ScenarioSnapshot.Organization.OrganizationId,
            scenario.ScenarioSnapshot.CurrentDate,
            Array.Empty<TrainingCampPlayer>(),
            Array.Empty<TrainingCampEvaluation>(),
            CompletedOn: scenario.ScenarioSnapshot.CurrentDate);
        var snapshot = scenario.ScenarioSnapshot with
        {
            TrainingCamp = camp,
            PendingActions = Array.Empty<PendingGmAction>(),
            ProspectRights = Array.Empty<DraftRightsRecord>(),
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true)
        };
        snapshot.Validate();

        return (registry, snapshot);
    }

    private static Rulebook WithRosterRules(Rulebook source, RosterRules rosterRules) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = rosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules,
            SeasonRules = source.SeasonRules,
            AffiliateRules = source.AffiliateRules
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
