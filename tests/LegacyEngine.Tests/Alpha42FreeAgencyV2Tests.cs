using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.World;

internal sealed class Alpha42FreeAgencyV2Tests
{
    public void FreeAgencyWindowUsesSeasonCalendar()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var window = new FreeAgencyV2Service().BuildWindow(scenario);
        var open = scenario.Season.Calendar.Milestones.Single(milestone => milestone.Type == LegacyEngine.Seasons.SeasonMilestoneType.FreeAgencyOpens);
        var close = scenario.Season.Calendar.Milestones.Single(milestone => milestone.Type == LegacyEngine.Seasons.SeasonMilestoneType.FreeAgencyEnds);

        Assert.Equal(open.Date.Value, window.OpensOn);
        Assert.Equal(close.Date.Value, window.ClosesOn);
    }

    public void FreeAgencyPhaseChangesOverTime()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new FreeAgencyV2Service();
        var window = service.BuildWindow(created.ScenarioSnapshot);

        Assert.Equal(FreeAgencyPhase.NotOpen, service.BuildWindow(WithDate(created.ScenarioSnapshot, window.OpensOn.AddDays(-1))).Phase);
        Assert.Equal(FreeAgencyPhase.OpeningDay, service.BuildWindow(WithDate(created.ScenarioSnapshot, window.OpensOn)).Phase);
        Assert.Equal(FreeAgencyPhase.ActiveMarket, service.BuildWindow(WithDate(created.ScenarioSnapshot, window.OpensOn.AddDays(8))).Phase);
        Assert.Equal(FreeAgencyPhase.LateMarket, service.BuildWindow(WithDate(created.ScenarioSnapshot, window.ClosesOn)).Phase);
        Assert.Equal(FreeAgencyPhase.Closed, service.BuildWindow(WithDate(created.ScenarioSnapshot, window.ClosesOn.AddDays(1))).Phase);
    }

    public void MotivationsAndCompetingOffersGenerated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = OpenMarket(created);
        var state = new FreeAgencyV2Service().EnsureMarketState(created.Registry, scenario).FreeAgencyMarketState!;

        Assert.True(state.MotivationProfiles.Count == scenario.FreeAgentMarket!.FreeAgents.Count, "Each free agent should get a motivation profile.");
        Assert.True(state.MotivationProfiles.All(profile => profile.TopMotivations.Count == 3), "Top three motivations should be exposed.");
        Assert.True(state.Competitions.Count > 0, "Some free agents should have competing offers.");
        Assert.True(state.Competitions.Any(competition => !string.IsNullOrWhiteSpace(competition.WhyPlayerMayChooseThem)), "Competing offers need reasons.");
    }

    public void MotivationsAffectOfferScore()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = OpenMarket(created);
        var service = new FreeAgencyV2Service();
        scenario = service.EnsureMarketState(created.Registry, scenario);
        var agent = scenario.FreeAgentMarket!.FreeAgents.OrderByDescending(agent => agent.Interest.PlayerOrganizationInterest).First();
        var low = service.BuildOffer(created.Registry, scenario, agent.PersonId, agent.ContractAsk.AnnualAmount * 0.5m, agent.ContractAsk.TermYears);
        var high = service.BuildOffer(created.Registry, scenario, agent.PersonId, agent.ContractAsk.AnnualAmount + 2_000m, agent.ContractAsk.TermYears);

        Assert.True(high.DecisionScore > low.DecisionScore, "A stronger money/term fit should improve the explainable decision score.");
    }

    public void DelayedResponseCreatesActionCenterItem()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new FreeAgencyV2Service();
        var scenario = service.EnsureMarketState(created.Registry, OpenMarket(created));
        var agent = scenario.FreeAgentMarket!.FreeAgents.First(agent => scenario.FreeAgencyMarketState!.ActiveCompetitions(agent.PersonId).Count > 0);
        var offered = service.SubmitOffer(created.Registry, scenario, agent.PersonId, agent.ContractAsk.AnnualAmount, agent.ContractAsk.TermYears).ScenarioSnapshot;
        var readiness = new SeasonReadinessService().Evaluate(created.Registry, offered);
        var items = new ActionCenterService().BuildItems(
            offered,
            Array.Empty<InboxMessage>(),
            new BudgetOverviewService().Build(offered, RulebookPresets.CreateJuniorMajor()),
            readiness,
            Array.Empty<StaffVacancy>());

        Assert.True(offered.FreeAgencyMarketState!.FindOffer(agent.PersonId)!.IsPendingResponse, "Offer should wait for a response.");
        Assert.True(items.Any(item => item.Title.Contains(agent.Name, StringComparison.Ordinal) && item.Category == ActionCenterCategory.Contracts), "Action Center should show pending response due date.");
    }

    public void CompetingOfferCanWinPlayerAndCreateLeagueNews()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new FreeAgencyV2Service();
        var scenario = service.EnsureMarketState(created.Registry, OpenMarket(created));
        var agent = scenario.FreeAgentMarket!.FreeAgents.First(agent => scenario.FreeAgencyMarketState!.ActiveCompetitions(agent.PersonId).Count > 0);
        var offered = service.SubmitOffer(created.Registry, scenario, agent.PersonId, agent.ContractAsk.AnnualAmount, agent.ContractAsk.TermYears).ScenarioSnapshot;
        var offer = offered.FreeAgencyMarketState!.FindOffer(agent.PersonId)!;
        var boosted = offered.FreeAgencyMarketState.Competitions
            .Select(competition => competition.PersonId == agent.PersonId ? competition with { PlayerInterest = 100 } : competition)
            .ToArray();
        offered = WithDate(offered with { FreeAgencyMarketState = offered.FreeAgencyMarketState with { Competitions = boosted } }, offer.ResponseDate);
        var resolved = service.ProgressMarket(created.Registry, offered);

        Assert.Equal(FreeAgentStatus.Unavailable, resolved.ScenarioSnapshot.FreeAgentMarket!.Find(agent.PersonId)!.Status);
        Assert.True(resolved.LeagueTransactions.Any(transaction => transaction.TransactionType == LeagueTransactionType.PlayerSigned && transaction.PersonName == agent.Name), "Player signing elsewhere should go to League News.");
        Assert.True(resolved.InboxItems.Any(item => item.Summary.Contains(agent.Name, StringComparison.Ordinal)), "Offered player signing elsewhere should alert the GM.");
    }

    public void AcceptedFreeAgentOfferCreatesPendingApprovalAndNoContract()
    {
        var prepared = AcceptedOfferResult();

        Assert.True(prepared.Result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.ApproveContract && action.PersonId == prepared.Agent.PersonId && action.IsOpen), "Accepted offer should create pending approval.");
        Assert.False(prepared.Result.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.Agent.PersonId), "Accepted offer should not auto-sign.");
    }

    public void ApprovingAcceptedFreeAgentOfferCreatesContract()
    {
        var prepared = AcceptedOfferResult();
        var action = prepared.Result.ScenarioSnapshot.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveContract && action.PersonId == prepared.Agent.PersonId && action.IsOpen);
        var approved = new PendingGmActionService().Approve(prepared.Created.Registry, prepared.Result.ScenarioSnapshot, action.ActionId);

        Assert.True(approved.Success, approved.Message);
        Assert.True(approved.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.Agent.PersonId), "GM approval should create the signed contract.");
    }

    public void LateMarketReducesAskAndImprovesInterest()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new FreeAgencyV2Service();
        var window = service.BuildWindow(created.ScenarioSnapshot);
        var openScenario = service.EnsureMarketState(created.Registry, WithDate(created.ScenarioSnapshot, window.OpensOn));
        var agent = openScenario.FreeAgentMarket!.FreeAgents.First(agent => agent.Status == FreeAgentStatus.Available);
        var lateScenario = WithDate(openScenario, window.ClosesOn);
        var progressed = service.ProgressMarket(created.Registry, lateScenario).ScenarioSnapshot;
        var updated = progressed.FreeAgentMarket!.Find(agent.PersonId)!;

        Assert.True(updated.ContractAsk.AnnualAmount < agent.ContractAsk.AnnualAmount, "Late market should lower some asks.");
        Assert.True(updated.Interest.PlayerOrganizationInterest >= agent.Interest.PlayerOrganizationInterest, "Late market should not reduce player interest.");
    }

    public void StaffRecommendationsGenerated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = OpenMarket(created);
        var agent = scenario.FreeAgentMarket!.FreeAgents.First();
        var recommendations = new FreeAgencyV2Service().BuildStaffRecommendations(scenario, agent.PersonId);

        Assert.True(recommendations.HeadCoach.Contains("Head coach", StringComparison.Ordinal), "Head coach recommendation should be shown.");
        Assert.True(recommendations.Scout.Contains("Scout", StringComparison.Ordinal), "Scout recommendation should be shown.");
        Assert.True(recommendations.Owner.Contains("Owner", StringComparison.Ordinal), "Owner budget view should be shown.");
    }

    public void SaveLoadPreservesMarketState()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = new FreeAgencyV2Service().EnsureMarketState(created.Registry, OpenMarket(created));
        var inbox = new InboxManager();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha42-{Guid.NewGuid():N}.json");
        var service = new SaveGameService();
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());
        var saved = service.SaveCareer(scenario, inbox.Query(new InboxFilter()), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = service.LoadFromFile(path, RulebookPresets.CreateJuniorMajor());

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.FreeAgencyMarketState is not null, "Free agency market state should survive save/load.");
        Assert.Equal(scenario.FreeAgencyMarketState!.MotivationProfiles.Count, loaded.SaveGame.ScenarioSnapshot.FreeAgencyMarketState!.MotivationProfiles.Count);
    }

    public void AlphaDesktopExposesFreeAgencyV2Ui()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Market phase", StringComparison.Ordinal), "Desktop should show free agency market phase.");
        Assert.True(source.Contains("Top motivations", StringComparison.Ordinal), "Desktop should show player motivations.");
        Assert.True(source.Contains("Competing offers", StringComparison.Ordinal), "Desktop should show competing offers.");
        Assert.True(source.Contains("Pending response", StringComparison.Ordinal), "Desktop should show pending response timing.");
        Assert.True(source.Contains("Offer likelihood", StringComparison.Ordinal), "Desktop should show offer likelihood.");
    }

    public void Alpha42HasNoGodotConversationTreeOrGameSimulation()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "FreeAgency*.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "client", "AlphaDesktop"), "*.cs", SearchOption.TopDirectoryOnly))
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 4.2 should not add Godot.");
        Assert.False(text.Contains("ConversationTree", StringComparison.Ordinal), "Free agency should not add full conversation trees.");
        Assert.False(text.Contains("ArbitrationEngine", StringComparison.Ordinal), "Free agency should not add arbitration.");
        Assert.False(text.Contains("OfferSheetEngine", StringComparison.Ordinal), "Free agency should not add offer sheets.");
        Assert.False(text.Contains("PlayByPlay", StringComparison.Ordinal), "Alpha 4.2 should not add game simulation expansion.");
    }

    private static (NewGmScenarioResult Created, FreeAgent Agent, FreeAgencyV2Result Result) AcceptedOfferResult()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new FreeAgencyV2Service();
        var scenario = service.EnsureMarketState(created.Registry, OpenMarket(created));
        var agent = scenario.FreeAgentMarket!.FreeAgents
            .Where(agent => scenario.FreeAgencyMarketState!.ActiveCompetitions(agent.PersonId).Count == 0)
            .OrderByDescending(agent => agent.Interest.PlayerOrganizationInterest)
            .First();
        agent = agent with
        {
            ContractAsk = agent.ContractAsk with { AnnualAmount = 1_000m, TermYears = 1, Notes = "Affordable ask for deterministic acceptance test." },
            Interest = agent.Interest with { PlayerOrganizationInterest = 100, CompetingInterest = "No active competing offers.", MotivationSummary = "Strong interest in role, stability, and relationship fit." },
            FitSummary = agent.FitSummary with { FitScore = 100, RosterNeed = "Clear roster fit.", BudgetImpact = "Affordable.", StaffRecommendation = "Strong fit.", RiskSummary = "Low risk." },
            ProjectedLineupRole = "Regular roster role",
            DevelopmentTrend = "Clear development plan"
        };
        scenario = scenario with
        {
            FreeAgentMarket = scenario.FreeAgentMarket.Replace(agent),
            FreeAgencyMarketState = scenario.FreeAgencyMarketState! with
            {
                Competitions = scenario.FreeAgencyMarketState.Competitions.Where(competition => competition.PersonId != agent.PersonId).ToArray()
            }
        };
        var result = service.SubmitOffer(created.Registry, scenario, agent.PersonId, 5_000m, 2);
        if (result.OfferState?.IsPendingResponse == true)
        {
            result = service.ProgressMarket(created.Registry, WithDate(result.ScenarioSnapshot, result.OfferState.ResponseDate));
        }

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.ApproveContract && action.PersonId == agent.PersonId), "Test setup should create an accepted pending offer.");
        return (created, agent, result);
    }

    private static NewGmScenarioSnapshot OpenMarket(NewGmScenarioResult created)
    {
        var service = new FreeAgencyV2Service();
        var window = service.BuildWindow(created.ScenarioSnapshot);
        return WithDate(created.ScenarioSnapshot, window.OpensOn);
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
