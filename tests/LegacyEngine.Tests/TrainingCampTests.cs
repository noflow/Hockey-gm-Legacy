using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Organizations;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class TrainingCampTests
{
    public void TrainingCampCanBeCreated()
    {
        var ready = ReadyForCamp();
        var result = new TrainingCampService().OpenCamp(ready.Registry, ready.ScenarioSnapshot);

        Assert.Equal(ready.ScenarioSnapshot.Organization.OrganizationId, result.Camp.OrganizationId);
        Assert.True(result.Camp.Players.Count > 0, "Camp should contain invited players.");
    }

    public void ReturningRosterPlayersCanBeInvited()
    {
        var ready = ReadyForCamp();
        var result = new TrainingCampService().OpenCamp(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Camp.Players.Any(player => player.InviteType == TrainingCampInviteType.ReturningRosterPlayer), "Returning roster players should be invited.");
    }

    public void DraftedProspectsRecruitsAndTryoutsCanBeInvited()
    {
        var ready = ReadyForCamp();
        var service = new TrainingCampService();
        var opened = service.OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        var tryoutPerson = ready.ScenarioSnapshot.AlphaSnapshot.People.First(person => opened.Camp.FindPlayer(person.PersonId) is null);
        var invitedTryout = service.InvitePlayer(
            ready.Registry,
            opened.ScenarioSnapshot,
            tryoutPerson.PersonId,
            RosterPosition.Center,
            TrainingCampInviteType.Tryout,
            PlayerAcquisitionSource.Tryout);

        Assert.True(invitedTryout.Camp.Players.Any(player => player.InviteType == TrainingCampInviteType.DraftedProspect), "Drafted prospects should be invited after the draft.");
        Assert.True(invitedTryout.Camp.Players.Any(player => player.InviteType == TrainingCampInviteType.Recruit), "Recruits should be invited.");
        Assert.True(invitedTryout.Camp.Players.Any(player => player.InviteType == TrainingCampInviteType.Tryout), "Tryouts should be inviteable.");
    }

    public void AhlAssignedFromParentPlayerCanBeInvited()
    {
        var ready = ReadyForCampWithRulebook(RulebookPresets.Create(DraftLeaguePreset.AhlStyle), parentOrganizationId: "org-nhl-parent");
        var service = new TrainingCampService();
        var opened = service.OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        var invited = service.InvitePlayer(
            ready.Registry,
            opened.ScenarioSnapshot,
            "person-parent-prospect-001",
            RosterPosition.Defense,
            TrainingCampInviteType.AssignedFromParentClub,
            PlayerAcquisitionSource.AssignedFromParentClub,
            "org-nhl-parent");

        Assert.True(invited.Camp.Players.Any(player => player.InviteType == TrainingCampInviteType.AssignedFromParentClub), "AHL parent assignments should be inviteable.");
    }

    public void CampEvaluationIsGenerated()
    {
        var ready = ReadyForCamp();
        var service = new TrainingCampService();
        var opened = service.OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        var evaluated = service.EvaluateCamp(ready.Registry, opened.ScenarioSnapshot);

        Assert.True(evaluated.Camp.Evaluations.Count > 0, "Camp should generate evaluations.");
    }

    public void EvaluationIncludesPlayerName()
    {
        var ready = ReadyForCamp();
        var service = new TrainingCampService();
        var opened = service.OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        var evaluated = service.EvaluateCamp(ready.Registry, opened.ScenarioSnapshot);

        Assert.False(string.IsNullOrWhiteSpace(evaluated.Camp.Evaluations.First().PlayerName), "Evaluation should include player name.");
    }

    public void KeepDecisionChangesStatus()
    {
        var ready = OpenReadyCamp();
        var player = ready.ScenarioSnapshot.TrainingCamp!.Players.First();
        var result = new TrainingCampService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new TrainingCampDecision(player.PersonId, TrainingCampDecisionType.Keep, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, "Keep decision should succeed.");
        Assert.Equal(TrainingCampStatus.Kept, result.Camp.FindPlayer(player.PersonId)!.Status);
    }

    public void CutDecisionChangesStatus()
    {
        var ready = OpenReadyCamp();
        var player = ready.ScenarioSnapshot.TrainingCamp!.Players.First();
        var result = new TrainingCampService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new TrainingCampDecision(player.PersonId, TrainingCampDecisionType.Cut, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, "Cut decision should succeed.");
        Assert.Equal(TrainingCampStatus.Cut, result.Camp.FindPlayer(player.PersonId)!.Status);
    }

    public void AssignToAffiliateWorksWhenSupported()
    {
        var ready = ReadyForCampWithRulebook(RulebookPresets.Create(DraftLeaguePreset.AhlStyle), affiliateOrganizationId: "org-ahl-affiliate");
        var service = new TrainingCampService();
        var opened = service.OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        var player = opened.Camp.Players.First();
        var result = service.ApplyDecision(
            ready.Registry,
            opened.ScenarioSnapshot,
            new TrainingCampDecision(player.PersonId, TrainingCampDecisionType.AssignToAffiliate, opened.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(TrainingCampStatus.AssignedToAffiliate, result.Camp.FindPlayer(player.PersonId)!.Status);
    }

    public void JuniorAffiliateDecisionsAreUnavailableByDefault()
    {
        var ready = OpenReadyCamp();
        var player = ready.ScenarioSnapshot.TrainingCamp!.Players.First();
        var result = new TrainingCampService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new TrainingCampDecision(player.PersonId, TrainingCampDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));

        Assert.False(result.Success, "Junior-style camp should not allow affiliate assignment by default.");
    }

    public void ReturnToParentWorksForAhlStyleSource()
    {
        var ready = ReadyForCampWithRulebook(RulebookPresets.Create(DraftLeaguePreset.AhlStyle), parentOrganizationId: "org-nhl-parent");
        var service = new TrainingCampService();
        var opened = service.OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        var invited = service.InvitePlayer(
            ready.Registry,
            opened.ScenarioSnapshot,
            "person-parent-prospect-002",
            RosterPosition.LeftWing,
            TrainingCampInviteType.AssignedFromParentClub,
            PlayerAcquisitionSource.AssignedFromParentClub,
            "org-nhl-parent");
        var result = service.ApplyDecision(
            ready.Registry,
            invited.ScenarioSnapshot,
            new TrainingCampDecision("person-parent-prospect-002", TrainingCampDecisionType.ReturnToParent, invited.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(TrainingCampStatus.ReturnedToParent, result.Camp.FindPlayer("person-parent-prospect-002")!.Status);
    }

    public void OpeningRosterValidationUsesRuleEngine()
    {
        var ready = OpenReadyCamp();
        var result = new TrainingCampService().CompleteCamp(ready.Registry, ready.ScenarioSnapshot);

        Assert.Equal(RuleErrorCodes.ActiveRosterTooLarge, result.Camp.Summary!.RosterValidationResult.RuleCode);
    }

    public void CampSummaryIsGenerated()
    {
        var ready = OpenReadyCamp();
        var result = new TrainingCampService().CompleteCamp(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Camp.Summary!.PlayersInvited > 0, "Summary should count invited players.");
        Assert.False(string.IsNullOrWhiteSpace(result.Camp.Summary.StaffSummary), "Summary should include staff text.");
    }

    public void CampEventsAreCreated()
    {
        var ready = ReadyForCamp();
        var service = new TrainingCampService();
        var opened = service.OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        var evaluated = service.EvaluateCamp(ready.Registry, opened.ScenarioSnapshot);
        service.CompleteCamp(ready.Registry, evaluated.ScenarioSnapshot);

        Assert.True(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.TrainingCampOpened), "Camp open event should be queued.");
        Assert.True(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.TrainingCampEvaluationCreated), "Camp evaluation event should be queued.");
        Assert.True(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.TrainingCampCompleted), "Camp completed event should be queued.");
    }

    public void InboxItemsCanBeGeneratedForCampEvents()
    {
        var ready = ReadyForCamp();
        var result = new TrainingCampService().OpenCamp(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.TrainingCampOpened), "Opening camp should create an inbox item.");
        Assert.Equal(InboxCategory.Staff, InboxManager.Categorize(result.InboxItems.First()));
    }

    public void AlphaDesktopExposesTrainingCampSurfaceAndActions()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Training Camp", StringComparison.Ordinal), "AlphaDesktop should expose a Training Camp surface.");
        Assert.True(text.Contains("Open Camp", StringComparison.Ordinal), "AlphaDesktop should expose Open Camp.");
        Assert.True(text.Contains("Evaluate Camp", StringComparison.Ordinal), "AlphaDesktop should expose Evaluate Camp.");
        Assert.True(text.Contains("Complete Camp", StringComparison.Ordinal), "AlphaDesktop should expose Complete Camp.");
    }

    public void TrainingCampHasNoGodotSaveOrGameSimulationDependency()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "TrainingCamp*.cs");
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Training camp should not depend on Godot.");
        Assert.False(text.Contains("Save", StringComparison.Ordinal), "Training camp should not implement save/load.");
        Assert.False(text.Contains("GameSimulation", StringComparison.Ordinal), "Training camp should not implement game simulation.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) OpenReadyCamp()
    {
        var ready = ReadyForCamp();
        var opened = new TrainingCampService().OpenCamp(ready.Registry, ready.ScenarioSnapshot);
        return (ready.Registry, opened.ScenarioSnapshot);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyForCamp() =>
        ReadyForCampWithRulebook(RulebookPresets.Create(DraftLeaguePreset.JuniorMajor));

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyForCampWithRulebook(
        Rulebook rulebook,
        string? parentOrganizationId = null,
        string? affiliateOrganizationId = null)
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var registry = scenario.Registry with { Rulebook = rulebook };
        var completed = new AlphaDraftExperienceService().SimulateToCompletion(registry, scenario.ScenarioSnapshot);
        var organization = completed.ScenarioSnapshot.Organization with
        {
            ParentOrganizationId = parentOrganizationId,
            AffiliateOrganizationId = affiliateOrganizationId
        };
        var scenarioSnapshot = completed.ScenarioSnapshot with
        {
            Organization = organization,
            AlphaSnapshot = completed.ScenarioSnapshot.AlphaSnapshot with { Organization = organization }
        };

        return (registry, scenarioSnapshot);
    }

    private static NewGmScenarioResult AdvanceToDraftDay(NewGmScenarioResult scenario)
    {
        var snapshot = scenario.AlphaSnapshot;
        var scenarioSnapshot = scenario.ScenarioSnapshot;
        var coordinator = new DailySimulationCoordinator();

        while (snapshot.CurrentDate < scenarioSnapshot.DraftDate)
        {
            var result = coordinator.AdvanceOneDay(scenario.Registry, snapshot);
            snapshot = result.WorldSnapshot;
            scenarioSnapshot = scenarioSnapshot with
            {
                AlphaSnapshot = snapshot,
                Season = snapshot.Season ?? scenarioSnapshot.Season
            };
        }

        return scenario with
        {
            AlphaSnapshot = snapshot,
            ScenarioSnapshot = scenarioSnapshot
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

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
