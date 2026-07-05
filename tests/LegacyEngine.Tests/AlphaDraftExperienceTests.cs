using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class AlphaDraftExperienceTests
{
    public void RulebookDraftLengthComesFromJuniorRulebook()
    {
        var rulebook = new RulebookLoader().LoadFromFile(Path.Combine(FindRepositoryRoot(), "data", "rulebooks", "junior_v1.json"));

        Assert.Equal(15, rulebook.DraftRules!.Rounds);
    }

    public void JuniorPresetHasFifteenRounds()
    {
        var rulebook = RulebookPresets.Create(DraftLeaguePreset.JuniorMajor);

        Assert.Equal(15, rulebook.DraftRules!.Rounds);
        Assert.True(rulebook.DraftRules.DraftEnabled, "Junior Major draft should be enabled.");
    }

    public void NhlPresetHasSevenRounds()
    {
        var rulebook = RulebookPresets.Create(DraftLeaguePreset.NhlStyle);

        Assert.Equal(7, rulebook.DraftRules!.Rounds);
        Assert.True(rulebook.DraftRules.DraftEnabled, "NHL-style draft should be enabled.");
    }

    public void AhlPresetDisablesDraft()
    {
        var rulebook = RulebookPresets.Create(DraftLeaguePreset.AhlStyle);

        Assert.False(rulebook.DraftRules!.DraftEnabled, "AHL-style draft should be disabled.");
        Assert.Equal(0, rulebook.DraftRules.Rounds);
    }

    public void DraftBoardReorderingWorks()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var second = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).Skip(1).First();
        var result = new NewGmScenarioActions().MoveDraftBoardPlayer(scenario.Registry, scenario.ScenarioSnapshot, second.ProspectPersonId, -1);

        Assert.Equal(1, result.AlphaSnapshot.DraftBoard.Entries.Single(entry => entry.ProspectPersonId == second.ProspectPersonId).Rank);
    }

    public void DraftNotesAndStarsWork()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new AlphaDraftExperienceService();
        var prospect = scenario.AlphaSnapshot.DraftBoard.Entries.First();

        var starred = service.StarProspect(scenario.Registry, scenario.ScenarioSnapshot, prospect.ProspectPersonId, true);
        var noted = service.UpdatePersonalNotes(scenario.Registry, starred.ScenarioSnapshot, prospect.ProspectPersonId, "Watch second effort.");
        var entry = noted.AlphaSnapshot.DraftBoard.Entries.Single(item => item.ProspectPersonId == prospect.ProspectPersonId);

        Assert.True(entry.IsStarred, "Prospect should be starred.");
        Assert.Equal("Watch second effort.", entry.PersonalNotes);
    }

    public void AiDraftingRunsUntilPlayerPick()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var service = new AlphaDraftExperienceService();

        var started = service.StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);
        var result = service.RunAiPicksUntilPlayerTurn(scenario.Registry, started.ScenarioSnapshot);

        Assert.Equal(DraftExperienceStatus.AwaitingPlayerPick, result.DraftState.Status);
        Assert.True(result.DraftState.IsPlayerTurn, "AI drafting should stop on the player's pick.");
        Assert.Equal(3, result.DraftState.Selections.Count);
    }

    public void PlayerDraftingRecordsSelection()
    {
        var ready = ReadyForPlayerPick();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var result = new AlphaDraftExperienceService().MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.True(result.DraftState.Selections.Any(selection => selection.IsPlayerSelection), "Player selection should be recorded.");
        Assert.False(result.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == prospect.ProspectPersonId), "Drafted player should leave the board.");
    }

    public void DuplicateDraftSelectionIsPrevented()
    {
        var ready = ReadyForPlayerPick();
        var service = new AlphaDraftExperienceService();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var picked = service.MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);
        var nextPick = picked.DraftState.CurrentPick
            ?? throw new InvalidOperationException("Expected another pick after the player's first selection.");

        Assert.Throws<InvalidOperationException>(() => ready.Registry.DraftEngine.SelectProspect(
            picked.DraftState.Draft!,
            nextPick.RoundNumber,
            nextPick.PickNumber,
            prospect.ProspectPersonId,
            new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero),
            ruleValidator: new DraftRuleValidator(ready.Registry.Rulebook!)));
    }

    public void DraftRecapIsGenerated()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var result = new AlphaDraftExperienceService().SimulateToCompletion(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.Equal(DraftExperienceStatus.Completed, result.DraftState.Status);
        Assert.True(result.DraftState.Recap is not null, "Draft recap should be generated.");
        Assert.Equal(15, result.DraftState.Recap!.RoundsCompleted);
        Assert.Equal(15, result.DraftState.Recap.YourSelections.Count);
    }

    public void DraftEventsAreGenerated()
    {
        var ready = ReadyForPlayerPick();
        var service = new AlphaDraftExperienceService();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        service.MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.True(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.DraftStarted), "Draft start event should exist.");
        Assert.True(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.PlayerDrafted), "Player drafted event should exist.");
        Assert.True(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.OwnerDraftReaction), "Owner draft reaction event should exist.");
    }

    public void DesktopIntegrationExposesDraftActions()
    {
        var programPath = Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs");
        var text = File.ReadAllText(programPath);

        Assert.True(text.Contains("Start Draft", StringComparison.Ordinal), "AlphaDesktop should expose Start Draft.");
        Assert.True(text.Contains("AI Picks", StringComparison.Ordinal), "AlphaDesktop should expose AI picks.");
        Assert.True(text.Contains("Draft Top", StringComparison.Ordinal), "AlphaDesktop should expose player drafting.");
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
