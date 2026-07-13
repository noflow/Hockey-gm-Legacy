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
        var expectedAiPicks = started.DraftState.Draft!.Picks
            .TakeWhile(pick => pick.OwningOrganizationId != started.DraftState.PlayerOrganizationId)
            .Count();

        Assert.Equal(DraftExperienceStatus.AwaitingPlayerPick, result.DraftState.Status);
        Assert.True(result.DraftState.IsPlayerTurn, "AI drafting should stop on the player's pick.");
        Assert.Equal(expectedAiPicks, result.DraftState.Selections.Count);
    }

    public void StartDraftBeginsDraft()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var result = new AlphaDraftExperienceService().StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.Equal(DraftExperienceStatus.InProgress, result.DraftState.Status);
        Assert.True(result.DraftState.Draft is not null, "Starting draft day should create the active draft.");
    }

    public void DraftIncludesEveryLeagueTeam()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var result = new AlphaDraftExperienceService().StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);
        var teamCount = scenario.ScenarioSnapshot.LeagueProfile.Teams.Count;

        Assert.Equal(teamCount, result.DraftState.OrganizationNames.Count);
        Assert.Equal(teamCount * result.DraftState.TotalRounds, result.DraftState.Draft!.Picks.Count);
    }

    public void DraftOrderRunsFromWorstPreviousRecordToChampion()
    {
        var career = new MultiLeagueCareerService();
        var team = career.TeamsFor(LeagueExperience.Nhl).First();
        var selected = career.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId);
        var scenario = AdvanceToDraftDay(career.CreateScenario(selected));
        var result = new AlphaDraftExperienceService().StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);
        var order = result.DraftState.Draft!.DraftOrder.OrganizationIds;
        var teams = scenario.ScenarioSnapshot.LeagueProfile.Teams.ToDictionary(item => item.OrganizationId, StringComparer.Ordinal);
        var champion = teams.Single(item => string.Equals(item.Value.TeamName, scenario.ScenarioSnapshot.LeagueProfile.Identity.CurrentChampion, StringComparison.OrdinalIgnoreCase)).Key;
        var actualStandings = scenario.ScenarioSnapshot.Standings?.Teams
            .Where(item => item.GamesPlayed > 0)
            .ToArray();
        var worst = actualStandings is { Length: > 0 } && actualStandings.Length == teams.Count
            ? actualStandings
                .OrderBy(item => item.Points)
                .ThenBy(item => item.Wins)
                .ThenBy(item => item.GoalsFor - item.GoalsAgainst)
                .ThenBy(item => item.TeamName, StringComparer.Ordinal)
                .Last()
                .OrganizationId
            : teams
                .Where(item => item.Key != champion)
                .OrderByDescending(item => PreviousRecordPoints(item.Value.PreviousRecord))
                .ThenByDescending(item => PreviousRecordWins(item.Value.PreviousRecord))
                .ThenBy(item => item.Value.TeamName, StringComparer.Ordinal)
                .Last()
                .Key;

        Assert.True(worst == order[0], $"The worst prior-season team should receive the first overall pick. Expected {worst}, got {order[0]}.");
        Assert.True(champion == order[^1], "The reigning champion should receive the last pick in each round.");
        Assert.True(result.DraftState.Draft!.Picks[0].OwningOrganizationId == order[0], "Round one pick ownership should match draft order.");
        Assert.True(result.DraftState.Draft!.Picks[order.Count].OwningOrganizationId == order[0], "Round two should repeat the same draft order.");
    }

    public void DraftBoardHasEnoughProspectsForFullLeagueDraft()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var result = new AlphaDraftExperienceService().StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.True(
            scenario.AlphaSnapshot.DraftBoard.Entries.Count >= result.DraftState.Draft!.Picks.Count,
            "Draft board should have enough prospects for every team and every round.");
    }

    public void LiveDraftStartAdvancesAiPicksAutomatically()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var result = new AlphaDraftExperienceService().StartLiveDraft(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.Equal(DraftExperienceStatus.AwaitingPlayerPick, result.DraftState.Status);
        Assert.True(result.DraftState.IsPlayerTurn, "Live draft should pause on the player's pick.");
        Assert.True(result.DraftState.Selections.Count > 0, "AI picks should be made automatically before the player's pick.");
    }

    public void PlayerDraftingRecordsSelection()
    {
        var ready = ReadyForPlayerPick();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var result = new AlphaDraftExperienceService().MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.True(result.DraftState.Selections.Any(selection => selection.IsPlayerSelection), "Player selection should be recorded.");
        Assert.False(result.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == prospect.ProspectPersonId), "Drafted player should leave the board.");
    }

    public void LiveDraftPlayerSelectionContinuesToNextPlayerPick()
    {
        var ready = ReadyForPlayerPick();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var result = new AlphaDraftExperienceService().MakePlayerSelectionAndContinue(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.Equal(DraftExperienceStatus.AwaitingPlayerPick, result.DraftState.Status);
        Assert.True(result.DraftState.IsPlayerTurn, "Live draft should pause again at the player's next pick.");
        Assert.False(result.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == prospect.ProspectPersonId), "Drafted player should leave available list.");
    }

    public void PlayerDraftPickAddsDraftRightsNotActiveRoster()
    {
        var ready = ReadyForPlayerPick();
        var originalRosterCount = ready.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count;
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var result = new AlphaDraftExperienceService().MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.True(result.ScenarioSnapshot.DraftRights.Any(selection => selection.ProspectPersonId == prospect.ProspectPersonId), "Player selection should be added to draft rights/prospect list.");
        Assert.Equal(originalRosterCount, result.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count);
        Assert.False(result.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == prospect.ProspectPersonId), "Drafted prospect should not be auto-added to active roster.");
        Assert.False(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.SignDraftPick && action.PersonId == prospect.ProspectPersonId), "Drafted prospect should not create a mandatory pending signing decision.");
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

    public void DraftRecapInboxMessageCreated()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var result = new AlphaDraftExperienceService().SimulateToCompletion(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.DraftRecapCreated), "Draft completion should create a recap inbox message.");
    }

    public void DraftOnlyCreatesFinalReviewInbox()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var result = new AlphaDraftExperienceService().SimulateToCompletion(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.Equal(1, result.InboxItems.Count);
        Assert.True(result.InboxItems.All(item => item.EventType == LegacyEngine.Events.LegacyEventType.DraftRecapCreated), "Draft should only create the final recap inbox message.");
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
        Assert.False(ready.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.PendingGmActionCreated), "Drafting alone should not create a mandatory pending GM signing action.");
    }

    public void DraftCompletionCreatesRecapEvent()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        new AlphaDraftExperienceService().SimulateToCompletion(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.True(scenario.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.DraftCompleted), "Draft completed event should exist.");
        Assert.True(scenario.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.DraftRecapCreated), "Draft recap event should exist.");
    }

    public void DesktopIntegrationExposesDraftActions()
    {
        var programPath = Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs");
        var text = File.ReadAllText(programPath);

        Assert.True(text.Contains("Start Draft", StringComparison.Ordinal), "AlphaDesktop should expose Start Draft.");
        Assert.True(text.Contains("Draft Player", StringComparison.Ordinal), "AlphaDesktop should expose player drafting.");
        Assert.True(text.Contains("Select a prospect, then click Draft Player.", StringComparison.Ordinal), "Draft Player action should be visible and explained in the modal.");
        Assert.True(text.Contains("End Draft", StringComparison.Ordinal), "AlphaDesktop should expose End Draft.");
    }

    public void DesktopIntegrationExposesLiveDraftModal()
    {
        var programPath = Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs");
        var text = File.ReadAllText(programPath);

        Assert.True(text.Contains("Draft Day", StringComparison.Ordinal), "AlphaDesktop should show a Draft Day modal.");
        Assert.True(text.Contains("BuildDraftModalOverlay", StringComparison.Ordinal), "AlphaDesktop should build a modal overlay.");
        Assert.True(text.Contains("IsDraftModalVisible", StringComparison.Ordinal), "AlphaDesktop should show the draft modal on draft day.");
        Assert.True(text.Contains("_draftModalDismissed = true", StringComparison.Ordinal), "End Draft should return to the dashboard by dismissing the modal.");
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

    private static int PreviousRecordPoints(string record)
    {
        var parts = record.Split('-', StringSplitOptions.TrimEntries);
        var wins = parts.Length > 0 && int.TryParse(parts[0], out var parsedWins) ? parsedWins : 0;
        var overtime = parts.Length > 2 && int.TryParse(parts[2], out var parsedOvertime) ? parsedOvertime : 0;
        return wins * 2 + overtime;
    }

    private static int PreviousRecordWins(string record)
    {
        var parts = record.Split('-', StringSplitOptions.TrimEntries);
        return parts.Length > 0 && int.TryParse(parts[0], out var wins) ? wins : 0;
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
