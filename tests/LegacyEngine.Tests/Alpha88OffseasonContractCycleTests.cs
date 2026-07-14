using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.World;

internal sealed class Alpha88OffseasonContractCycleTests
{
    public void CycleExpiresContractsAndRefreshesMarket()
    {
        var prepared = ExpiringScenario();
        var result = new OffseasonContractCycleService().Process(prepared.Registry, prepared.Scenario);

        Assert.True(result.Success, result.Message);
        Assert.Equal(ContractStatus.Expired, result.ScenarioSnapshot.Contracts.Single(item => item.PersonId == prepared.PersonId).Status);
        Assert.True(result.ExpiredContractCount == 1, "The cycle should report the newly expired contract.");
        Assert.True(result.ScenarioSnapshot.OffseasonContractCycle.LastProcessedDate == result.ScenarioSnapshot.CurrentDate, "The cycle should record its processing date.");
    }

    public void CycleIsIdempotentForContractExpiryNotice()
    {
        var prepared = ExpiringScenario();
        var service = new OffseasonContractCycleService();
        var first = service.Process(prepared.Registry, prepared.Scenario);
        var second = service.Process(prepared.Registry, first.ScenarioSnapshot);

        Assert.True(first.InboxItems.Any(item => item.EventType == LegacyEventType.ContractExpired), "The first pass should create one useful expiry notice.");
        Assert.False(second.InboxItems.Any(item => item.EventType == LegacyEventType.ContractExpired), "The same expiry should not generate a duplicate notice.");
        Assert.Equal(0, second.ExpiredContractCount);
    }

    public void CycleDoesNotApproveContractsOrMoveRosterPlayers()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var beforeContracts = created.ScenarioSnapshot.Contracts.Count;
        var beforeRoster = created.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count;
        var result = new OffseasonContractCycleService().Process(created.Registry, created.ScenarioSnapshot);

        Assert.Equal(beforeContracts, result.ScenarioSnapshot.Contracts.Count);
        Assert.Equal(beforeRoster, result.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count);
        Assert.False(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.AddToRoster && action.Status == PendingGmActionStatus.Completed), "The cycle must not complete roster actions.");
    }

    public void ContractMarketActionCenterUsesContractWorkspaceContext()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = new ContractMarketService().StartNegotiation(
            created.Registry,
            created.ScenarioSnapshot,
            created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First().PersonId).ScenarioSnapshot;
        var budget = new BudgetOverviewService().Build(scenario, created.Registry.Rulebook!);
        var readiness = new SeasonReadinessService().Evaluate(created.Registry, scenario);
        var items = new ActionCenterService().BuildItems(scenario, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Contracts && item.Title.Contains("Contract negotiation", StringComparison.OrdinalIgnoreCase)), "Open negotiations should appear in Action Center.");
    }

    public void SaveLoadPreservesOffseasonCycleState()
    {
        var prepared = ExpiringScenario();
        var result = new OffseasonContractCycleService().Process(prepared.Registry, prepared.Scenario);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha88-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(result.ScenarioSnapshot, prepared.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(result.ScenarioSnapshot, Array.Empty<InboxMessage>(), result.LeagueTransactions, new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, prepared.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(result.ScenarioSnapshot.OffseasonContractCycle.LastProcessedDate, loaded.SaveGame!.ScenarioSnapshot.OffseasonContractCycle.LastProcessedDate);
        Assert.True(loaded.SaveGame.ScenarioSnapshot.OffseasonContractCycle.ProcessedExpiryNotices.Count > 0, "Expiry notice history should survive save/load.");
    }

    public void ShortOffseasonSoakDoesNotDuplicateContractNotices()
    {
        var prepared = ExpiringScenario();
        var service = new OffseasonContractCycleService();
        var current = prepared.Scenario;
        var totalExpiryNotices = 0;
        for (var day = 0; day < 5; day++)
        {
            var result = service.Process(prepared.Registry, current);
            totalExpiryNotices += result.InboxItems.Count(item => item.EventType == LegacyEventType.ContractExpired);
            current = WithDate(result.ScenarioSnapshot, result.ScenarioSnapshot.CurrentDate.AddDays(1));
        }

        Assert.Equal(1, totalExpiryNotices);
        Assert.True(current.OffseasonContractCycle.ProcessedExpiryNotices.Count == 1, "Repeated offseason passes should keep one expiry notice key.");
    }

    public void AlphaDesktopExposesOffseasonContractCycleContext()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Contract Market", StringComparison.Ordinal), "Contract Market should remain the primary contract workspace.");
        Assert.True(source.Contains("Contract decision", StringComparison.Ordinal), "Desktop should expose contract decision context.");
        Assert.True(source.Contains("ActionCenterCategory.Contracts => (\"Hockey Operations\", \"Contract Management\")", StringComparison.Ordinal), "Contract actions should route directly to Contract Management.");
    }

    private static PreparedScenario ExpiringScenario()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First();
        var currentDate = created.ScenarioSnapshot.CurrentDate;
        var existing = created.ScenarioSnapshot.Contracts.FirstOrDefault(item => item.PersonId == player.PersonId)
            ?? new Contract(
                $"alpha88-expiring:{player.PersonId}",
                player.PersonId,
                created.ScenarioSnapshot.Organization.OrganizationId,
                ContractType.JuniorPlayerAgreement,
                ContractStatus.Signed,
                new ContractTerm(currentDate.AddDays(-30), currentDate),
                new ContractMoney(100_000m),
                Array.Empty<ContractClause>(),
                currentDate.AddDays(-30),
                currentDate.AddDays(-30),
                null,
                null,
                null);
        var expiring = existing with
        {
            Status = ContractStatus.Signed,
            Term = new ContractTerm(currentDate.AddDays(-30), currentDate),
            ExpiredOn = null
        };
        var contracts = created.ScenarioSnapshot.Contracts
            .Where(item => item.ContractId != expiring.ContractId)
            .Append(expiring)
            .ToArray();
        var alpha = created.ScenarioSnapshot.AlphaSnapshot with
        {
            Contracts = created.ScenarioSnapshot.AlphaSnapshot.Contracts
                .Where(item => item.ContractId != expiring.ContractId)
                .Append(expiring)
                .ToArray()
        };
        var scenario = created.ScenarioSnapshot with
        {
            AlphaSnapshot = alpha,
            Contracts = contracts,
            PlayerRightsDecisions = Array.Empty<PlayerRightsDecision>(),
            RightsHistory = RightsHistory.Empty,
            FreeAgencyMarketState = null,
            OffseasonContractCycle = OffseasonContractCycleState.Empty
        };
        scenario.Validate();
        return new PreparedScenario(created.Registry, scenario, player.PersonId);
    }

    private static NewGmScenarioSnapshot WithDate(NewGmScenarioSnapshot scenario, DateOnly date)
    {
        var world = scenario.AlphaSnapshot.WorldState;
        var snapshot = scenario.AlphaSnapshot with { WorldState = world with { Clock = new WorldClock(new WorldDate(date)) } };
        return scenario with { AlphaSnapshot = snapshot };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be found.");
    }

    private sealed record PreparedScenario(EngineRegistry Registry, NewGmScenarioSnapshot Scenario, string PersonId);
}
