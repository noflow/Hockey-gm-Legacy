using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;

internal sealed class Alpha812ContractWorkflowTests
{
    public void ContractMarketSummaryExposesActiveContractsSeparately()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var summary = new ContractMarketService().BuildSummary(created.ScenarioSnapshot, created.Registry.Rulebook);

        Assert.True(summary.ActiveContracts.Count > 0, "The contract desk should expose current signed contracts.");
        Assert.True(summary.ActiveContracts.All(contract => contract.Status == ContractStatus.Signed), "Active contracts must be signed.");
        Assert.True(summary.ActiveContracts.All(contract => contract.Term.EndDate >= created.ScenarioSnapshot.CurrentDate), "Active contracts must not include expired terms.");
        Assert.True(summary.ActiveContracts.SequenceEqual(summary.ActiveContracts.OrderBy(contract => contract.Term.EndDate)), "Active contracts should be sorted by nearest expiry.");
    }

    public void UnifiedContractManagementExposesDecisionQueuesAndFilters()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Contract Management", StringComparison.Ordinal), "Contract Management should be the core contract workspace.");
        Assert.True(source.Contains("Active Contracts", StringComparison.Ordinal), "Active contracts should have a dedicated view.");
        Assert.True(source.Contains("Expiring This Year", StringComparison.Ordinal), "Expiring contracts should have a dedicated view.");
        Assert.True(source.Contains("Expired / Rights", StringComparison.Ordinal), "Expired contracts and RFA/UFA rights should have a dedicated view.");
        Assert.True(source.Contains("Negotiations & Offers", StringComparison.Ordinal), "Open negotiations should have a dedicated view.");
        Assert.True(source.Contains("Free Agent Market", StringComparison.Ordinal), "Free agents should have a dedicated view.");
        Assert.True(source.Contains("_contractManagementSearchInput", StringComparison.Ordinal), "Contract Management should support player/status search.");
    }

    public void ContractLifecyclePreservesRightsAndRemovesExpiredSalary()
    {
        var career = new MultiLeagueCareerService();
        var team = career.TeamsFor(LeagueExperience.Nhl).First();
        var created = career.CreateScenario(career.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId));
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First(item => item.Position != RosterPosition.Goalie);
        var current = created.ScenarioSnapshot.CurrentDate;
        var contract = created.ScenarioSnapshot.Contracts.First(item => item.PersonId == player.PersonId && item.Status == ContractStatus.Signed);
        var expiredContract = contract with
        {
            Term = new ContractTerm(contract.Term.StartDate, current.AddDays(-1)),
            ExpiredOn = current.AddDays(-1)
        };
        var contracts = created.ScenarioSnapshot.Contracts
            .Where(item => item.PersonId != player.PersonId)
            .Append(expiredContract)
            .ToArray();
        var scenario = created.ScenarioSnapshot with
        {
            Contracts = contracts,
            AlphaSnapshot = created.ScenarioSnapshot.AlphaSnapshot with { Contracts = contracts },
            PlayerRightsDecisions = Array.Empty<PlayerRightsDecision>(),
            RightsHistory = RightsHistory.Empty
        };

        var before = new SalaryCapService().BuildSnapshot(scenario, created.Registry.Rulebook);
        var updated = new ContractExpiryService().ProcessExpiredContracts(scenario, created.Registry.Rulebook);
        var after = new SalaryCapService().BuildSnapshot(updated, created.Registry.Rulebook);

        Assert.Equal(ContractStatus.Expired, updated.Contracts.Single(item => item.PersonId == player.PersonId).Status);
        Assert.True(after.CurrentCapHit < before.CurrentCapHit, "Expired salary should leave the active cap calculation.");
        Assert.True(updated.PlayerRightsDecisions.Any(item => item.PersonId == player.PersonId), "Expiry should refresh the player's RFA/UFA rights record.");
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
