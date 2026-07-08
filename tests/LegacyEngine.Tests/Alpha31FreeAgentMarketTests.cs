using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha31FreeAgentMarketTests
{
    public void FreeAgentMarketGeneratedAtScenarioStart()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.FreeAgentMarket is not null, "New GM scenario should include a free-agent market.");
        Assert.True(scenario.FreeAgentMarket!.FreeAgents.Count >= 20, "Free-agent market should include useful depth options.");
        Assert.True(scenario.FreeAgentMarket.FreeAgents.Any(agent => agent.Position == RosterPosition.Goalie), "Free-agent market should include goalies.");
        Assert.True(scenario.FreeAgentMarket.FreeAgents.Any(agent => agent.FitSummary.FitScore >= 70), "Free-agent market should include useful higher-interest fits.");
        Assert.True(scenario.FreeAgentMarket.FreeAgents.Any(agent => agent.FitSummary.StaffRecommendation.Contains("Poor fit", StringComparison.OrdinalIgnoreCase)), "Free-agent market should include poor-fit options.");
    }

    public void FreeAgentsHaveCleanNamesAndKnownBio()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var agents = scenario.FreeAgentMarket!.FreeAgents;

        Assert.True(agents.All(agent => !agent.Name.Any(char.IsDigit)), "Free-agent display names should not contain numeric suffixes.");
        Assert.True(agents.All(agent => agent.Position != RosterPosition.Unknown), "Free agents should have known public positions.");
        Assert.True(agents.All(agent => !string.IsNullOrWhiteSpace(agent.PreviousTeam)), "Free agents should show prior team context.");
        Assert.True(agents.All(agent => agent.HeightInches > 0 && agent.WeightPounds > 0), "Free agents should show basic physical bio.");
    }

    public void FreeAgentsHavePriorStatsAndCareerHistory()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var agent = scenario.FreeAgentMarket!.FreeAgents[0];

        Assert.True(scenario.PriorSeasonStats.Any(stat => stat.PersonId == agent.PersonId), "Free agents should have prior stat lines.");
        Assert.True(scenario.CareerStatSummaries.Any(summary => summary.PersonId == agent.PersonId), "Free agents should have career summaries.");
        Assert.True(agent.LastSeasonStats.SummaryText.Contains(agent.Name, StringComparison.Ordinal), "Free agent last-season stats should name the player.");
    }

    public void ContractAskAndBudgetImpactExist()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var agent = scenario.FreeAgentMarket!.FreeAgents[0];
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());

        Assert.True(agent.ContractAsk.AnnualAmount > 0, "Free agents should have a contract ask.");
        Assert.True(!string.IsNullOrWhiteSpace(agent.ContractAsk.Notes), "Contract ask should include context.");
        Assert.True(budget.RemainingBudget - agent.ContractAsk.AnnualAmount != budget.RemainingBudget, "Contract ask should affect budget calculations.");
    }

    public void ShortlistWorks()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First(item => !item.IsShortlisted);
        var result = new FreeAgentMarketService().Shortlist(created.Registry, created.ScenarioSnapshot, agent.PersonId);

        Assert.True(result.Success, "Shortlist action should succeed.");
        Assert.True(result.ScenarioSnapshot.FreeAgentMarket!.Find(agent.PersonId)!.IsShortlisted, "Free agent should be shortlisted.");
        Assert.True(result.InboxItems.Any(item => item.Summary.Contains(agent.Name, StringComparison.Ordinal)), "Shortlist inbox should name the free agent.");
    }

    public void OfferCreatesPendingGmActionAndDoesNotAutoSign()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First(item => item.Interest.PlayerOrganizationInterest >= 35);
        var beforeContracts = created.ScenarioSnapshot.Contracts.Count;
        var result = new FreeAgentMarketService().OfferContract(created.Registry, created.ScenarioSnapshot, agent.PersonId);

        Assert.True(result.Success, "Free-agent offer should succeed.");
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.PersonId == agent.PersonId && action.ActionType == PendingGmActionType.SignFreeAgent && action.IsOpen), "Offer should create a pending GM signing action.");
        Assert.Equal(beforeContracts, result.ScenarioSnapshot.Contracts.Count);
        Assert.False(result.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == agent.PersonId), "Offer should not auto-sign a contract.");
    }

    public void ApprovingFreeAgentSigningCreatesContract()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First(item => item.Interest.PlayerOrganizationInterest >= 35);
        var offered = new FreeAgentMarketService().OfferContract(created.Registry, created.ScenarioSnapshot, agent.PersonId).ScenarioSnapshot;
        var action = offered.PendingActions.Single(item => item.PersonId == agent.PersonId && item.ActionType == PendingGmActionType.SignFreeAgent && item.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, offered, action.ActionId);

        Assert.True(approved.Success, "Approving free-agent pending action should succeed.");
        Assert.True(approved.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == agent.PersonId), "Approval should create the contract.");
        Assert.Equal(FreeAgentStatus.Signed, approved.ScenarioSnapshot.FreeAgentMarket!.Find(agent.PersonId)!.Status);
    }

    public void DecliningFreeAgentSigningDoesNotCreateContract()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First(item => item.Interest.PlayerOrganizationInterest >= 35);
        var offered = new FreeAgentMarketService().OfferContract(created.Registry, created.ScenarioSnapshot, agent.PersonId).ScenarioSnapshot;
        var action = offered.PendingActions.Single(item => item.PersonId == agent.PersonId && item.ActionType == PendingGmActionType.SignFreeAgent && item.IsOpen);
        var declined = new PendingGmActionService().Decline(created.Registry, offered, action.ActionId);

        Assert.True(declined.Success, "Declining free-agent pending action should succeed.");
        Assert.False(declined.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == agent.PersonId), "Decline should not create a contract.");
        Assert.Equal(FreeAgentStatus.Rejected, declined.ScenarioSnapshot.FreeAgentMarket!.Find(agent.PersonId)!.Status);
    }

    public void InviteToCampCreatesCampWorkflow()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents[0];
        var result = new FreeAgentMarketService().InviteToCamp(created.Registry, created.ScenarioSnapshot, agent.PersonId);

        Assert.True(result.Success, "Free-agent camp invite should succeed.");
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.PersonId == agent.PersonId && action.ActionType == PendingGmActionType.InviteToCamp), "Camp invite should create a pending invite when camp is not open.");
        Assert.True(result.InboxItems.Any(item => item.Summary.Contains(agent.Name, StringComparison.Ordinal)), "Camp invite inbox should name the free agent.");
    }

    public void WithdrawOfferWorks()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First(item => item.Interest.PlayerOrganizationInterest >= 35);
        var offered = new FreeAgentMarketService().OfferContract(created.Registry, created.ScenarioSnapshot, agent.PersonId).ScenarioSnapshot;
        var result = new FreeAgentMarketService().WithdrawOffer(created.Registry, offered, agent.PersonId);

        Assert.True(result.Success, "Withdraw offer should succeed.");
        Assert.Equal(FreeAgentStatus.Withdrawn, result.ScenarioSnapshot.FreeAgentMarket!.Find(agent.PersonId)!.Status);
    }

    public void LowInterestFreeAgentCanRejectOffer()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents[0];
        var lowInterest = agent with { Interest = agent.Interest with { PlayerOrganizationInterest = 20 } };
        var scenario = created.ScenarioSnapshot with { FreeAgentMarket = created.ScenarioSnapshot.FreeAgentMarket!.Replace(lowInterest) };
        var result = new FreeAgentMarketService().OfferContract(created.Registry, scenario, agent.PersonId);

        Assert.True(result.Success, "Low-interest rejection should be a completed market result.");
        Assert.Equal(FreeAgentStatus.Rejected, result.ScenarioSnapshot.FreeAgentMarket!.Find(agent.PersonId)!.Status);
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.FreeAgentOfferRejected), "Rejection should create an inbox item.");
    }

    public void OtherTeamFreeAgentSigningGoesToLeagueNewsNotInbox()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var agent = scenario.FreeAgentMarket!.FreeAgents[0];
        var result = new FreeAgentMarketService().RecordOtherTeamSigning(scenario, agent.PersonId, "Regina Plainsmen");

        Assert.True(result.Success, "Other-team signing should be recorded.");
        Assert.Equal(0, result.InboxItems.Count);
        Assert.True(result.LeagueTransactions.Any(transaction => transaction.TransactionType == LeagueTransactionType.PlayerSigned && transaction.PersonName == agent.Name), "Other-team signing should create a league transaction.");
    }

    public void PlayerDossierIncludesFreeAgentSectionsWithoutHiddenRatings()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var agent = scenario.FreeAgentMarket!.FreeAgents[0];
        var dossier = new PlayerDossierService().CreateDossier(scenario, agent.PersonId);
        var text = string.Join(" ", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Free agent", StringComparison.OrdinalIgnoreCase), "Dossier should show free-agent status/source.");
        Assert.True(text.Contains(agent.ContractAsk.AnnualAmount.ToString("C0"), StringComparison.Ordinal), "Dossier should include contract ask.");
        Assert.False(text.Contains("CurrentAbility", StringComparison.Ordinal), "Dossier must not expose hidden current ability.");
        Assert.False(text.Contains("Potential =", StringComparison.Ordinal), "Dossier must not expose hidden potential.");
    }

    public void AlphaDesktopExposesFreeAgentWorkspaceAndActions()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Free Agents", StringComparison.Ordinal), "Desktop should expose a Free Agents section.");
        Assert.True(source.Contains("BuildFreeAgentRows", StringComparison.Ordinal), "Desktop should build free-agent rows.");
        Assert.True(source.Contains("OfferFreeAgentContractFor", StringComparison.Ordinal), "Desktop should expose selected free-agent contract actions.");
        Assert.True(source.Contains("InviteFreeAgentToCampFor", StringComparison.Ordinal), "Desktop should expose selected free-agent camp invite actions.");
    }

    public void Alpha31HasNoGodotSaveOrFullNegotiation()
    {
        var root = FindRepositoryRoot();
        var integrationFiles = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "client", "AlphaDesktop"), "*.cs", SearchOption.AllDirectories))
            .ToArray();
        var text = string.Join("\n", integrationFiles.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 3.1 should not add Godot dependency.");
        Assert.False(text.Contains("ConversationTree", StringComparison.Ordinal), "Free agency should not add a full conversation tree.");
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
