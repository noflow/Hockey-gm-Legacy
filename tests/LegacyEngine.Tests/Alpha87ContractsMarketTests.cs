using LegacyEngine.Contracts;
using LegacyEngine.Integration;

internal sealed class Alpha87ContractsMarketTests
{
    public void ContractMarketSummaryIncludesAllDecisionSources()
    {
        var career = new MultiLeagueCareerService();
        var team = career.TeamsFor(LeagueExperience.Nhl).First();
        var created = career.CreateScenario(career.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId));
        var summary = new ContractMarketService().BuildSummary(created.ScenarioSnapshot, created.Registry.Rulebook);

        Assert.True(summary.ExpiringContracts.Count > 0, "Contract market should show expiring contracts.");
        Assert.True(summary.RightsDecisions.Count > 0, "Contract market should show rights decisions.");
        Assert.True(summary.FreeAgents.Count > 0, "Contract market should show free agents.");
        Assert.True(!string.IsNullOrWhiteSpace(summary.Summary), "Contract market should explain current pressure.");
    }

    public void ExpiredContractsMoveToExpiredBoard()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var target = created.ScenarioSnapshot.Contracts.First(contract => contract.Status == ContractStatus.Signed);
        var expired = target with
        {
            Term = new ContractTerm(created.ScenarioSnapshot.CurrentDate.AddYears(-2), created.ScenarioSnapshot.CurrentDate.AddDays(-1)),
            SignedOn = created.ScenarioSnapshot.CurrentDate.AddYears(-2),
            ExpiredOn = created.ScenarioSnapshot.CurrentDate.AddDays(-1)
        };
        var contracts = created.ScenarioSnapshot.Contracts
            .Where(contract => contract.PersonId != target.PersonId)
            .Append(expired)
            .ToArray();
        var scenario = created.ScenarioSnapshot with
        {
            Contracts = contracts,
            AlphaSnapshot = created.ScenarioSnapshot.AlphaSnapshot with { Contracts = contracts }
        };

        var summary = new ContractMarketService().BuildSummary(scenario, created.Registry.Rulebook);

        Assert.True(summary.ExpiredContracts.Any(contract => contract.PersonId == target.PersonId), "A past contract should appear on the expired board.");
        Assert.False(summary.ExpiringContracts.Any(contract => contract.PersonId == target.PersonId), "An expired contract should not remain in the expiring queue.");
    }

    public void SignedReplacementRemovesPlayerFromExpiryBoard()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var target = created.ScenarioSnapshot.Contracts.First(contract => contract.Status == ContractStatus.Signed);
        var oldContract = target with
        {
            ContractId = $"{target.ContractId}:old",
            Term = new ContractTerm(created.ScenarioSnapshot.CurrentDate.AddYears(-2), created.ScenarioSnapshot.CurrentDate.AddDays(-1)),
            SignedOn = created.ScenarioSnapshot.CurrentDate.AddYears(-2),
            ExpiredOn = created.ScenarioSnapshot.CurrentDate.AddDays(-1),
            Status = ContractStatus.Expired
        };
        var replacement = target with
        {
            ContractId = $"{target.ContractId}:replacement",
            Term = new ContractTerm(created.ScenarioSnapshot.CurrentDate, created.ScenarioSnapshot.CurrentDate.AddYears(3)),
            SignedOn = created.ScenarioSnapshot.CurrentDate
        };
        var contracts = created.ScenarioSnapshot.Contracts
            .Where(contract => contract.PersonId != target.PersonId)
            .Append(oldContract)
            .Append(replacement)
            .ToArray();
        var scenario = created.ScenarioSnapshot with
        {
            Contracts = contracts,
            AlphaSnapshot = created.ScenarioSnapshot.AlphaSnapshot with { Contracts = contracts }
        };

        var summary = new ContractMarketService().BuildSummary(scenario, created.Registry.Rulebook);

        Assert.False(summary.ExpiredContracts.Any(contract => contract.PersonId == target.PersonId), "A signed replacement should remove the old contract from the expired board.");
        Assert.False(summary.ExpiringContracts.Any(contract => contract.PersonId == target.PersonId), "A long replacement contract should not appear as a current-year expiry.");
    }

    public void NegotiationStartsWithDemandAndDeadline()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var result = new ContractMarketService().StartNegotiation(created.Registry, created.ScenarioSnapshot, agent.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(agent.PersonId, result.Negotiation!.PersonId);
        Assert.True(result.Negotiation.Demand.AnnualSalary > 0, "Demand should include salary.");
        Assert.True(result.Negotiation.DecisionDeadline is not null, "Negotiation should have a decision deadline.");
        Assert.True(result.ScenarioSnapshot.ContractNegotiationHistory.ForPlayer(agent.PersonId).Count == 1, "Negotiation history should be stored.");
    }

    public void AcceptedOfferCompletesSigningWithoutPendingApproval()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents
            .OrderByDescending(item => item.Interest.PlayerOrganizationInterest)
            .First();
        var service = new ContractMarketService();
        var started = service.StartNegotiation(created.Registry, created.ScenarioSnapshot, agent.PersonId);
        var negotiation = started.Negotiation!;
        var result = service.SubmitOffer(created.Registry, started.ScenarioSnapshot, agent.PersonId, negotiation.Demand.AnnualSalary, negotiation.Demand.TermYears);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Negotiation!.LastEvaluation is not null, "Offer should return an explainable evaluation.");
        if (result.Evaluation!.Decision == ContractOfferDecision.Accepted)
        {
            Assert.True(result.Negotiation.Status == ContractNegotiationStatus.Signed, "An accepted contract-market offer should close as signed.");
            Assert.True(result.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == agent.PersonId && contract.Status == ContractStatus.Signed), "An accepted offer should create the signed contract immediately.");
            Assert.False(result.ScenarioSnapshot.PendingActions.Any(action => action.PersonId == agent.PersonId && action.ActionType == PendingGmActionType.ApproveContract && action.IsOpen), "An accepted contract-market offer should not create a second GM approval step.");
        }
        else
        {
            Assert.True(result.Negotiation.Status is ContractNegotiationStatus.Countered or ContractNegotiationStatus.Rejected, "A non-accepted offer should remain a negotiation response.");
        }
    }

    public void NegotiationHistorySurvivesSnapshotRoundTrip()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var result = new ContractMarketService().StartNegotiation(created.Registry, created.ScenarioSnapshot, agent.PersonId);

        result.ScenarioSnapshot.Validate();
        Assert.True(result.ScenarioSnapshot.ContractNegotiations.Count == 1, "Negotiation should be persisted on the scenario snapshot.");
        Assert.True(result.ScenarioSnapshot.ContractNegotiationHistory.Entries.Count == 1, "Negotiation history should be persisted on the scenario snapshot.");
    }

    public void ContractComparablesUseVisibleContractInformationOnly()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First();
        var comparables = new ContractMarketService().BuildComparables(created.ScenarioSnapshot, player.PersonId);

        Assert.False(comparables.Any(item => item.Context.Contains("true rating", StringComparison.OrdinalIgnoreCase)), "Comparables must not expose hidden ratings.");
        Assert.False(comparables.Any(item => item.Source.Contains("hidden", StringComparison.OrdinalIgnoreCase)), "Comparables must not expose hidden ratings.");
    }

    public void ArbitrationV2SubmissionAndSettlementModelsValidate()
    {
        var submission = new ArbitrationSubmission("submission-1", "case-1", "player-1", "Agent", new DateOnly(2026, 7, 1), 2_000_000m, "Top-six role", "Recent points and role usage support the request.");
        var evidence = new ArbitrationEvidence("evidence-1", "Recent production", "55 points", "Season stats", false);
        var comparable = new ArbitrationComparable("comp-1", "Comparable Player", LegacyEngine.Rosters.RosterPosition.Center, 24, 1_800_000m, 2, "Current contracts", "Same role and age band.");
        var hearing = new ArbitrationHearing("hearing-1", new DateOnly(2026, 7, 15), "League hearing", "Scheduled", "Prepare production and comparable contracts.");
        var settlement = new ArbitrationSettlementOffer("settlement-1", "player-1", 1_900_000m, 2, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), "Settlement proposal", false);

        submission.Validate();
        evidence.Validate();
        comparable.Validate();
        hearing.Validate();
        settlement.Validate();
    }

    public void FreeAgencyTargetBoardShowsCompetitionAndTiming()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var board = new FreeAgencyV3Service().BuildTargetBoard(created.Registry, created.ScenarioSnapshot);

        Assert.True(board.Targets.Count > 0, "Free agency target board should contain available targets.");
        Assert.True(board.Targets.All(target => target.MarketTiming.Length > 0), "Targets should explain market timing.");
        Assert.True(board.Summary.Contains("target", StringComparison.OrdinalIgnoreCase), "Target board should summarize the market.");
    }

    public void ContractOfferCarriesExplicitRolePromises()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var freeAgent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var service = new ContractManagementService();
        var ask = service.BuildAsk(created.ScenarioSnapshot, ContractAskType.FreeAgent, freeAgent.PersonId);
        var request = new ContractOfferBuildRequest(
            freeAgent.PersonId,
            ContractAskType.FreeAgent,
            ask.RequestedSalary,
            ask.RequestedTermYears,
            ask.DesiredRole,
            "A defined development plan",
            false,
            "No staff promise",
            "Explicit promise test")
        {
            PositionPromise = "Center",
            IceTimePromise = "Top-six role",
            NhlRosterPromise = "NHL roster"
        };

        var evaluation = service.BuildOffer(created.Registry, created.ScenarioSnapshot, request);

        Assert.True(evaluation.Explanation.Reasons.Any(reason => reason.Contains("Position promise: Center", StringComparison.Ordinal)), "Position promise should be part of the offer explanation.");
        Assert.True(evaluation.Explanation.Reasons.Any(reason => reason.Contains("Ice-time promise: Top-six role", StringComparison.Ordinal)), "Ice-time promise should be part of the offer explanation.");
        Assert.True(evaluation.Explanation.Reasons.Any(reason => reason.Contains("NHL status promise: NHL roster", StringComparison.Ordinal)), "NHL status promise should be part of the offer explanation.");
    }

    public void MarketOfferPreservesPromisesOnCounterOrSigning()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var freeAgent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var service = new ContractMarketService();
        var started = service.StartNegotiation(created.Registry, created.ScenarioSnapshot, freeAgent.PersonId);
        var result = service.SubmitOffer(
            created.Registry,
            started.ScenarioSnapshot,
            freeAgent.PersonId,
            started.Negotiation!.Demand.AnnualSalary,
            started.Negotiation.Demand.TermYears,
            started.Negotiation.Demand.DesiredRole,
            "Promise persistence test",
            "Center",
            "Top-six role",
            "NHL roster");

        Assert.True(result.Success, result.Message);
        Assert.True(result.Negotiation?.CurrentOffer?.PositionPromise == "Center", "The current offer should retain the position promise.");
        Assert.True(result.Negotiation?.CurrentOffer?.IceTimePromise == "Top-six role", "The current offer should retain the ice-time promise.");
        Assert.True(result.Negotiation?.CurrentOffer?.NhlRosterPromise == "NHL roster", "The current offer should retain the NHL status promise.");
    }

    public void SaveLoadPreservesContractNegotiation()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var agent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var started = new ContractMarketService().StartNegotiation(created.Registry, created.ScenarioSnapshot, agent.PersonId);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha87-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(started.ScenarioSnapshot, created.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(started.ScenarioSnapshot, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, created.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.ContractNegotiations.Any(item => item.PersonId == agent.PersonId), "Contract negotiation should survive save/load.");
        Assert.True(loaded.SaveGame.ScenarioSnapshot.ContractNegotiationHistory.ForPlayer(agent.PersonId).Count > 0, "Contract negotiation history should survive save/load.");
    }

    public void AlphaDesktopExposesContractMarketWorkspace()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Contract Market", StringComparison.Ordinal), "Desktop should expose the unified Contract Market workspace.");
        Assert.True(source.Contains("BuildContractMarketRows", StringComparison.Ordinal), "Desktop should expose selectable contract-market rows.");
        Assert.True(source.Contains("Start Contract Talks", StringComparison.Ordinal), "Desktop should expose contract negotiation actions.");
        Assert.True(source.Contains("Expiring / Expired", StringComparison.Ordinal), "Desktop should expose a dedicated expiry board.");
        Assert.True(source.Contains("Issue Qualifying Offer", StringComparison.Ordinal), "Desktop should label the RFA qualifying-offer action clearly.");
    }

    public void Alpha87DoesNotAddGodotOrFullCba()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n", Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "ContractMarket*.cs", SearchOption.TopDirectoryOnly).Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Contract market should not add Godot.");
        Assert.False(text.Contains("FullCba", StringComparison.OrdinalIgnoreCase), "Contract market should not add a full CBA simulation.");
        Assert.False(text.Contains("ConversationTree", StringComparison.OrdinalIgnoreCase), "Contract market should not add a conversation tree.");
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
