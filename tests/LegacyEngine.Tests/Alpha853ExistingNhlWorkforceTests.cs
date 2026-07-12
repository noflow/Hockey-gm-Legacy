using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha853ExistingNhlWorkforceTests
{
    public void NhlWorkforceIncludesYoungPrimeVeteranAndAgingPlayers()
    {
        var scenario = NhlScenario();
        var distribution = scenario.LeagueWorkforce!.AgeDistribution;

        Assert.True(distribution.Age18To21 > 0, "NHL workforce should include rookies and young players.");
        Assert.True(distribution.Age25To29 > 0, "NHL workforce should include prime-age players.");
        Assert.True(distribution.Age34To36 > 0, "NHL workforce should include aging veterans.");
        Assert.True(distribution.Age37Plus > 0, "NHL workforce should include a small number of late-career players.");
    }

    public void TeamWorkforceHasMixedCareerStagesAndUpcomingContracts()
    {
        var scenario = NhlScenario();
        var profile = scenario.LeagueWorkforce!.Teams.Single(team => team.OrganizationId == scenario.Organization.OrganizationId);

        Assert.True(profile.CareerStageDistribution.Counts.Keys.Count() >= 3, "An NHL team should have multiple career stages.");
        Assert.True(profile.Players.Any(player => player.ContractYearsRemaining <= 1), "Starting roster should include expiring-contract decisions.");
        Assert.True(profile.Players.GroupBy(player => player.ContractYearsRemaining).Count() >= 3, "Starting contracts should have varied remaining terms.");
    }

    public void LeagueStrategiesProduceDifferentAgeShapes()
    {
        var scenario = NhlScenario();
        var contender = scenario.LeagueWorkforce!.Teams.First(team => team.Strategy.Contains("Win", StringComparison.OrdinalIgnoreCase));
        var builder = scenario.LeagueWorkforce.Teams.First(team => team.Strategy.Contains("Prospect", StringComparison.OrdinalIgnoreCase));

        Assert.True(contender.AgeDistribution.Age30To33 + contender.AgeDistribution.Age34To36 >= builder.AgeDistribution.Age30To33 + builder.AgeDistribution.Age34To36, "Contenders should generally carry at least as much veteran weight as prospect builders.");
        Assert.True(builder.AgeDistribution.Age18To21 + builder.AgeDistribution.Age22To24 != contender.AgeDistribution.Age18To21 + contender.AgeDistribution.Age22To24, "Prospect builders should have a distinct young-player runway.");
    }

    public void StartingFreeAgentMarketHasCareerAndQualityVariation()
    {
        var scenario = NhlScenario();
        var market = scenario.FreeAgentMarket!;

        Assert.True(market.FreeAgents.Any(agent => agent.Age <= 24), "Free-agent market should include young players.");
        Assert.True(market.FreeAgents.Any(agent => agent.Age is >= 25 and <= 31), "Free-agent market should include prime-age replacement options.");
        Assert.True(market.FreeAgents.Any(agent => agent.Age >= 34), "Free-agent market should include veterans.");
        Assert.True(market.FreeAgents.Any(agent => agent.RetirementRisk >= RetirementRisk.ConsideringRetirement), "Free-agent market should include near-retirement candidates.");
        Assert.True(market.FreeAgents.Count(agent => agent.MarketTier == FreeAgentMarketTier.ImpactFreeAgent) <= 2, "Impact free agents should stay rare.");
        Assert.True(market.FreeAgents.Any(agent => agent.MarketTier is FreeAgentMarketTier.RolePlayer or FreeAgentMarketTier.VeteranDepth), "Market should include useful role players.");
    }

    public void VeteranFreeAgentsUseShortTermContenderAwareAsks()
    {
        var scenario = NhlScenario();
        var veteran = scenario.FreeAgentMarket!.FreeAgents.First(agent => agent.RetirementRisk >= RetirementRisk.ConsideringRetirement);

        Assert.Equal(1, veteran.ContractAsk.TermYears);
        Assert.True(veteran.FinalContractPreference?.PrefersContender == true, "Veteran final-contract candidates should value contender opportunity.");
    }

    public void OlderPlayersCarryPriorCareerHistory()
    {
        var scenario = NhlScenario();
        var veteran = scenario.AlphaSnapshot.Roster.Players
            .Select(player => scenario.AlphaSnapshot.People.Single(person => person.PersonId == player.PersonId))
            .OrderByDescending(person => person.CalculateAge(scenario.CurrentDate))
            .First();
        var career = scenario.CareerStatSummaries.Single(summary => summary.PersonId == veteran.PersonId);

        Assert.True(career.Seasons >= 10, "Older NHL roster players need multi-year career summaries.");
        Assert.True(scenario.PriorSeasonStats.Any(stat => stat.PersonId == veteran.PersonId), "Older NHL roster players need a previous-season stat line.");
    }

    public void ContractManagementAndRightsExposeStartOfCareerDecisions()
    {
        var scenario = NhlScenario();
        var summary = new ContractManagementService().BuildSummary(scenario, RulebookPresets.CreateNhlStyle());

        Assert.True(summary.ExpiringPlayers.Count > 0, "Contract Management should surface expiring players at NHL career start.");
        Assert.True(scenario.PlayerRightsDecisions.Any(decision => decision.RightsStatus == FreeAgentRightsStatus.PendingRfa), "Starting NHL roster should include RFA projection.");
        Assert.True(scenario.PlayerRightsDecisions.Any(decision => decision.RightsStatus == FreeAgentRightsStatus.PendingUfa), "Starting NHL roster should include UFA projection.");
    }

    public void WorkforceValidationPassesAndSaveLoadPreservesMarket()
    {
        var scenario = NhlScenario();
        var file = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha853-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateNhlStyle());
        var saved = new SaveGameService().SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, file);
        var loaded = new SaveGameService().LoadFromFile(file, RulebookPresets.CreateNhlStyle());

        Assert.True(scenario.WorkforceValidation?.IsValid == true, scenario.WorkforceValidation?.Summary ?? "Workforce validation missing.");
        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.FreeAgentMarket!.FreeAgents.Count, loaded.SaveGame!.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.Count);
        Assert.Equal(scenario.LeagueWorkforce!.LeaguePlayers.Count, loaded.SaveGame.ScenarioSnapshot.LeagueWorkforce!.LeaguePlayers.Count);
        Assert.True(loaded.SaveGame.ScenarioSnapshot.FreeAgentMarket.FreeAgents.All(agent => loaded.SaveGame.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(agent.PersonId) is null), "No player should be duplicated between active roster and free-agent market.");
    }

    public void DesktopExposesFreeAgentAndContractContext()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Impact Players", StringComparison.Ordinal), "Free-agent UI should expose market filters.");
        Assert.True(source.Contains("Near Retirement", StringComparison.Ordinal), "Free-agent UI should expose retirement filter.");
        Assert.True(source.Contains("Market tier", StringComparison.Ordinal), "Free-agent detail should show market tier.");
        Assert.True(source.Contains("NHL Contract Context", StringComparison.Ordinal), "Contracts UI should show opening NHL contract context.");
        Assert.True(source.Contains("League Market Watch", StringComparison.Ordinal), "Contracts UI should show public other-team expiry watch.");
    }

    public void HasNoGodotOrRealPlayerDatabaseDependency()
    {
        var root = FindRepositoryRoot();
        var source = string.Join(Environment.NewLine, Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Workforce*.cs").Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*RetirementWatch*.cs")).Select(File.ReadAllText));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Workforce realism must remain engine-only.");
        Assert.False(source.Contains("real player database", StringComparison.OrdinalIgnoreCase), "Workforce realism must not add a real-player database.");
    }

    private static NewGmScenarioSnapshot NhlScenario()
    {
        var careers = new MultiLeagueCareerService();
        var team = careers.TeamsFor(LeagueExperience.Nhl).First();
        return careers.CreateScenario(careers.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId)).ScenarioSnapshot;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HockeyGmLegacy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be located.");
    }
}
