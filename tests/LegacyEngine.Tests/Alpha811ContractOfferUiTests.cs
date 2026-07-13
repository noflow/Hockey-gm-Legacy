using LegacyEngine.Integration;

internal sealed class Alpha811ContractOfferUiTests
{
    public void ExistingPlayerCanOpenContractNegotiation()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var contract = new ContractMarketService().BuildSummary(created.ScenarioSnapshot, created.Registry.Rulebook).ExpiringContracts.FirstOrDefault();

        Assert.True(contract is not null, "The inherited scenario should include an expiring player contract.");
        var result = new ContractMarketService().StartNegotiation(created.Registry, created.ScenarioSnapshot, contract!.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Negotiation?.IsOpen == true, "An expiring player should have an explicit negotiation record.");
    }

    public void ContractUiExposesOfferFormFromRightsAndFreeAgents()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("ShowContractOfferDialog", StringComparison.Ordinal), "Contract screens should open a readable offer form.");
        Assert.True(source.Contains("Offer Contract", StringComparison.Ordinal), "Expiring players and free agents should expose Offer Contract.");
        Assert.True(source.Contains("Annual salary", StringComparison.Ordinal), "The offer form should explain annual salary.");
        Assert.True(source.Contains("Term (years)", StringComparison.Ordinal), "The offer form should capture contract term.");
        Assert.True(source.Contains("Agent opening ask", StringComparison.Ordinal), "The UI should show the agent's opening salary and term before the GM edits an offer.");
        Assert.True(source.Contains("contract is signed immediately", StringComparison.Ordinal), "The UI should explain that an accepted submitted offer completes the contract.");
    }

    public void ContractMarketOfferAcceptsUserEnteredSalaryAndTerm()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("SubmitContractMarketOfferFor(\n        string personId", StringComparison.Ordinal), "The UI should pass salary and term into the contract market.");
        Assert.True(source.Contains("State.SubmitContractMarketOfferFor(\n                    personId,", StringComparison.Ordinal), "Submitting the form should use the entered offer values.");
    }

    public void ContractUiExposesRoleAndRosterPromiseControls()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Position guarantee", StringComparison.Ordinal), "The offer form should let the GM answer position guarantees.");
        Assert.True(source.Contains("Ice-time promise", StringComparison.Ordinal), "The offer form should let the GM offer an ice-time role.");
        Assert.True(source.Contains("NHL status", StringComparison.Ordinal), "The offer form should let the GM offer an NHL roster pathway.");
        Assert.True(source.Contains("Expiring Contracts Quick View", StringComparison.Ordinal), "The contract market should expose a quick expiring-contract view.");
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
