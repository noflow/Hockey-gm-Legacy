using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.World;

internal sealed class Alpha56SalaryCapRosterComplianceTests
{
    public void ProfessionalRulebooksEnableSalaryCap()
    {
        var service = new SalaryCapService();
        var nhl = service.BuildProfile(RulebookPresets.CreateNhlStyle());
        var ahl = service.BuildProfile(RulebookPresets.CreateAhlStyle());

        Assert.True(nhl.IsEnabled && nhl.CapAmount > 0 && nhl.SalaryFloor > 0, "NHL-style rulebooks should enable salary cap rules.");
        Assert.True(ahl.IsEnabled && ahl.CapAmount > 0 && ahl.SalaryFloor > 0, "AHL-style rulebooks should enable salary cap rules.");
    }

    public void JuniorRulebookDisablesSalaryCap()
    {
        var profile = new SalaryCapService().BuildProfile(RulebookPresets.CreateJuniorMajor());

        Assert.False(profile.IsEnabled, "Junior rulebooks should use operating budgets, not salary cap rules.");
    }

    public void CapCalculationCountsSignedPlayerContracts()
    {
        var created = CreateProfessionalScenario();
        var scenario = WithSignedContract(created.ScenarioSnapshot, "cap-test-player-001", 2_500_000m, 2);
        var snapshot = new SalaryCapService().BuildSnapshot(scenario, created.Registry.Rulebook);

        Assert.True(snapshot.IsEnabled, "Professional scenario should enable salary cap snapshot.");
        Assert.True(snapshot.CurrentCapHit >= 2_500_000m, "Signed player contracts should count toward current cap hit.");
        Assert.True(snapshot.AvailableCapSpace < snapshot.Profile.CapAmount, "Available cap space should drop after signed commitments.");
        Assert.True(snapshot.ContractCommitments.Any(item => item.PersonId == "cap-test-player-001"), "Contract commitments should include player identity.");
    }

    public void CapUpdatesAfterSigning()
    {
        var created = CreateProfessionalScenario();
        var service = new SalaryCapService();
        var before = service.BuildSnapshot(created.ScenarioSnapshot, created.Registry.Rulebook);
        var afterScenario = WithSignedContract(created.ScenarioSnapshot, "cap-test-player-002", 1_750_000m, 3);
        var after = service.BuildSnapshot(afterScenario, created.Registry.Rulebook);

        Assert.True(after.CurrentCapHit > before.CurrentCapHit, "Signing should increase cap hit.");
        Assert.True(after.AvailableCapSpace < before.AvailableCapSpace, "Signing should reduce available cap space.");
    }

    public void CapUpdatesAfterTrade()
    {
        var created = CreateProfessionalScenario();
        var scenario = WithSignedContract(created.ScenarioSnapshot, created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers[0].PersonId, 1_000_000m, 1);
        var outgoing = new TradeAsset(TradeAssetType.Player, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, "outgoing", "Outgoing Player", RosterPosition.Center, 24, 1_000_000m, 50);
        var incoming = new TradeAsset(TradeAssetType.Player, TradeSide.OtherOrganization, "org-other", "Other Club", "incoming", "Incoming Player", RosterPosition.Defense, 25, 4_000_000m, 55);
        var offer = new TradeOffer("trade-cap-test", scenario.CurrentDate, "org-other", "Other Club", TradeOfferStatus.Drafted, new[] { outgoing }, new[] { incoming });
        var calculation = new SalaryCapService().ProjectAfterTrade(scenario, created.Registry.Rulebook, offer);

        Assert.True(calculation.After.CurrentCapHit > calculation.Before.CurrentCapHit, "Receiving higher salary should increase cap projection.");
    }

    public void TradeValidationRejectsOverCapMove()
    {
        var created = CreateProfessionalScenario();
        var tinyRulebook = WithSalaryCap(created.Registry.Rulebook!, capAmount: 1_000m, floor: 0m, maxContracts: 50);
        var registry = created.Registry with { Rulebook = tinyRulebook };
        var scenario = created.ScenarioSnapshot;
        var service = new TradeService();
        var target = scenario.TradeBlock!.Entries.OrderByDescending(entry => entry.SalaryImpact).First();
        var give = service.CreateRosterPlayerAsset(scenario, scenario.AlphaSnapshot.Roster.ActivePlayers[0].PersonId);
        var receive = service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization);
        var offer = service.CreateOffer(scenario, target.OrganizationId, target.TeamName, new[] { give }, new[] { receive });
        var result = service.ProposeTrade(registry, scenario, offer);

        Assert.False(result.Success, "Over-cap trade should fail validation.");
        Assert.True(result.Message.Contains("salary cap", StringComparison.OrdinalIgnoreCase), "Failure should explain salary cap impact.");
    }

    public void RosterComplianceFlagsContractLimit()
    {
        var created = CreateProfessionalScenario();
        var tinyRulebook = WithSalaryCap(created.Registry.Rulebook!, capAmount: 100_000_000m, floor: 0m, maxContracts: 1);
        var scenario = WithSignedContract(created.ScenarioSnapshot, "cap-test-player-003", 1_000_000m, 1);
        scenario = WithSignedContract(scenario, "cap-test-player-004", 1_000_000m, 1);
        var compliance = new SalaryCapService().ValidateRosterCompliance(scenario, tinyRulebook);

        Assert.False(compliance.IsCompliant, "Contract count above the rulebook limit should fail compliance.");
        Assert.True(compliance.Reasons.Any(reason => reason.Contains("contract limit", StringComparison.OrdinalIgnoreCase)), "Compliance reason should name contract limit.");
    }

    public void FreeAgencyOfferRespectsCap()
    {
        var created = CreateProfessionalScenario();
        var tinyRulebook = WithSalaryCap(created.Registry.Rulebook!, capAmount: 1_000m, floor: 0m, maxContracts: 50);
        var registry = created.Registry with { Rulebook = tinyRulebook };
        var service = new FreeAgencyV2Service();
        var scenario = OpenMarket(created.ScenarioSnapshot);
        scenario = service.EnsureMarketState(registry, scenario);
        var agent = scenario.FreeAgentMarket!.FreeAgents.First(agent => agent.Status == FreeAgentStatus.Available);
        var result = service.SubmitOffer(registry, scenario, agent.PersonId, 5_000_000m, 1);

        Assert.False(result.Success, "Free agency offer should be blocked when it cannot fit under cap.");
        Assert.True(result.Message.Contains("salary cap", StringComparison.OrdinalIgnoreCase), "Free agency cap block should explain cap impact.");
    }

    public void DashboardAndDecisionScreensExposeCapDetails()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Salary Cap", StringComparison.Ordinal), "Dashboard or budget screen should expose salary cap.");
        Assert.True(source.Contains("Cap after signing", StringComparison.Ordinal), "Free agency/contract screen should preview cap after signing.");
        Assert.True(source.Contains("Cap before", StringComparison.Ordinal), "Trade builder should preview cap before trade.");
        Assert.True(source.Contains("Cap indicator", StringComparison.Ordinal), "Trade builder should show green/red cap indicator text.");
    }

    public void SaveLoadPreservesCapCommitments()
    {
        var created = CreateProfessionalScenario();
        var scenario = WithSignedContract(created.ScenarioSnapshot, "cap-save-player", 3_250_000m, 4);
        var service = new SalaryCapService();
        var before = service.BuildSnapshot(scenario, created.Registry.Rulebook);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha56-{Guid.NewGuid():N}.json");
        var save = new SaveGameService();
        var budget = new BudgetOverviewService().Build(scenario, created.Registry.Rulebook ?? RulebookPresets.CreateNhlStyle());
        var saved = save.SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = save.LoadFromFile(path, created.Registry.Rulebook);
        var after = service.BuildSnapshot(loaded.SaveGame!.ScenarioSnapshot, created.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(before.CurrentCapHit, after.CurrentCapHit);
        Assert.True(after.ContractCommitments.Any(item => item.PersonId == "cap-save-player"), "Loaded career should preserve cap-counted contract.");
    }

    public void NoForbiddenCapSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "SalaryCap*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.False(text.Contains("LTIR", StringComparison.OrdinalIgnoreCase), "Alpha 5.6 should not implement LTIR.");
        Assert.False(text.Contains("Buyout", StringComparison.OrdinalIgnoreCase), "Alpha 5.6 should not implement buyouts.");
        Assert.False(text.Contains("OfferSheetEngine", StringComparison.OrdinalIgnoreCase), "Alpha 5.6 should not implement offer sheets.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 5.6 should not add Godot.");
    }

    private static NewGmScenarioResult CreateProfessionalScenario()
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Nhl).First();
        return service.CreateScenario(service.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId));
    }

    private static NewGmScenarioSnapshot WithSignedContract(NewGmScenarioSnapshot scenario, string personId, decimal salary, int years)
    {
        var contract = new Contract(
            $"contract-cap-test:{personId}:{Guid.NewGuid():N}",
            personId,
            scenario.Organization.OrganizationId,
            ContractType.JuniorPlayerAgreement,
            ContractStatus.Signed,
            ContractExpiryCalendar.TermForYears(scenario.CurrentDate, scenario.Season.Settings, years),
            new ContractMoney(salary),
            Array.Empty<ContractClause>(),
            scenario.CurrentDate,
            scenario.CurrentDate,
            null,
            null,
            null);
        var contracts = scenario.Contracts.Append(contract).ToArray();
        var alpha = scenario.AlphaSnapshot with { Contracts = contracts };
        return scenario with { Contracts = contracts, AlphaSnapshot = alpha };
    }

    private static Rulebook WithSalaryCap(Rulebook source, decimal capAmount, decimal floor, int maxContracts) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = source.RosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules,
            SeasonRules = source.SeasonRules,
            StaffRules = source.StaffRules,
            AffiliateRules = source.AffiliateRules,
            PlayerAssignmentRules = source.PlayerAssignmentRules,
            FreeAgentRightsRules = source.FreeAgentRightsRules,
            ArbitrationRules = source.ArbitrationRules,
            SalaryCapRules = new SalaryCapRules
            {
                SalaryCapEnabled = true,
                CapAmount = capAmount,
                SalaryFloor = floor,
                MaximumRosterSize = source.RosterRules?.ActiveRoster ?? 23,
                MaximumContracts = maxContracts,
                MaximumRetainedSalaryPlaceholder = source.SalaryCapRules?.MaximumRetainedSalaryPlaceholder ?? 0m,
                OffseasonCapRulesPlaceholder = source.SalaryCapRules?.OffseasonCapRulesPlaceholder ?? "Test cap rulebook."
            }
        };

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
