using LegacyEngine.Integration;

internal sealed class Alpha811AgentContractNegotiationTests
{
    public void RfaUfaNegotiationIncludesSalaryAndTermDemand()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var market = new ContractMarketService().BuildSummary(created.ScenarioSnapshot, created.Registry.Rulebook);
        var target = market.ExpiringContracts.FirstOrDefault();

        Assert.True(target is not null, "The inherited organization should have an expiring player contract.");
        var result = new ContractMarketService().StartNegotiation(created.Registry, created.ScenarioSnapshot, target!.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Negotiation?.Demand.AnnualSalary > 0, "RFA/UFA negotiations should include an annual salary demand.");
        Assert.True(result.Negotiation?.Demand.TermYears > 0, "RFA/UFA negotiations should include a contract term.");
        Assert.True(result.Negotiation?.Demand.TeamPreference is not null, "Negotiations should retain player team preferences.");
    }

    public void AgentPreferencesIncludeTeamFitAndHometownDiscounts()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var prepared = new OrganizationPlanningService().EnsurePlans(created.ScenarioSnapshot);
        var freeAgent = prepared.FreeAgentMarket!.FreeAgents[0];
        var organization = prepared.Organization;
        var adjusted = freeAgent with
        {
            Hometown = organization.Identity.City,
            Interest = freeAgent.Interest with { MotivationSummary = "Values hometown stability and the coaching staff." }
        };
        var market = prepared.FreeAgentMarket with
        {
            FreeAgents = prepared.FreeAgentMarket.FreeAgents
                .Select(item => item.PersonId == freeAgent.PersonId ? adjusted : item)
                .ToArray()
        };
        var scenario = prepared with
        {
            FreeAgentMarket = market,
            CurrentOrganizationPlan = prepared.CurrentOrganizationPlan! with { Window = CompetitiveWindow.Competing }
        };
        var ask = new ContractManagementService().BuildAsk(scenario, ContractAskType.FreeAgent, freeAgent.PersonId);
        var fit = new ContractTeamFitService().Evaluate(scenario, ask);

        Assert.True(ask.TeamPreference.HometownImportance >= 75, "A hometown player should value the local organization more.");
        Assert.True(ask.TeamPreference.MaximumHometownDiscountPercent > 0, "A hometown/staff fit should allow a modest discount.");
        Assert.True(fit.HometownDiscountApplied, "A good hometown and staff fit should activate the discount path.");
        Assert.True(fit.AcceptedSalaryDiscountPercent > 0, "The accepted salary target should reflect the hometown discount.");
    }

    public void ContenderPreferenceAddsPoorTeamRejectionRisk()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var prepared = new OrganizationPlanningService().EnsurePlans(created.ScenarioSnapshot);
        var freeAgent = prepared.FreeAgentMarket!.FreeAgents[0] with
        {
            Hometown = "Helsinki, Finland",
            FinalContractPreference = new FinalContractPreference(false, true, true, "Wants a credible contender and will not join a rebuilding club.")
        };
        var market = prepared.FreeAgentMarket with
        {
            FreeAgents = prepared.FreeAgentMarket.FreeAgents
                .Select(item => item.PersonId == freeAgent.PersonId ? freeAgent : item)
                .ToArray()
        };
        var scenario = prepared with
        {
            FreeAgentMarket = market,
            Standings = null,
            CurrentOrganizationPlan = prepared.CurrentOrganizationPlan! with { Window = CompetitiveWindow.Rebuild }
        };
        var ask = new ContractManagementService().BuildAsk(scenario, ContractAskType.FreeAgent, freeAgent.PersonId);
        var fit = new ContractTeamFitService().Evaluate(scenario, ask);

        Assert.True(ask.TeamPreference.PrefersContender, "The agent/player profile should preserve contender preference.");
        Assert.True(fit.TeamFitScore < ask.TeamPreference.MinimumTeamFit, $"A rebuilding organization should fall below a contender player's minimum fit. Actual fit={fit.TeamFitScore}, minimum={ask.TeamPreference.MinimumTeamFit}, winning={fit.WinningScore}, staff={fit.StaffFitScore}, hometown={fit.HometownScore}, relationship={fit.RelationshipScore}.");
        Assert.True(fit.Risk.Contains("reject", StringComparison.OrdinalIgnoreCase), "Poor team fit should explain the rejection risk.");
    }

    public void ContractUiExposesNegotiationApprovalAndRejectionControls()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Accept Counter", StringComparison.Ordinal), "The contract market should let the GM accept a counteroffer.");
        Assert.True(source.Contains("Reject / Walk Away", StringComparison.Ordinal), "The contract market should let the GM reject or walk away.");
        Assert.True(source.Contains("Submit Revised Offer", StringComparison.Ordinal), "A counteroffer should return the GM to an editable revised offer flow.");
        Assert.True(source.Contains("Team preference", StringComparison.Ordinal), "The contract UI should explain player team preferences.");
        Assert.True(source.Contains("Team-fit risk", StringComparison.Ordinal), "The contract UI should explain poor-team rejection risk.");
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
