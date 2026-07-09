using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha27FirstMonthPlayabilityTests
{
    public void AdvanceToNextGameStopsOnPlayerGame()
    {
        var ready = ScenarioWithPlayerTeamGameTomorrow("alpha27-next-game");
        var result = new FirstMonthAdvanceService().AdvanceToNextGame(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.StoppedForAttention, "Advance should stop for player team game.");
        Assert.True(result.StopReason.Contains("played", StringComparison.OrdinalIgnoreCase), result.StopReason);
        Assert.Equal(1, result.DaysAdvanced);
    }

    public void AdvanceToMonthEndStopsAtMonthlyReport()
    {
        var ready = ScenarioWithNoGames();
        var result = new FirstMonthAdvanceService().AdvanceToMonthEnd(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.StoppedForAttention, "Advance to month end should stop for monthly report.");
        Assert.True(result.StopReason.Contains("monthly report", StringComparison.OrdinalIgnoreCase), result.StopReason);
        Assert.True(result.MonthlySummary is not null, "Monthly summary should be returned.");
    }

    public void AdvanceStopsOnInjury()
    {
        var ready = ScenarioWithInjuryGameTomorrow();
        var result = new FirstMonthAdvanceService().AdvanceDays(ready.Registry, ready.ScenarioSnapshot, 7);

        Assert.True(result.StoppedForAttention, "Advance should stop for injury concern.");
        Assert.True(result.StopReason.Contains("medical", StringComparison.OrdinalIgnoreCase) || result.StopReason.Contains("injur", StringComparison.OrdinalIgnoreCase), result.StopReason);
    }

    public void AdvanceStopsOnUrgentPendingAction()
    {
        var ready = ScenarioWithNoGames();
        var action = new PendingGmAction(
            "pending-alpha27-urgent",
            PendingGmActionType.AddToRoster,
            PendingGmActionStatus.Pending,
            ready.ScenarioSnapshot.CurrentDate,
            ready.ScenarioSnapshot.AlphaSnapshot.Players[0].PersonId,
            ready.ScenarioSnapshot.AlphaSnapshot.Players[0].Identity.DisplayName,
            ready.ScenarioSnapshot.Organization.OrganizationId,
            "Urgent roster decision",
            "Resolve roster issue before next game.",
            "Approve add to roster.",
            RosterPosition.Center);
        var snapshot = ready.ScenarioSnapshot with { PendingActions = new[] { action } };

        var result = new FirstMonthAdvanceService().AdvanceDays(ready.Registry, snapshot, 7);

        Assert.Equal(0, result.DaysAdvanced);
        Assert.True(result.StopReason.Contains("urgent pending action", StringComparison.OrdinalIgnoreCase), result.StopReason);
    }

    public void MonthlySummaryGenerated()
    {
        var ready = ScenarioWithNoGames();
        var result = new MonthlyGmSummaryService().Generate(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Created, "Monthly summary should be created.");
        Assert.True(result.ScenarioSnapshot.MonthlySummaries.Count == 1, "Monthly summary should be archived.");
    }

    public void MonthlySummaryIncludesRecord()
    {
        var ready = ScenarioWithCompletedPlayerGame();
        var result = new MonthlyGmSummaryService().Generate(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Summary.TeamRecordForMonth.Contains("-", StringComparison.Ordinal), "Monthly summary should include record.");
        Assert.True(result.Summary.OverallRecord.Contains("pts", StringComparison.Ordinal), "Monthly summary should include overall points.");
    }

    public void MonthlySummaryIncludesRequiredSections()
    {
        var ready = ScenarioWithCompletedPlayerGame();
        var result = new MonthlyGmSummaryService().Generate(ready.Registry, ready.ScenarioSnapshot);
        var text = string.Join(" ", result.Summary.Sections.Select(section => section.Title));

        Assert.True(text.Contains("Owner", StringComparison.Ordinal), "Summary should include owner/staff section.");
        Assert.True(text.Contains("Development", StringComparison.Ordinal), "Summary should include development section.");
        Assert.True(text.Contains("Roster", StringComparison.Ordinal), "Summary should include roster/budget section.");
        Assert.True(result.Summary.HeadScoutUpdate.Length > 0, "Summary should include scout update.");
        Assert.True(result.Summary.BudgetStatus.Length > 0, "Summary should include budget status.");
    }

    public void MonthlySummaryInboxCreatedOnce()
    {
        var ready = ScenarioWithNoGames();
        var service = new MonthlyGmSummaryService();
        var first = service.Generate(ready.Registry, ready.ScenarioSnapshot);
        var second = service.Generate(ready.Registry, first.ScenarioSnapshot);

        Assert.Equal(1, first.InboxItems.Count);
        Assert.Equal(0, second.InboxItems.Count);
    }

    public void RoutineDevelopmentDoesNotSpamInbox()
    {
        var ready = ScenarioWithNoGames();
        var result = new FirstMonthAdvanceService().AdvanceToMonthEnd(ready.Registry, ready.ScenarioSnapshot);

        Assert.False(result.InboxItems.Any(item => item.EventType == LegacyEventType.PlayerDevelopmentUpdated), "Routine development should stay out of inbox.");
    }

    public void LeagueWideGamesDoNotSpamInbox()
    {
        var ready = ScenarioWithLeagueGameTomorrow();
        var result = new FirstMonthAdvanceService().AdvanceDays(ready.Registry, ready.ScenarioSnapshot, 2);

        Assert.False(result.InboxItems.Any(item => item.EventType == LegacyEventType.GamePlayed), "League-wide games should not create game recap inbox items.");
    }

    public void InboxPrioritySortingWorks()
    {
        var manager = new InboxManager();
        manager.Add(new AlphaInboxItem("low", DateTimeOffset.Parse("2026-09-01T10:00:00Z"), LegacyEventType.Generic, LegacyEventSeverity.Notice, "Routine", "Routine update.", null));
        manager.Add(new AlphaInboxItem("urgent", DateTimeOffset.Parse("2026-09-01T08:00:00Z"), LegacyEventType.PlayerInjured, LegacyEventSeverity.Warning, "Injury", "Important injury.", null));
        manager.Add(new AlphaInboxItem("normal-new", DateTimeOffset.Parse("2026-09-01T12:00:00Z"), LegacyEventType.Generic, LegacyEventSeverity.Notice, "Newer", "New normal.", null));

        var ordered = manager.Query(new InboxFilter());

        Assert.Equal("urgent", ordered[0].InboxItemId);
        Assert.True(ordered[0].Priority == InboxPriority.Important || ordered[0].Priority == InboxPriority.Urgent, "Urgent/important message should sort first.");
    }

    public void DashboardExposesAdvanceControlsAndSummaryCounts()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Advance to Next Game", StringComparison.Ordinal), "Dashboard should expose advance to next game.");
        Assert.True(text.Contains("Advance to Month End", StringComparison.Ordinal), "Dashboard should expose advance to month end.");
        Assert.True(text.Contains("Urgent Decisions", StringComparison.Ordinal), "Dashboard should expose urgent decision count.");
        Assert.True(text.Contains("Monthly Summary", StringComparison.Ordinal), "Desktop should expose monthly summary panel.");
        Assert.True(text.Contains("Next stop reason", StringComparison.Ordinal), "Dashboard should expose next stop reason.");
    }

    public void FirstMonthPassHasNoGodotSaveOrFullTacticalSimulation()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Advance*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Monthly*.cs"))
            .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") });
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 2.7 should not add Godot.");
        Assert.False(text.Contains("SaveSystem", StringComparison.Ordinal), "Alpha 2.7 should not add save/load.");
        Assert.False(text.Contains("PlayByPlay", StringComparison.Ordinal), "Alpha 2.7 should not add play-by-play.");
        Assert.False(text.Contains("LineMatching", StringComparison.Ordinal), "Alpha 2.7 should not add line matching.");
        Assert.False(text.Contains("MatchupEngine", StringComparison.Ordinal), "Alpha 2.7 should not add a matchup engine.");
        Assert.False(text.Contains("FullTacticalSimulator", StringComparison.Ordinal), "Alpha 2.7 should not add full tactical simulation.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithPlayerTeamGameTomorrow(string gameId)
    {
        var ready = ReadyScenario();
        var opponent = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot).First(team => team.OrganizationId != ready.ScenarioSnapshot.Organization.OrganizationId);
        var game = new ScheduledGame(gameId, ready.ScenarioSnapshot.CurrentDate.AddDays(1), ready.ScenarioSnapshot.Organization.OrganizationId, opponent.OrganizationId);
        return WithSchedule(ready, new[] { game });
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithInjuryGameTomorrow()
    {
        var ready = ReadyScenario();
        var opponent = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot).First(team => team.OrganizationId != ready.ScenarioSnapshot.Organization.OrganizationId);
        var date = ready.ScenarioSnapshot.CurrentDate.AddDays(1);
        var stats = new SeasonStatsService();
        var baseSnapshot = ready.ScenarioSnapshot with
        {
            Standings = stats.CreateStandings(ready.ScenarioSnapshot.Season.LeagueId, SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot))
        };

        for (var index = 0; index < 500; index++)
        {
            var game = new ScheduledGame($"alpha27-injury-game-{index}", date, ready.ScenarioSnapshot.Organization.OrganizationId, opponent.OrganizationId);
            var completed = game.Complete(new BasicGameSimulator().Simulate(game));
            var recap = new GameRecapService().CreateRecap(baseSnapshot, completed);
            if (recap.InjuryNotes.Count > 0)
            {
                return WithSchedule(ready, new[] { game });
            }
        }

        throw new InvalidOperationException("Could not find deterministic injury-note game.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithLeagueGameTomorrow()
    {
        var ready = ReadyScenario();
        var teams = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot)
            .Where(team => team.OrganizationId != ready.ScenarioSnapshot.Organization.OrganizationId)
            .Take(2)
            .ToArray();
        var game = new ScheduledGame("alpha27-league-game", ready.ScenarioSnapshot.CurrentDate.AddDays(1), teams[0].OrganizationId, teams[1].OrganizationId);
        return WithSchedule(ready, new[] { game });
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithNoGames()
    {
        var ready = ReadyScenario();
        return WithSchedule(ready, Array.Empty<ScheduledGame>());
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ScenarioWithCompletedPlayerGame()
    {
        var ready = ScenarioWithPlayerTeamGameTomorrow("alpha27-completed-game");
        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(ready.Registry, ready.ScenarioSnapshot);
        return (ready.Registry, result.ScenarioSnapshot);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) WithSchedule(
        (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ready,
        IReadOnlyList<ScheduledGame> games)
    {
        var worldEngine = new LegacyEngine.World.WorldEngine(ready.Registry.WorldEngine.State, new EventEngine());
        var registry = EngineRegistry.Create(worldEngine, ready.Registry.Rulebook);
        var teams = SeasonFrameworkService.LeagueTeams(ready.ScenarioSnapshot);
        var stats = new SeasonStatsService();
        var alpha = ready.ScenarioSnapshot.AlphaSnapshot with { Injuries = Array.Empty<LegacyEngine.Injuries.Injury>() };
        var snapshot = ready.ScenarioSnapshot with
        {
            AlphaSnapshot = alpha,
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true, SeasonBegun: true),
            Schedule = new GameSchedule("alpha27-test-schedule", ready.ScenarioSnapshot.Season.LeagueId, games),
            Standings = stats.CreateStandings(ready.ScenarioSnapshot.Season.LeagueId, teams),
            TeamStats = stats.CreateTeamStats(teams),
            PlayerStats = stats.CreatePlayerStats(alpha),
            GoalieStats = stats.CreateGoalieStats(alpha),
            GameRecaps = Array.Empty<GameRecap>(),
            MonthlySummaries = Array.Empty<MonthlyGmSummary>()
        };
        snapshot.Validate();
        return (registry, snapshot);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyScenario()
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
                MinRoster = total,
                MaxRoster = total,
                ActiveRoster = active,
                GoaliesRequired = goalies,
                OverageSlots = Math.Max(3, overage),
                ImportSlots = Math.Max(2, imports),
                InjuredReserveEnabled = true,
                ReserveListEnabled = true
            });

        var registry = scenario.Registry with { Rulebook = rulebook };
        var camp = new TrainingCamp(
            "camp-alpha27-ready",
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
            AffiliateRules = source.AffiliateRules,
            FreeAgentRightsRules = source.FreeAgentRightsRules,
            ArbitrationRules = source.ArbitrationRules
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
