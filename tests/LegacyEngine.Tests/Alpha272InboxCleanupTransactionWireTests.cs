using LegacyEngine.Events;
using LegacyEngine.Integration;

internal sealed class Alpha272InboxCleanupTransactionWireTests
{
    public void OtherTeamContractSignedGoesToLeagueNewsNotInbox()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        QueueTransaction(
            scenario.Registry,
            scenario.ScenarioSnapshot.CurrentDate,
            LegacyEventType.ContractSigned,
            "org-river-city",
            "player-river-01",
            "River City Royals",
            "Logan Fraser",
            "River City signed Logan Fraser to a junior agreement.",
            "Adds veteran depth.");

        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.False(result.InboxItems.Any(item => item.Title.Contains("Logan Fraser", StringComparison.Ordinal)), "Other-team signing should not enter the GM inbox.");
        Assert.True(result.LeagueTransactions.Any(item => item.PersonName == "Logan Fraser" && item.TransactionType == LeagueTransactionType.ContractSigned), "Other-team signing should appear in League News.");
    }

    public void OtherTeamRosterAddGoesToLeagueNewsNotInbox()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        QueueTransaction(
            scenario.Registry,
            scenario.ScenarioSnapshot.CurrentDate,
            LegacyEventType.PlayerAddedToRoster,
            "org-lakeview",
            "player-lakeview-01",
            "Lakeview Miners",
            "Evan Brooks",
            "Lakeview added Evan Brooks to the roster after camp.",
            "Camp depth decision.");

        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.False(result.InboxItems.Any(item => item.Title.Contains("Evan Brooks", StringComparison.Ordinal)), "Other-team roster move should not enter the GM inbox.");
        Assert.True(result.LeagueTransactions.Any(item => item.PersonName == "Evan Brooks" && item.TransactionType == LeagueTransactionType.PlayerAddedToRoster), "Other-team roster move should appear in League News.");
    }

    public void PlayerTeamContractDecisionGoesToInbox()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var player = scenario.ScenarioSnapshot.AlphaSnapshot.Players[0];
        QueueTransaction(
            scenario.Registry,
            scenario.ScenarioSnapshot.CurrentDate,
            LegacyEventType.ContractOffered,
            scenario.ScenarioSnapshot.Organization.OrganizationId,
            player.PersonId,
            scenario.ScenarioSnapshot.Organization.Name,
            player.Identity.DisplayName,
            $"{scenario.ScenarioSnapshot.Organization.Name} offered {player.Identity.DisplayName} a contract for GM review.",
            "GM approval required.");

        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.True(result.InboxItems.Any(item => item.PrimaryPersonId == player.PersonId && item.EventType == LegacyEventType.ContractOffered), "Player-team contract decision should enter the GM inbox.");
        Assert.False(result.LeagueTransactions.Any(item => item.PersonId == player.PersonId), "Player-team contract decision should not be treated as outside league noise.");
    }

    public void VagueSystemMessagesAreFiltered()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var player = scenario.ScenarioSnapshot.AlphaSnapshot.Players[0];
        var legacyEvent = scenario.Registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.ScenarioSnapshot.CurrentDate.Year, scenario.ScenarioSnapshot.CurrentDate.Month, scenario.ScenarioSnapshot.CurrentDate.Day, 9, 0, 0, TimeSpan.Zero),
            LegacyEventType.ContractSigned,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            "Contract signed",
            "Contract signed",
            new LegacyEventContext(PrimaryPersonId: player.PersonId, OrganizationId: scenario.ScenarioSnapshot.Organization.OrganizationId),
            new Dictionary<string, object?>
            {
                ["team_name"] = scenario.ScenarioSnapshot.Organization.Name,
                ["player_name"] = player.Identity.DisplayName
            });
        scenario.Registry.EventEngine.QueueEvent(legacyEvent);

        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.False(result.InboxItems.Any(item => item.EventType == LegacyEventType.ContractSigned), "Vague system contract message should not enter inbox.");
        Assert.False(result.LeagueTransactions.Any(item => item.PersonId == player.PersonId), "Vague same-team system message should not be re-routed to League News.");
    }

    public void LeagueNewsFeedDisplaysTransactions()
    {
        var service = new LeagueTransactionWireService();
        var legacyEvent = new EventEngine().CreateEvent(
            DateTimeOffset.Parse("2026-09-01T12:00:00Z"),
            LegacyEventType.PlayerReleased,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            "Roster move",
            "Released after camp.",
            new LegacyEventContext(PrimaryPersonId: "player-1", OrganizationId: "org-1"),
            new Dictionary<string, object?>
            {
                ["team_name"] = "Summit Ridge Hawks",
                ["player_name"] = "Noah Tremblay",
                ["reason"] = "Roster cutdown."
            });

        var transactions = service.BuildTransactions(new[] { legacyEvent }, category: LeagueNewsCategory.RosterMoves);

        Assert.Equal(1, transactions.Count);
        Assert.Equal(LeagueTransactionType.PlayerReleased, transactions[0].TransactionType);
        Assert.True(transactions[0].Description.Contains("Summit Ridge Hawks", StringComparison.Ordinal), "Description should name the team.");
        Assert.True(transactions[0].Description.Contains("Noah Tremblay", StringComparison.Ordinal), "Description should name the player.");
    }

    public void InboxRemainsTeamDecisionFocused()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        QueueTransaction(
            scenario.Registry,
            scenario.ScenarioSnapshot.CurrentDate,
            LegacyEventType.ContractSigned,
            "org-river-city",
            "player-river-02",
            "River City Royals",
            "Caleb Moore",
            "River City signed Caleb Moore.",
            "Routine transaction.");
        QueueTransaction(
            scenario.Registry,
            scenario.ScenarioSnapshot.CurrentDate,
            LegacyEventType.ContractOffered,
            scenario.ScenarioSnapshot.Organization.OrganizationId,
            scenario.ScenarioSnapshot.AlphaSnapshot.Players[1].PersonId,
            scenario.ScenarioSnapshot.Organization.Name,
            scenario.ScenarioSnapshot.AlphaSnapshot.Players[1].Identity.DisplayName,
            "Contract decision requires GM review.",
            "GM approval required.");

        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.Equal(1, result.InboxItems.Count(item => item.EventType == LegacyEventType.ContractOffered || item.EventType == LegacyEventType.ContractSigned));
        Assert.Equal(1, result.LeagueTransactions.Count(item => item.TransactionType == LeagueTransactionType.ContractSigned));
    }

    public void AlphaDesktopExposesLeagueNewsTab()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("AddTab(tabs, \"League News\")", StringComparison.Ordinal), "AlphaDesktop should expose League News.");
        Assert.True(text.Contains("League News / Transaction Wire", StringComparison.Ordinal), "League News should name the transaction wire.");
        Assert.True(text.Contains("Filters: All | Signings | Roster Moves | Injuries | Draft | Staff", StringComparison.Ordinal), "League News should expose transaction filters.");
    }

    private static void QueueTransaction(
        EngineRegistry registry,
        DateOnly date,
        LegacyEventType eventType,
        string organizationId,
        string personId,
        string teamName,
        string personName,
        string description,
        string reason)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            eventType.ToString(),
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: organizationId),
            new Dictionary<string, object?>
            {
                ["team_name"] = teamName,
                ["player_name"] = personName,
                ["reason"] = reason
            });
        registry.EventEngine.QueueEvent(legacyEvent);
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

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
