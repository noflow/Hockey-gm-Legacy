using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class ProspectDecisionTests
{
    public void DraftedPlayerStartsAsDraftRightsHeld()
    {
        var ready = ReadyForPlayerPick();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var drafted = new AlphaDraftExperienceService().MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.Equal(ProspectStatus.DraftRightsHeld, drafted.ScenarioSnapshot.ProspectRights.Single(item => item.ProspectPersonId == prospect.ProspectPersonId).Status);
    }

    public void DraftedPlayerHasNonContractProspectPaths()
    {
        var ready = ReadyForPlayerPick();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var drafted = new AlphaDraftExperienceService().MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);
        var available = new ProspectDecisionService().AvailableDecisions(ready.Registry, drafted.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.True(available.Contains(ProspectDecisionType.OfferContract), "Drafted player should allow an explicit contract offer.");
        Assert.True(available.Contains(ProspectDecisionType.InviteToCamp), "Drafted player should allow a camp invite without signing first.");
        Assert.True(available.Contains(ProspectDecisionType.ReturnToJunior), "Drafted player should allow return to junior/youth while retaining rights where allowed.");
        Assert.False(drafted.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.SignDraftPick && action.PersonId == prospect.ProspectPersonId), "Drafted player should not require signing before other prospect decisions.");
    }

    public void DraftedPlayerIsNotActiveRosterByDefault()
    {
        var ready = ReadyForPlayerPick();
        var originalRosterCount = ready.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count;
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var drafted = new AlphaDraftExperienceService().MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.Equal(originalRosterCount, drafted.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count);
        Assert.False(drafted.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == prospect.ProspectPersonId), "Drafted prospect should not join active roster automatically.");
    }

    public void OfferContractCreatesPendingAction()
    {
        var ready = ReadyWithDraftedProspects();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();
        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.OfferContract, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(ProspectStatus.ContractOffered, result.Prospect.Status);
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.IsOpen && action.PersonId == prospect.ProspectPersonId && action.ActionType == PendingGmActionType.SignDraftPick), "Offer contract should create or preserve a pending signing action.");
    }

    public void ApprovingSigningCreatesContract()
    {
        var ready = ReadyWithDraftedProspects();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();
        var offered = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.OfferContract, ready.ScenarioSnapshot.CurrentDate));
        var action = offered.ScenarioSnapshot.PendingActions.First(item => item.IsOpen && item.PersonId == prospect.ProspectPersonId && item.ActionType == PendingGmActionType.SignDraftPick);
        var approved = new PendingGmActionService().Approve(ready.Registry, offered.ScenarioSnapshot, action.ActionId);

        Assert.True(approved.Success, approved.Message);
        Assert.True(approved.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prospect.ProspectPersonId), "Approved signing should create a contract.");
        Assert.Equal(ProspectStatus.Signed, approved.ScenarioSnapshot.ProspectRights.Single(item => item.ProspectPersonId == prospect.ProspectPersonId).Status);
    }

    public void InviteToCampAddsTrainingCampInvite()
    {
        var ready = ReadyWithDraftedProspects();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();
        var invited = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.InviteToCamp, ready.ScenarioSnapshot.CurrentDate));
        var opened = new TrainingCampService().OpenCamp(ready.Registry, invited.ScenarioSnapshot);

        Assert.Equal(ProspectStatus.InvitedToCamp, invited.Prospect.Status);
        Assert.True(opened.Camp.Players.Any(player => player.PersonId == prospect.ProspectPersonId && player.InviteType == TrainingCampInviteType.DraftedProspect), "Explicitly invited drafted prospect should appear in camp.");
    }

    public void ReturnToJuniorChangesProspectStatus()
    {
        var ready = ReadyWithDraftedProspects();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();
        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.ReturnToJunior, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(ProspectStatus.ReturnedToJunior, result.Prospect.Status);
        Assert.True(result.ScenarioSnapshot.ProspectRights.Any(item => item.ProspectPersonId == prospect.ProspectPersonId), "Returning to junior should retain prospect rights record.");
        Assert.False(result.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prospect.ProspectPersonId), "Returning to junior should not create a contract.");
    }

    public void ReturnToYouthTeamChangesProspectStatus()
    {
        var ready = ReadyWithDraftedProspects();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();
        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.ReturnToYouthTeam, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(ProspectStatus.ReturnedToYouthTeam, result.Prospect.Status);
    }

    public void AssignToAffiliateOnlyAvailableWhenRulebookSupportsIt()
    {
        var junior = ReadyWithDraftedProspects();
        var juniorProspect = junior.ScenarioSnapshot.ProspectRights.First();
        var blocked = new ProspectDecisionService().ApplyDecision(
            junior.Registry,
            junior.ScenarioSnapshot,
            new ProspectDecision(juniorProspect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, junior.ScenarioSnapshot.CurrentDate));

        var nhl = ReadyWithDraftedProspects(RulebookPresets.Create(DraftLeaguePreset.NhlStyle), affiliateOrganizationId: "org-ahl-affiliate");
        var nhlProspect = nhl.ScenarioSnapshot.ProspectRights.First();
        nhlProspect = nhlProspect with
        {
            Age = 20,
            Status = ProspectStatus.Signed,
            DevelopmentLevel = PlayerDevelopmentLevel.Junior
        };
        var nhlScenario = nhl.ScenarioSnapshot with
        {
            ProspectRights = nhl.ScenarioSnapshot.ProspectRights
                .Select(item => item.ProspectPersonId == nhlProspect.ProspectPersonId ? nhlProspect : item)
                .ToArray()
        };
        nhlScenario = new PlayerPipelineService().UpsertProspect(nhlScenario, nhlProspect, "Test prospect signed before affiliate assignment.");
        var assigned = new ProspectDecisionService().ApplyDecision(
            nhl.Registry,
            nhlScenario,
            new ProspectDecision(nhlProspect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, nhlScenario.CurrentDate));

        Assert.False(blocked.Success, "Junior-style rulebook without affiliate should not allow affiliate assignment.");
        Assert.True(assigned.Success, assigned.Message);
        Assert.Equal(ProspectStatus.AssignedToAffiliate, assigned.Prospect.Status);
    }

    public void AhlStyleTeamsDoNotUseAmateurDraftFlow()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario(rulebook: RulebookPresets.Create(DraftLeaguePreset.AhlStyle)));
        var result = new AlphaDraftExperienceService().StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.Equal(DraftExperienceStatus.Disabled, result.DraftState.Status);
        Assert.Equal(0, result.ScenarioSnapshot.ProspectRights.Count);
    }

    public void ProspectDecisionsCreateEventsAndInboxMessages()
    {
        var ready = ReadyWithDraftedProspects();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();
        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.InviteToCamp, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.ProspectInvitedToCamp), "Prospect invite event should be queued.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.ProspectInvitedToCamp), "Prospect invite inbox item should be created.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.OwnerDraftReaction), "Owner reaction should be created.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.ScoutRecommendationUpdated), "Head scout reaction should be created.");
    }

    public void AlphaDesktopExposesProspectActions()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Prospect List", StringComparison.Ordinal), "AlphaDesktop should expose prospect list.");
        Assert.True(text.Contains("Offer Contract", StringComparison.Ordinal), "AlphaDesktop should expose offer contract action.");
        Assert.True(text.Contains("Invite Prospect", StringComparison.Ordinal), "AlphaDesktop should expose camp invite action.");
        Assert.True(text.Contains("Return Prospect", StringComparison.Ordinal), "AlphaDesktop should expose return action.");
        Assert.True(text.Contains("Assign Prospect", StringComparison.Ordinal), "AlphaDesktop should expose affiliate assignment action.");
        Assert.True(text.Contains("Release Rights", StringComparison.Ordinal), "AlphaDesktop should expose release rights action.");
    }

    public void ProspectDecisionsHaveNoGodotSaveOrGameSimulationDependency()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "ProspectDecisionService.cs"));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Prospect decisions should not depend on Godot.");
        Assert.False(text.Contains("Save", StringComparison.Ordinal), "Prospect decisions should not implement save/load.");
        Assert.False(text.Contains("GameSimulation", StringComparison.Ordinal), "Prospect decisions should not implement game simulation.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyWithDraftedProspects(
        Rulebook? rulebook = null,
        string? affiliateOrganizationId = null)
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario(rulebook: rulebook));
        if (affiliateOrganizationId is not null)
        {
            var organization = scenario.ScenarioSnapshot.Organization with { AffiliateOrganizationId = affiliateOrganizationId };
            scenario = scenario with
            {
                ScenarioSnapshot = scenario.ScenarioSnapshot with
                {
                    Organization = organization,
                    AlphaSnapshot = scenario.ScenarioSnapshot.AlphaSnapshot with { Organization = organization }
                }
            };
        }

        var completed = new AlphaDraftExperienceService().SimulateToCompletion(scenario.Registry, scenario.ScenarioSnapshot);
        return (scenario.Registry, completed.ScenarioSnapshot);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyForPlayerPick()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var service = new AlphaDraftExperienceService();
        var started = service.StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);
        var ai = service.RunAiPicksUntilPlayerTurn(scenario.Registry, started.ScenarioSnapshot);
        return (scenario.Registry, ai.ScenarioSnapshot);
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
