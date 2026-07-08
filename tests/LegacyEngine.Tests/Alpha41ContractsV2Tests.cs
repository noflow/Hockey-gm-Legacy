using LegacyEngine.Integration;
using LegacyEngine.Relationships;

internal sealed class Alpha41ContractsV2Tests
{
    public void ContractAskCreated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var ask = new ContractManagementService().BuildAsk(created.ScenarioSnapshot, ContractAskType.FreeAgent, agent.PersonId);

        Assert.Equal(agent.PersonId, ask.PersonId);
        Assert.True(ask.RequestedSalary > 0, "Ask should include requested salary.");
        Assert.True(ask.RequestedTermYears > 0, "Ask should include requested term.");
        Assert.True(!string.IsNullOrWhiteSpace(ask.DesiredRole), "Ask should include desired role.");
    }

    public void OfferBuilderCalculatesBudgetImpact()
    {
        var prepared = AcceptedOffer();

        Assert.True(prepared.Evaluation.TotalCost > 0, "Offer should calculate total cost.");
        Assert.True(prepared.Evaluation.BudgetRemainingAfter < prepared.Evaluation.BudgetRemainingBefore, "Offer should reduce remaining budget.");
        Assert.True(prepared.Evaluation.Comparison.BudgetRemainingAfter == prepared.Evaluation.BudgetRemainingAfter, "Comparison should include budget after.");
    }

    public void OfferUsesCommonExpiryDate()
    {
        var prepared = AcceptedOffer();
        var expected = ContractExpiryCalendar.TermForYears(prepared.Scenario.CurrentDate, prepared.Scenario.Season.Settings, prepared.Request.TermYears).EndDate;

        Assert.Equal(expected, prepared.Evaluation.Term.EndDate);
        Assert.Equal(prepared.Scenario.CurrentDate, prepared.Evaluation.Term.StartDate);
    }

    public void LikelihoodEstimateGenerated()
    {
        var prepared = AcceptedOffer();

        Assert.True(prepared.Evaluation.Likelihood is ContractLikelihood.Possible or ContractLikelihood.Likely or ContractLikelihood.VeryLikely, "Accepted offer should have a useful likelihood.");
        Assert.True(prepared.Evaluation.DecisionScore > 0, "Decision score should be exposed for explainability.");
    }

    public void AcceptedOfferCreatesPendingGmAction()
    {
        var prepared = AcceptedOffer();
        var result = new ContractManagementService().SubmitOffer(prepared.Created.Registry, prepared.Scenario, prepared.Request);

        Assert.Equal(ContractOfferDecision.Accepted, result.Evaluation.Decision);
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.IsOpen && action.ActionType == PendingGmActionType.ApproveContract && action.PersonId == prepared.Agent.PersonId), "Accepted offer should create pending GM approval.");
        Assert.False(result.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.Agent.PersonId), "Accepted offer should not auto-sign.");
    }

    public void RejectedOfferDoesNotCreateContract()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var request = new ContractOfferBuildRequest(agent.PersonId, ContractAskType.FreeAgent, 0m, 1, "No clear role", "No development commitment", false, "No staff promise", "Lowball test offer");
        var result = new ContractManagementService().SubmitOffer(created.Registry, created.ScenarioSnapshot, request);

        Assert.Equal(ContractOfferDecision.Rejected, result.Evaluation.Decision);
        Assert.False(result.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == agent.PersonId), "Rejected offer must not create contract.");
        Assert.False(result.ScenarioSnapshot.PendingActions.Any(action => action.PersonId == agent.PersonId && action.IsOpen), "Rejected offer should not create approval action.");
    }

    public void ApprovedPendingOfferCreatesContract()
    {
        var prepared = AcceptedOffer();
        var offered = new ContractManagementService().SubmitOffer(prepared.Created.Registry, prepared.Scenario, prepared.Request).ScenarioSnapshot;
        var action = offered.PendingActions.Single(action => action.PersonId == prepared.Agent.PersonId && action.ActionType == PendingGmActionType.ApproveContract && action.IsOpen);
        var approved = new PendingGmActionService().Approve(prepared.Created.Registry, offered, action.ActionId);

        Assert.True(approved.Success, approved.Message);
        Assert.True(approved.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.Agent.PersonId), "GM approval should create signed contract.");
        Assert.True(approved.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.Agent.PersonId && contract.Money.SalaryOrStipend == prepared.Request.AnnualSalary), "Signed contract should preserve offered salary.");
    }

    public void DeclinedPendingOfferDoesNotCreateContract()
    {
        var prepared = AcceptedOffer();
        var offered = new ContractManagementService().SubmitOffer(prepared.Created.Registry, prepared.Scenario, prepared.Request).ScenarioSnapshot;
        var action = offered.PendingActions.Single(action => action.PersonId == prepared.Agent.PersonId && action.ActionType == PendingGmActionType.ApproveContract && action.IsOpen);
        var declined = new PendingGmActionService().Decline(prepared.Created.Registry, offered, action.ActionId);

        Assert.True(declined.Success, declined.Message);
        Assert.False(declined.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.Agent.PersonId), "Declined approval should not create contract.");
    }

    public void PlayerRolePromiseAffectsDecision()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var service = new ContractManagementService();
        var ask = service.BuildAsk(created.ScenarioSnapshot, ContractAskType.FreeAgent, agent.PersonId);
        var matching = service.BuildOffer(created.Registry, created.ScenarioSnapshot, new ContractOfferBuildRequest(agent.PersonId, ContractAskType.FreeAgent, ask.RequestedSalary, ask.RequestedTermYears, ask.DesiredRole, "Development plan", true, "No staff promise", "Matching role"));
        var vague = service.BuildOffer(created.Registry, created.ScenarioSnapshot, new ContractOfferBuildRequest(agent.PersonId, ContractAskType.FreeAgent, ask.RequestedSalary, ask.RequestedTermYears, "Undefined depth", "Development plan", true, "No staff promise", "Vague role"));

        Assert.True(matching.DecisionScore > vague.DecisionScore, "Matching role promise should improve score.");
    }

    public void StaffRoleFocusPromiseAffectsDecision()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var staff = created.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole != LegacyEngine.Staff.StaffRole.GeneralManager);
        var service = new ContractManagementService();
        var ask = service.BuildAsk(created.ScenarioSnapshot, ContractAskType.StaffMember, staff.PersonId);
        var matching = service.BuildOffer(created.Registry, created.ScenarioSnapshot, new ContractOfferBuildRequest(staff.PersonId, ContractAskType.StaffMember, ask.RequestedSalary, ask.RequestedTermYears, "Staff role", "Professional support", false, ask.DesiredRole, "Clear staff focus"));
        var vague = service.BuildOffer(created.Registry, created.ScenarioSnapshot, new ContractOfferBuildRequest(staff.PersonId, ContractAskType.StaffMember, ask.RequestedSalary, ask.RequestedTermYears, "Staff role", "Professional support", false, "Undefined", "No clear staff focus"));

        Assert.True(matching.DecisionScore > vague.DecisionScore, "Staff role/focus promise should improve score.");
    }

    public void RelationshipAffectsDecision()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var low = WithRelationship(created.ScenarioSnapshot, agent.PersonId, 20);
        var high = WithRelationship(created.ScenarioSnapshot, agent.PersonId, 90);
        var service = new ContractManagementService();
        var ask = service.BuildAsk(high, ContractAskType.FreeAgent, agent.PersonId);
        var request = new ContractOfferBuildRequest(agent.PersonId, ContractAskType.FreeAgent, ask.RequestedSalary * 0.8m, ask.RequestedTermYears, ask.DesiredRole, "Development plan", true, "No staff promise", "Relationship test");
        var lowEval = service.BuildOffer(created.Registry, low, request);
        var highEval = service.BuildOffer(created.Registry, high, request);

        Assert.True(highEval.DecisionScore > lowEval.DecisionScore, "Higher relationship/trust should improve contract score.");
        Assert.Equal(90, service.BuildAsk(high, ContractAskType.FreeAgent, agent.PersonId).RelationshipTrustImpact);
    }

    public void ExpiringContractScreenDataGenerated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var summary = new ContractManagementService().BuildSummary(created.ScenarioSnapshot, created.Registry.Rulebook);

        Assert.True(summary.UnsignedProspects.Count >= 0, "Summary should be generated.");
        Assert.True(summary.Budget.TotalBudget > 0, "Summary should include budget.");
    }

    public void ContractComparisonGenerated()
    {
        var prepared = AcceptedOffer();

        Assert.True(prepared.Evaluation.Comparison.AskAnnualCost > 0, "Comparison should include ask.");
        Assert.True(!string.IsNullOrWhiteSpace(prepared.Evaluation.Comparison.LikelyReaction), "Comparison should include likely reaction.");
    }

    public void InboxMessagesIncludeExplanation()
    {
        var prepared = AcceptedOffer();
        var result = new ContractManagementService().SubmitOffer(prepared.Created.Registry, prepared.Scenario, prepared.Request);

        Assert.True(result.InboxItems.Any(item => item.Summary.Contains(prepared.Agent.Name, StringComparison.Ordinal) || item.Summary.Contains("GM must approve", StringComparison.OrdinalIgnoreCase)), "Inbox should include person/context/explanation.");
    }

    public void LeagueNewsReceivesOtherTeamNotableSigning()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var transaction = new ContractManagementService().RecordOtherTeamNotableSigning(created.ScenarioSnapshot, "Regina Plainsmen", agent.PersonId, 2_500m);

        Assert.Equal(LeagueTransactionType.ContractSigned, transaction.TransactionType);
        Assert.Equal(LeagueNewsCategory.Signings, transaction.Category);
        Assert.True(transaction.Description.Contains(agent.Name, StringComparison.Ordinal), "League transaction should name the person.");
    }

    public void NoAutoSigning()
    {
        var prepared = AcceptedOffer();
        var before = prepared.Scenario.Contracts.Count;
        var result = new ContractManagementService().SubmitOffer(prepared.Created.Registry, prepared.Scenario, prepared.Request);

        Assert.Equal(before, result.ScenarioSnapshot.Contracts.Count);
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.ApproveContract), "Accepted offer should stop at pending approval.");
    }

    public void AlphaDesktopExposesContractManagementUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Contracts", StringComparison.Ordinal), "Desktop should expose Contracts workspace.");
        Assert.True(source.Contains("BuildContractsWorkspace", StringComparison.Ordinal), "Desktop should build contract management screen.");
        Assert.True(source.Contains("Accepted Offers Awaiting GM Approval", StringComparison.Ordinal), "Desktop should show accepted offers awaiting approval.");
    }

    public void Alpha41HasNoGodotSaveConversationOrDatabaseSystem()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Contract*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 4.1 should not add Godot.");
        Assert.False(text.Contains("ConversationTree", StringComparison.Ordinal), "Contracts should not add full conversation trees.");
        Assert.False(text.Contains("ArbitrationEngine", StringComparison.Ordinal), "Contracts should not add arbitration.");
        Assert.False(text.Contains("OfferSheetEngine", StringComparison.Ordinal), "Contracts should not add offer sheets.");
        Assert.False(text.Contains("DbContext", StringComparison.Ordinal), "Alpha 4.1 should not add database persistence.");
    }

    private static (NewGmScenarioResult Created, NewGmScenarioSnapshot Scenario, FreeAgent Agent, ContractOfferBuildRequest Request, ContractOfferEvaluation Evaluation) AcceptedOffer()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents
            .OrderByDescending(agent => agent.Interest.PlayerOrganizationInterest)
            .First();
        var service = new ContractManagementService();
        var ask = service.BuildAsk(created.ScenarioSnapshot, ContractAskType.FreeAgent, agent.PersonId);
        var request = new ContractOfferBuildRequest(
            agent.PersonId,
            ContractAskType.FreeAgent,
            ask.RequestedSalary + 250m,
            ask.RequestedTermYears,
            ask.DesiredRole,
            "Clear development plan with regular staff reviews",
            true,
            "No staff promise",
            "Strong offer for test");
        var evaluation = service.BuildOffer(created.Registry, created.ScenarioSnapshot, request);

        Assert.Equal(ContractOfferDecision.Accepted, evaluation.Decision);
        return (created, created.ScenarioSnapshot, agent, request, evaluation);
    }

    private static NewGmScenarioSnapshot WithRelationship(NewGmScenarioSnapshot scenario, string personId, int value)
    {
        var relationship = new Relationship(
            $"relationship:test:{value}:{personId}",
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            personId,
            RelationshipType.Professional,
            value,
            value,
            value,
            value,
            value,
            0,
            0,
            scenario.CurrentDate,
            true,
            Array.Empty<RelationshipHistoryEntry>());
        var relationships = scenario.AlphaSnapshot.Relationships
            .Where(existing => existing.ToPersonId != personId || existing.FromPersonId != scenario.AlphaSnapshot.GeneralManager.PersonId)
            .Append(relationship)
            .ToArray();
        return scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { Relationships = relationships } };
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
