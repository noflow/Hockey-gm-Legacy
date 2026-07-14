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
        Assert.True(source.Contains("No GM approval remains", StringComparison.Ordinal), "Completed contracts should not be described as awaiting approval.");
        Assert.True(source.Contains("pendingApproval", StringComparison.Ordinal), "Approval actions should be driven by actual pending actions.");
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

    public void SignedReplacementClearsStaleRightsDecision()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First(item => item.Position != RosterPosition.Goalie);
        var target = created.ScenarioSnapshot.Contracts.First(contract => contract.PersonId == player.PersonId && contract.Status == ContractStatus.Signed);
        var person = created.ScenarioSnapshot.AlphaSnapshot.People.First(item => item.PersonId == target.PersonId);
        var stale = new PlayerRightsDecision(
            "rights:stale",
            target.PersonId,
            person.Identity.DisplayName,
            player.Position,
            person.CalculateAge(created.ScenarioSnapshot.CurrentDate),
            1,
            created.ScenarioSnapshot.Organization.OrganizationId,
            created.ScenarioSnapshot.Organization.Name,
            target.ContractId,
            created.ScenarioSnapshot.CurrentDate.AddDays(-1),
            FreeAgentRightsStatus.PendingRfa,
            ContractRightsStatus.PendingRfa,
            true,
            null,
            new RightsExpiryRule("stale-rule", "Qualify before the rights deadline.", created.ScenarioSnapshot.CurrentDate.AddDays(7)),
            created.ScenarioSnapshot.Organization.OrganizationId,
            created.ScenarioSnapshot.Organization.Name,
            "Review the qualifying offer before the deadline.",
            "Agent expects a qualifying offer.",
            "Previous contract expired before the replacement was recorded.",
            created.ScenarioSnapshot.CurrentDate.AddDays(-1),
            created.ScenarioSnapshot.CurrentDate.AddDays(-1));
        var replacement = target with
        {
            ContractId = $"{target.ContractId}:replacement",
            Term = new ContractTerm(created.ScenarioSnapshot.CurrentDate, created.ScenarioSnapshot.CurrentDate.AddYears(4)),
            SignedOn = created.ScenarioSnapshot.CurrentDate
        };
        var replacementScenario = created.ScenarioSnapshot with
        {
            Contracts = created.ScenarioSnapshot.Contracts
                .Where(contract => contract.PersonId != target.PersonId)
                .Append(replacement)
                .ToArray(),
            AlphaSnapshot = created.ScenarioSnapshot.AlphaSnapshot with { Contracts = created.ScenarioSnapshot.Contracts
                .Where(contract => contract.PersonId != target.PersonId)
                .Append(replacement)
                .ToArray() },
            PlayerRightsDecisions = new[] { stale }
        };

        var cleaned = new RfaUfaService().EnsureRights(replacementScenario, created.Registry.Rulebook);

        Assert.False(cleaned.PlayerRightsDecisions.Any(decision => decision.PersonId == target.PersonId), "A long signed replacement must clear the stale RFA/UFA decision.");
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
