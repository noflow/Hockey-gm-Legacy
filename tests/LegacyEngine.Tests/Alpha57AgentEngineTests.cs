using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.World;

internal sealed class Alpha57AgentEngineTests
{
    public void AgentsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.Agents.Count >= 4, "Scenario should generate recurring agents.");
        Assert.True(scenario.Agents.All(agent => !string.IsNullOrWhiteSpace(agent.Profile.AgencyName)), "Agents should include agency names.");
    }

    public void PlayersAssignedAgents()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var rosterIds = scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId).ToArray();
        var freeAgentIds = scenario.FreeAgentMarket!.FreeAgents.Select(agent => agent.PersonId).ToArray();

        Assert.True(rosterIds.All(id => scenario.AgentRepresentations.Any(record => record.PersonId == id)), "Roster players should have representation records.");
        Assert.True(freeAgentIds.All(id => scenario.AgentRepresentations.Any(record => record.PersonId == id && record.AgentId is not null)), "Free agents should have professional agents.");
    }

    public void AgentRelationshipsExist()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var agent = scenario.Agents.First();

        Assert.True(agent.GmRelationship.Score is >= 0 and <= 100, "GM-agent relationship should be scored.");
        Assert.True(agent.OrganizationRelationship.Score is >= 0 and <= 100, "Organization-agent relationship should be scored.");
        Assert.True(agent.CoachRelationship.Score is >= 0 and <= 100, "Coach-agent placeholder relationship should be scored.");
    }

    public void NegotiationStylesAffectOfferReview()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = created.ScenarioSnapshot;
        var service = new ContractManagementService();
        var freeAgent = scenario.FreeAgentMarket!.FreeAgents.First();
        var ask = service.BuildAsk(scenario, ContractAskType.FreeAgent, freeAgent.PersonId);
        var request = new ContractOfferBuildRequest(freeAgent.PersonId, ContractAskType.FreeAgent, ask.RequestedSalary * 0.92m, ask.RequestedTermYears, ask.DesiredRole, "Development plan included", false, "No staff promise", "Agent style test");
        var review = new AgentEngine().ReviewOffer(scenario, ask, request, 60);

        Assert.True(review.ScoreModifier != 0 || review.NegotiationStyle != AgentNegotiationStyle.Collaborative, "Agent style should influence review output.");
        Assert.True(review.Opinion.Contains(review.AgentName, StringComparison.Ordinal), "Review should be voiced by the agent.");
    }

    public void OfferExplanationsIncludeAgentReasons()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var freeAgent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var service = new ContractManagementService();
        var ask = service.BuildAsk(created.ScenarioSnapshot, ContractAskType.FreeAgent, freeAgent.PersonId);
        var request = new ContractOfferBuildRequest(freeAgent.PersonId, ContractAskType.FreeAgent, ask.RequestedSalary, ask.RequestedTermYears, ask.DesiredRole, "Development plan included", false, "No staff promise", "Agent explanation test");
        var evaluation = service.BuildOffer(created.Registry, created.ScenarioSnapshot, request);

        Assert.True(evaluation.AgentReview is not null, "Contract evaluation should include agent review.");
        Assert.True(evaluation.Explanation.Reasons.Any(reason => reason.Contains("Agent opinion", StringComparison.Ordinal)), "Offer reasons should include agent opinion.");
        Assert.True(!string.IsNullOrWhiteSpace(evaluation.AgentCounterSuggestion), "Offer should include an agent counter suggestion.");
    }

    public void FreeAgencyUsesAgentMessages()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new FreeAgencyV2Service();
        var scenario = service.EnsureMarketState(created.Registry, OpenMarket(created.ScenarioSnapshot));
        var freeAgent = scenario.FreeAgentMarket!.FreeAgents.First(agent => agent.Status == FreeAgentStatus.Available);
        var result = service.SubmitOffer(created.Registry, scenario, freeAgent.PersonId, freeAgent.ContractAsk.AnnualAmount, freeAgent.ContractAsk.TermYears);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Message.Contains("Agent", StringComparison.OrdinalIgnoreCase) || result.InboxItems.Any(item => item.Title.Contains("Agent", StringComparison.OrdinalIgnoreCase)), "Free agency response should come through an agent.");
        Assert.True(result.OfferState?.Evaluation.AgentReview is not null, "Free agency offer state should preserve agent review.");
    }

    public void PlayerDossierShowsAgentRepresentation()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var personId = scenario.FreeAgentMarket!.FreeAgents.First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, personId);
        var text = string.Join("\n", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Agent:", StringComparison.Ordinal), "Dossier should show agent.");
        Assert.True(text.Contains("Agency:", StringComparison.Ordinal), "Dossier should show agency.");
        Assert.True(text.Contains("Representation history", StringComparison.Ordinal), "Dossier should show representation history.");
    }

    public void AgentHistoryIsTracked()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.AgentHistory.Count > 0, "Agent history should be seeded.");
        Assert.True(scenario.AgentHistory.Any(history => history.Category.Contains("Players", StringComparison.Ordinal)), "Agent history should track players represented.");
        Assert.True(scenario.AgentHistory.Any(history => history.Summary.Contains("Biggest deals", StringComparison.Ordinal)), "Agent history should include biggest deals placeholder.");
    }

    public void AlphaDesktopExposesAgentContractControls()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Agent Card", StringComparison.Ordinal), "Desktop should expose Agent Card.");
        Assert.True(source.Contains("Negotiation Style", StringComparison.Ordinal), "Desktop should expose negotiation style.");
        Assert.True(source.Contains("Agent comments", StringComparison.Ordinal), "Desktop should expose agent comments.");
        Assert.True(source.Contains("Improve Offer", StringComparison.Ordinal), "Desktop should expose Improve Offer.");
        Assert.True(source.Contains("Compare", StringComparison.Ordinal), "Desktop should expose Compare.");
        Assert.True(source.Contains("View Agent", StringComparison.Ordinal), "Desktop should expose View Agent.");
    }

    public void NoForbiddenAgentSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Agent*.cs", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(Path.Combine(root, "client", "AlphaDesktop"), "*.cs", SearchOption.TopDirectoryOnly))
                .Select(File.ReadAllText));

        Assert.False(text.Contains("ConversationTree", StringComparison.OrdinalIgnoreCase), "Alpha 5.7 should not add conversation trees.");
        Assert.False(text.Contains("VoiceDialogue", StringComparison.OrdinalIgnoreCase), "Alpha 5.7 should not add voice dialogue.");
        Assert.False(text.Contains("ArbitrationEngine", StringComparison.OrdinalIgnoreCase), "Alpha 5.7 should not add arbitration.");
        Assert.False(text.Contains("OfferSheetEngine", StringComparison.OrdinalIgnoreCase), "Alpha 5.7 should not add offer sheets.");
        Assert.False(text.Contains("Ltir", StringComparison.OrdinalIgnoreCase), "Alpha 5.7 should not add LTIR.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 5.7 should not add Godot.");
    }

    private static NewGmScenarioSnapshot OpenMarket(NewGmScenarioSnapshot scenario)
    {
        var service = new FreeAgencyV2Service();
        var window = service.BuildWindow(scenario);
        var world = scenario.AlphaSnapshot.WorldState;
        var snapshot = scenario.AlphaSnapshot with { WorldState = world with { Clock = new WorldClock(new WorldDate(window.OpensOn)) } };
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
