using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.World;

internal sealed class Alpha89OffseasonReadinessTests
{
    public void ReadinessReportShowsCampRosterAndMarketState()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var report = new OffseasonRosterReadinessService().BuildReport(created.ScenarioSnapshot, created.Registry.Rulebook);

        Assert.Equal(OffseasonReadinessPhase.ContractReview, report.Phase);
        Assert.True(report.CampOpensOn > created.ScenarioSnapshot.CurrentDate, "Camp should be in the future at scenario start.");
        Assert.True(report.ActiveRosterCount > 0, "Readiness should include the existing roster.");
        Assert.True(report.OpeningRosterTarget > 0, "Readiness should include the opening roster target.");
        Assert.True(report.Summary.Contains("Camp opens", StringComparison.Ordinal), "Readiness should explain the calendar transition.");
    }

    public void DraftToCampTransitionCreatesOneUsefulNotice()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new OffseasonRosterReadinessService();
        var first = service.Process(created.Registry, created.ScenarioSnapshot);
        var atDraft = WithDate(first.ScenarioSnapshot, first.ScenarioSnapshot.DraftDate);
        var second = service.Process(created.Registry, atDraft);
        var third = service.Process(created.Registry, second.ScenarioSnapshot);

        Assert.Equal(OffseasonReadinessPhase.CampPreparation, second.Report.Phase);
        Assert.True(second.InboxItems.Any(item => item.Title == "Camp preparation begins"), "The draft-to-camp transition should be visible once.");
        Assert.False(third.InboxItems.Any(item => item.Title == "Camp preparation begins"), "The same phase transition should not spam the inbox.");
    }

    public void ReadinessDoesNotApproveOrMovePlayers()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var contractCount = created.ScenarioSnapshot.Contracts.Count;
        var rosterCount = created.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count;
        var result = new OffseasonRosterReadinessService().Process(created.Registry, created.ScenarioSnapshot);

        Assert.Equal(contractCount, result.ScenarioSnapshot.Contracts.Count);
        Assert.Equal(rosterCount, result.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count);
        Assert.False(result.ScenarioSnapshot.PendingActions.Any(action => action.Status == PendingGmActionStatus.Completed), "Readiness must not complete pending GM actions.");
    }

    public void ReadinessPreservesStateThroughSaveLoad()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var result = new OffseasonRosterReadinessService().Process(created.Registry, created.ScenarioSnapshot);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha89-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(result.ScenarioSnapshot, created.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(result.ScenarioSnapshot, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, created.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(result.ScenarioSnapshot.OffseasonRosterReadinessState.LastPhase, loaded.SaveGame!.ScenarioSnapshot.OffseasonRosterReadinessState.LastPhase);
        Assert.Equal(result.ScenarioSnapshot.OffseasonRosterReadinessState.LastEvaluatedDate, loaded.SaveGame.ScenarioSnapshot.OffseasonRosterReadinessState.LastEvaluatedDate);
    }

    public void DesktopExposesOffseasonReadinessWorkspace()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Offseason Readiness", StringComparison.Ordinal), "Desktop should expose the offseason readiness workspace.");
        Assert.True(source.Contains("Camp opens", StringComparison.Ordinal), "Desktop should show the camp countdown.");
        Assert.True(source.Contains("No automatic moves", StringComparison.Ordinal), "Desktop should explain that GM decisions remain explicit.");
    }

    public void ReadinessSourceHasNoGodotSaveOrGameSimulationDependency()
    {
        var root = FindRepositoryRoot();
        var source = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "OffseasonRosterReadiness*.cs")
                .Select(File.ReadAllText));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Readiness should not depend on Godot.");
        Assert.False(source.Contains("SaveGameService", StringComparison.Ordinal), "Readiness should not implement save/load.");
        Assert.False(source.Contains("GameSimulation", StringComparison.Ordinal), "Readiness should not implement game simulation.");
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
}
