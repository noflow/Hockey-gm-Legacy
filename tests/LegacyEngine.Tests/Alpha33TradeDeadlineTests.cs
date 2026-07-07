using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Seasons;
using LegacyEngine.World;

internal sealed class Alpha33TradeDeadlineTests
{
    public void DeadlineDateComesFromSeasonCalendar()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var expected = created.ScenarioSnapshot.Season.Calendar.Milestones.Single(item => item.Type == SeasonMilestoneType.TradeDeadline).Date.Value;
        var settings = new TradeDeadlineService().BuildSettings(created.ScenarioSnapshot, created.Registry.Rulebook);

        Assert.Equal(expected, settings.DeadlineDate);
    }

    public void StatusChangesAtDeadlineWindows()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var deadline = DeadlineDate(created.ScenarioSnapshot);
        var service = new TradeDeadlineService();

        Assert.Equal(TradeDeadlineStatus.NotStarted, service.GetWindow(WithDate(created.ScenarioSnapshot, deadline.AddDays(-31)), created.Registry.Rulebook).Status);
        Assert.Equal(TradeDeadlineStatus.Approaching, service.GetWindow(WithDate(created.ScenarioSnapshot, deadline.AddDays(-30)), created.Registry.Rulebook).Status);
        Assert.Equal(TradeDeadlineStatus.DeadlineWeek, service.GetWindow(WithDate(created.ScenarioSnapshot, deadline.AddDays(-7)), created.Registry.Rulebook).Status);
        Assert.Equal(TradeDeadlineStatus.DeadlineDay, service.GetWindow(WithDate(created.ScenarioSnapshot, deadline), created.Registry.Rulebook).Status);
        Assert.Equal(TradeDeadlineStatus.Closed, service.GetWindow(WithDate(created.ScenarioSnapshot, deadline.AddDays(1)), created.Registry.Rulebook).Status);
    }

    public void BuyerSellerAssessmentGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var assessment = new TradeDeadlineService().AssessBuyerSeller(scenario);

        Assert.True(assessment.LeagueStrategies.Count > 1, "Buyer/seller assessment should include league strategies.");
        Assert.True(assessment.PlayerTeamStrategy.Direction != TeamTradeDirection.Unknown, "Player team should receive a useful direction.");
        Assert.True(!string.IsNullOrWhiteSpace(assessment.Summary), "Assessment should explain the deadline posture.");
    }

    public void TradeBlockExpandsNearDeadline()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(-30));
        var before = scenario.TradeBlock!.Entries.Count;
        var result = new TradeDeadlineService().AdvanceDeadline(created.Registry, scenario);

        Assert.True(result.ScenarioSnapshot.TradeBlock!.Entries.Count > before, "Deadline window should expand the trade block.");
        Assert.True(result.ScenarioSnapshot.TradeDeadlineState?.LastTradeBlockUpdate?.AddedPlayers > 0, "Trade block expansion should be recorded.");
    }

    public void DeadlineRumorsGenerated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(-7));
        var result = new TradeDeadlineService().AdvanceDeadline(created.Registry, scenario);

        Assert.True(result.ScenarioSnapshot.TradeDeadlineState!.Rumors.Count > 0, "Deadline should generate rumors.");
        Assert.True(result.LeagueTransactions.Any(item => item.Category == LeagueNewsCategory.Deadline), "Rumors should reach League News.");
    }

    public void DashboardExposesDeadlineCard()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Trade Deadline", StringComparison.Ordinal), "Dashboard should expose trade deadline card text.");
        Assert.True(source.Contains("TradeDeadlineWindow", StringComparison.Ordinal), "Desktop state should expose deadline window.");
    }

    public void ActionCenterExposesDeadlineActions()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(-7));
        var budget = new BudgetOverviewService().Build(scenario, created.Registry.Rulebook!);
        var readiness = new SeasonReadinessService().Evaluate(created.Registry, scenario);
        var vacancies = new StaffOfficeService().BuildVacancies(scenario, created.Registry.Rulebook!);
        var items = new ActionCenterService().BuildItems(scenario, Array.Empty<InboxMessage>(), budget, readiness, vacancies);

        Assert.True(items.Any(item => item.Title.Contains("trade deadline", StringComparison.OrdinalIgnoreCase)), "Action Center should expose deadline review action.");
    }

    public void OwnerCoachAssistantMessagesGeneratedButLimited()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(-7));
        var result = new TradeDeadlineService().AdvanceDeadline(created.Registry, scenario);

        Assert.True(result.InboxItems.Count <= 5, "Deadline messages should be limited, not spammy.");
        Assert.True(result.InboxItems.Any(item => item.Title.Contains("Owner", StringComparison.OrdinalIgnoreCase)), "Owner deadline expectation should be generated.");
        Assert.True(result.InboxItems.Any(item => item.Title.Contains("Assistant", StringComparison.OrdinalIgnoreCase)), "Assistant GM recommendation should be generated.");
        Assert.True(result.InboxItems.Any(item => item.Title.Contains("Coach", StringComparison.OrdinalIgnoreCase)), "Coach deadline note should be generated.");
    }

    public void TradesAllowedBeforeDeadline()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(-1));
        var offer = BuildOffer(scenario);
        var result = new TradeService().ProposeTrade(created.Registry, scenario, offer);

        Assert.True(result.Message != "Trade deadline has passed.", "Trades should remain allowed before the deadline closes.");
    }

    public void TradesBlockedAfterDeadline()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(1));
        var offer = BuildOffer(scenario);
        var result = new TradeService().ProposeTrade(created.Registry, scenario, offer);

        Assert.False(result.Success, "New trades should be blocked after deadline.");
        Assert.Equal(TradeOfferStatus.FailedValidation, result.TradeOffer!.Status);
        Assert.Equal("Trade deadline has passed.", result.Message);
    }

    public void TradeUiShowsClosedStateAfterDeadline()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Trade deadline has passed", StringComparison.Ordinal), "Trade UI should show closed reason.");
        Assert.True(source.Contains("TradesAllowed", StringComparison.Ordinal), "Trade UI should disable proposal actions from deadline state.");
    }

    public void LeagueNewsPostsDeadlineClosed()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(1));
        var result = new TradeDeadlineService().AdvanceDeadline(created.Registry, scenario);

        Assert.True(result.LeagueTransactions.Any(item => item.TransactionType == LeagueTransactionType.TradeDeadline && item.Description.Contains("Closed", StringComparison.OrdinalIgnoreCase)), "Deadline close should post to league news.");
    }

    public void DeadlineDoesNotSpamInbox()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithDate(created.ScenarioSnapshot, DeadlineDate(created.ScenarioSnapshot).AddDays(-7));
        var first = new TradeDeadlineService().AdvanceDeadline(created.Registry, scenario);
        var second = new TradeDeadlineService().AdvanceDeadline(created.Registry, first.ScenarioSnapshot);

        Assert.True(first.InboxItems.Count <= 5, "Initial deadline update should be limited.");
        Assert.Equal(0, second.InboxItems.Count);
    }

    public void Alpha33HasNoGodotSaveRealtimeOrFullNegotiation()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*TradeDeadline*.cs", SearchOption.TopDirectoryOnly)
            .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 3.3 should not add Godot.");
        Assert.False(text.Contains("SaveGame", StringComparison.Ordinal), "Alpha 3.3 should not add save/load.");
        Assert.False(text.Contains("RealTime", StringComparison.Ordinal), "Alpha 3.3 should not add real-time deadline clock.");
        Assert.False(text.Contains("RetainedSalary", StringComparison.Ordinal), "Alpha 3.3 should not add retained salary.");
    }

    private static TradeOffer BuildOffer(NewGmScenarioSnapshot scenario)
    {
        var service = new TradeService();
        var target = scenario.TradeBlock!.Entries.OrderBy(entry => entry.AssetValue).First();
        var outgoing = scenario.AlphaSnapshot.Roster.ActivePlayers.FirstOrDefault(player => player.Position == target.Position)
            ?? scenario.AlphaSnapshot.Roster.ActivePlayers[0];
        return service.CreateOffer(
            scenario,
            target.OrganizationId,
            target.TeamName,
            new[] { service.CreateRosterPlayerAsset(scenario, outgoing.PersonId) },
            new[] { service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization) });
    }

    private static DateOnly DeadlineDate(NewGmScenarioSnapshot scenario) =>
        scenario.Season.Calendar.Milestones.Single(item => item.Type == SeasonMilestoneType.TradeDeadline).Date.Value;

    private static NewGmScenarioSnapshot WithDate(NewGmScenarioSnapshot scenario, DateOnly date)
    {
        var world = scenario.AlphaSnapshot.WorldState with { Clock = new WorldClock(new WorldDate(date)) };
        var season = scenario.Season with
        {
            CurrentDate = new SeasonDate(date),
            CurrentPhase = scenario.Season.PhaseOn(date)
        };
        return scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { WorldState = world, Season = season },
            Season = season
        };
    }

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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
