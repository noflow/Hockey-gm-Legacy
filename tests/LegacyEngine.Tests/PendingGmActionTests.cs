using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;

internal sealed class PendingGmActionTests
{
    public void AdvanceDayDoesNotAutoSignRecruit()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits.First();
        var pending = new PendingGmActionService().CreateForRecruitCommitment(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            recruit.RecruitPersonId);
        var contractCount = pending.ScenarioSnapshot.Contracts.Count;

        var advanced = new DailySimulationCoordinator().AdvanceOneDay(scenario.Registry, pending.ScenarioSnapshot.AlphaSnapshot);
        var updatedScenario = pending.ScenarioSnapshot with { AlphaSnapshot = advanced.WorldSnapshot };

        Assert.Equal(contractCount, updatedScenario.Contracts.Count);
        Assert.True(updatedScenario.PendingActions.Any(action => action.Status == PendingGmActionStatus.Pending), "Pending sign recruit action should survive day advancement.");
    }

    public void AdvanceDayDoesNotAutoAddPlayerToRoster()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var personId = PersonNotOnRoster(scenario.ScenarioSnapshot);
        var pending = new PendingGmActionService().CreatePendingAction(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            PendingGmActionType.AddToRoster,
            personId,
            "Staff recommends adding this player, but the GM must approve.",
            "Approve adding the player to the active roster or decline.",
            RosterPosition.Center,
            PlayerAcquisitionSource.FreeAgentSigning);
        var rosterCount = pending.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count;

        var advanced = new DailySimulationCoordinator().AdvanceOneDay(scenario.Registry, pending.ScenarioSnapshot.AlphaSnapshot);
        var updatedScenario = pending.ScenarioSnapshot with { AlphaSnapshot = advanced.WorldSnapshot };

        Assert.Equal(rosterCount, updatedScenario.AlphaSnapshot.Roster.Players.Count);
        Assert.True(updatedScenario.PendingActions.Any(action => action.Status == PendingGmActionStatus.Pending), "Pending add roster action should survive day advancement.");
    }

    public void RecruitCommitmentCreatesPendingGmAction()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits.First();
        var result = new PendingGmActionService().CreateForRecruitCommitment(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            recruit.RecruitPersonId);

        Assert.Equal(PendingGmActionType.SignRecruit, result.Action.ActionType);
        Assert.Equal(PendingGmActionStatus.Pending, result.Action.Status);
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionId == result.Action.ActionId), "Scenario should track pending action.");
    }

    public void ApprovingSignRecruitCreatesContract()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits.First();
        var service = new PendingGmActionService();
        var pending = service.CreateForRecruitCommitment(scenario.Registry, scenario.ScenarioSnapshot, recruit.RecruitPersonId);
        var contractCount = pending.ScenarioSnapshot.Contracts.Count;

        var approved = service.Approve(scenario.Registry, pending.ScenarioSnapshot, pending.Action.ActionId);

        Assert.True(approved.Success, approved.Message);
        Assert.Equal(contractCount + 1, approved.ScenarioSnapshot.Contracts.Count);
        Assert.True(approved.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == recruit.RecruitPersonId && contract.Status == ContractStatus.Signed), "Approved recruit should have a signed contract.");
    }

    public void DecliningSignRecruitDoesNotCreateContract()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits.First();
        var service = new PendingGmActionService();
        var pending = service.CreateForRecruitCommitment(scenario.Registry, scenario.ScenarioSnapshot, recruit.RecruitPersonId);
        var contractCount = pending.ScenarioSnapshot.Contracts.Count;

        var declined = service.Decline(scenario.Registry, pending.ScenarioSnapshot, pending.Action.ActionId);

        Assert.True(declined.Success, declined.Message);
        Assert.Equal(contractCount, declined.ScenarioSnapshot.Contracts.Count);
        Assert.False(declined.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == recruit.RecruitPersonId), "Declined recruit should not receive a contract.");
    }

    public void ApprovingAddToRosterAddsPlayer()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var registry = scenario.Registry with { Rulebook = null };
        var personId = PersonNotOnRoster(scenario.ScenarioSnapshot);
        var service = new PendingGmActionService();
        var pending = service.CreatePendingAction(
            registry,
            scenario.ScenarioSnapshot,
            PendingGmActionType.AddToRoster,
            personId,
            "Coach recommends adding this player to the roster.",
            "Approve adding the player to the active roster or decline.",
            RosterPosition.Center,
            PlayerAcquisitionSource.FreeAgentSigning);
        var rosterCount = pending.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count;

        var approved = service.Approve(registry, pending.ScenarioSnapshot, pending.Action.ActionId);

        Assert.True(approved.Success, approved.Message);
        Assert.Equal(rosterCount + 1, approved.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count);
        Assert.True(approved.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == personId), "Approved add-to-roster action should add the player.");
    }

    public void DecliningAddToRosterDoesNotAddPlayer()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var personId = PersonNotOnRoster(scenario.ScenarioSnapshot);
        var service = new PendingGmActionService();
        var pending = service.CreatePendingAction(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            PendingGmActionType.AddToRoster,
            personId,
            "Coach recommends adding this player to the roster.",
            "Approve adding the player to the active roster or decline.",
            RosterPosition.Center,
            PlayerAcquisitionSource.FreeAgentSigning);
        var rosterCount = pending.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count;

        var declined = service.Decline(scenario.Registry, pending.ScenarioSnapshot, pending.Action.ActionId);

        Assert.True(declined.Success, declined.Message);
        Assert.Equal(rosterCount, declined.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count);
        Assert.False(declined.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == personId), "Declined add-to-roster action should not add the player.");
    }

    public void InboxMessageIsGeneratedForPendingAction()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits.First();
        var result = new PendingGmActionService().CreateForRecruitCommitment(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            recruit.RecruitPersonId);

        Assert.True(result.InboxItems.Any(item => item.PrimaryPersonId == recruit.RecruitPersonId), "Pending action should create an inbox item.");
    }

    public void AlphaDesktopExposesPendingActions()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Pending Actions", StringComparison.Ordinal), "AlphaDesktop should expose a Pending Actions tab.");
        Assert.True(text.Contains("Approve Pending", StringComparison.Ordinal), "AlphaDesktop should expose pending approval.");
        Assert.True(text.Contains("Decline Pending", StringComparison.Ordinal), "AlphaDesktop should expose pending decline.");
    }

    public void PendingActionsHaveNoGodotSaveOrFullGameSimulationDependency()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "PendingGmAction*.cs");
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Pending GM actions should not depend on Godot.");
        Assert.False(text.Contains("Save", StringComparison.Ordinal), "Pending GM actions should not implement save/load.");
        Assert.False(text.Contains("GameSimulation", StringComparison.Ordinal), "Pending GM actions should not implement full game simulation.");
    }

    private static string PersonNotOnRoster(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.People
            .First(person => scenario.AlphaSnapshot.Roster.FindPlayer(person.PersonId) is null)
            .PersonId;

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
