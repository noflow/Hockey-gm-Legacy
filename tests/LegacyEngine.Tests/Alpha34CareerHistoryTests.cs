using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.World;

internal sealed class Alpha34CareerHistoryTests
{
    public void CareerTimelineEntryCreated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.GMHired), "Scenario should seed the GM hire into career history.");
        Assert.True(scenario.CareerTimeline.Entries.Any(entry => entry.Title.Contains("GM hired", StringComparison.OrdinalIgnoreCase)), "Timeline entry should be readable.");
    }

    public void PlayerDraftedCreatesDraftHistory()
    {
        var completed = CompleteDraft();

        Assert.True(completed.ScenarioSnapshot.DraftPickHistory.Count > 0, "Completed draft should create draft pick history.");
        Assert.True(completed.ScenarioSnapshot.DraftClassHistory.Any(item => item.Year == completed.ScenarioSnapshot.Season.Year), "Completed draft should create a draft class record.");
    }

    public void DraftedPlayerAppearsInWhereAreTheyNow()
    {
        var completed = CompleteDraft();
        var whereAreTheyNow = new CareerHistoryService().BuildWhereAreTheyNow(completed.ScenarioSnapshot);
        var firstPick = completed.ScenarioSnapshot.DraftPickHistory[0];

        Assert.True(whereAreTheyNow.Any(item => item.PersonId == firstPick.PlayerPersonId), "Drafted player should appear in Where Are They Now.");
        Assert.True(whereAreTheyNow.All(item => !string.IsNullOrWhiteSpace(item.StaffOpinion)), "Where Are They Now should include staff context.");
    }

    public void DraftOutcomeStartsUnknownOrDeveloping()
    {
        var completed = CompleteDraft();

        Assert.True(completed.ScenarioSnapshot.DraftPickHistory.All(item => item.Outcome is DraftPickOutcome.Unknown or DraftPickOutcome.Developing), "New draft outcomes should stay Unknown/Developing.");
    }

    public void PlayerDossierIncludesTimeline()
    {
        var completed = CompleteDraft();
        var pick = completed.ScenarioSnapshot.DraftPickHistory[0];
        var dossier = new PlayerDossierService().CreateDossier(completed.ScenarioSnapshot, pick.PlayerPersonId);
        var text = string.Join(" ", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Career timeline", StringComparison.OrdinalIgnoreCase) || text.Contains("Draft history", StringComparison.OrdinalIgnoreCase), "Dossier should include career timeline or draft history.");
        Assert.True(text.Contains(pick.PlayerName, StringComparison.Ordinal), "Dossier history should name the player.");
    }

    public void StaffHistoryIsRecorded()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffCareerHistory.Count >= scenario.StaffMembers.Count, "Staff history should be recorded for current staff.");
        Assert.True(scenario.StaffCareerHistory.All(item => item.PreviousRoles.Count > 0 && !string.IsNullOrWhiteSpace(item.EvaluationSummary)), "Staff history should include readable summaries.");
    }

    public void GmCareerHistoryIsRecorded()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.GmCareerHistory is not null, "GM career history should be present.");
        Assert.Equal(scenario.GeneralManagerProfile.Person.PersonId, scenario.GmCareerHistory!.GmPersonId);
        Assert.True(scenario.GmCareerHistory.CareerNotes.Count > 0, "GM career history should include notes.");
    }

    public void OrganizationSeasonHistoryIsRecorded()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OrganizationSeasonHistory.Count > 0, "Organization season history should be seeded.");
        Assert.True(scenario.OrganizationSeasonHistory.Any(item => item.OrganizationId == scenario.Organization.OrganizationId), "Organization history should be tied to the player organization.");
    }

    public void TradeCompletedCreatesHistoryEntry()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.ScenarioSnapshot.TransactionHistory.Any(item => item.TransactionType == "TradeCompleted"), "Completed trade should create transaction history.");
        Assert.True(approved.ScenarioSnapshot.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.Traded), "Completed trade should create timeline history.");
    }

    public void FreeAgentSigningCreatesHistoryEntry()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First(item => item.Interest.PlayerOrganizationInterest >= 35);
        var offered = new FreeAgentMarketService().OfferContract(created.Registry, created.ScenarioSnapshot, agent.PersonId).ScenarioSnapshot;
        var action = offered.PendingActions.Single(item => item.PersonId == agent.PersonId && item.ActionType == PendingGmActionType.SignFreeAgent && item.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, offered, action.ActionId);

        Assert.True(approved.ScenarioSnapshot.TransactionHistory.Any(item => item.TransactionType == "FreeAgentSigned" && item.PersonId == agent.PersonId), "Free-agent signing should create transaction history.");
        Assert.True(approved.ScenarioSnapshot.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.Signed && entry.PersonId == agent.PersonId), "Free-agent signing should create timeline history.");
        Assert.True(approved.ScenarioSnapshot.GmCareerHistory!.FreeAgentsSigned > 0, "GM career history should count free-agent signings.");
    }

    public void InjuryCreatesHistoryEntry()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var injured = scenario.AlphaSnapshot.Roster.ActivePlayers[0];
        var updated = new CareerHistoryService().RecordInjury(scenario, injured.PersonId, $"{injured.Position} injury note for history.");

        Assert.True(updated.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.Injury && entry.PersonId == injured.PersonId), "Injury should create a career timeline entry.");
    }

    public void ReportsHistoryWorkspaceExposesHistoryViews()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("GM Career", StringComparison.Ordinal), "Reports workspace should expose GM Career.");
        Assert.True(source.Contains("Organization History", StringComparison.Ordinal), "Reports workspace should expose Organization History.");
        Assert.True(source.Contains("Draft History", StringComparison.Ordinal), "Reports workspace should expose Draft History.");
        Assert.True(source.Contains("Where Are They Now", StringComparison.Ordinal), "Reports workspace should expose Where Are They Now.");
        Assert.True(source.Contains("Transaction History", StringComparison.Ordinal), "Reports workspace should expose Transaction History.");
    }

    public void HiddenRatingsAreNotExposed()
    {
        var completed = CompleteDraft();
        var pick = completed.ScenarioSnapshot.DraftPickHistory[0];
        var dossier = new PlayerDossierService().CreateDossier(completed.ScenarioSnapshot, pick.PlayerPersonId);
        var whereAreTheyNow = new CareerHistoryService().BuildWhereAreTheyNow(completed.ScenarioSnapshot);
        var text = string.Join(" ", dossier.Sections.SelectMany(section => section.Lines))
            + " "
            + string.Join(" ", whereAreTheyNow.Select(item => $"{item.LatestStats} {item.DevelopmentTrend} {item.StaffOpinion}"));

        Assert.False(text.Contains("CurrentAbility", StringComparison.Ordinal), "History output must not expose hidden current ability.");
        Assert.False(text.Contains("Potential =", StringComparison.Ordinal), "History output must not expose hidden potential.");
        Assert.False(text.Contains("Hidden", StringComparison.Ordinal), "History output should not label hidden ratings.");
    }

    public void Alpha34HasNoGodotSaveOrDeepDatabase()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*History*.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Career*.cs", SearchOption.TopDirectoryOnly))
            .Concat(new[] { Path.Combine(root, "engine", "LegacyEngine", "Integration", "WhereAreTheyNowRecord.cs") })
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 3.4 should not add Godot.");
        Assert.False(text.Contains("SaveGame", StringComparison.Ordinal), "Alpha 3.4 should not add save/load.");
        Assert.False(text.Contains("DbContext", StringComparison.Ordinal), "Alpha 3.4 should not add a deep historical database.");
    }

    private static DraftExperienceResult CompleteDraft()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        return new AlphaDraftExperienceService().SimulateToCompletion(scenario.Registry, scenario.ScenarioSnapshot);
    }

    private static TradeOffer BuildAcceptedOffer(NewGmScenarioSnapshot scenario)
    {
        var service = new TradeService();
        var target = scenario.TradeBlock!.Entries.OrderBy(entry => entry.AssetValue).First();
        var outgoing = scenario.AlphaSnapshot.Roster.ActivePlayers
            .OrderByDescending(player => player.Position == target.Position)
            .ThenByDescending(player => scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == player.PersonId)?.Points ?? 0)
            .First();
        return service.CreateOffer(
            scenario,
            target.OrganizationId,
            target.TeamName,
            new[] { service.CreateRosterPlayerAsset(scenario, outgoing.PersonId) },
            new[] { service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization) });
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
