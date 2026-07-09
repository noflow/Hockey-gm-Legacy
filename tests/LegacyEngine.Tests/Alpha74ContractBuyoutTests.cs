using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha74ContractBuyoutTests
{
    public void BuyoutEligibilityComesFromRulebook()
    {
        var prepared = PrepareBuyoutScenario();
        var eligibility = new BuyoutService().BuildEligibility(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(BuyoutStatus.Eligible, eligibility.Status);
        Assert.True(prepared.Registry.Rulebook!.BuyoutRules!.BuyoutsEnabled, "NHL-style rulebook should enable buyouts.");
    }

    public void JuniorLeagueBuyoutsDisabled()
    {
        var prepared = PrepareBuyoutScenario(rulebookOverride: RulebookPresets.CreateJuniorMajor());
        var eligibility = new BuyoutService().BuildEligibility(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(BuyoutStatus.NotEligible, eligibility.Status);
        Assert.True(eligibility.Reason.Contains("disabled", StringComparison.OrdinalIgnoreCase), "Disabled rulebook reason should be readable.");
    }

    public void BuyoutBlockedOutsideWindow()
    {
        var prepared = PrepareBuyoutScenario(outsideWindow: true);
        var eligibility = new BuyoutService().BuildEligibility(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.False(eligibility.Status == BuyoutStatus.Eligible, "Player should not be eligible outside the buyout window.");
        Assert.True(eligibility.Reason.Contains("window", StringComparison.OrdinalIgnoreCase), "Outside-window reason should mention the window.");
    }

    public void BuyoutCalculationGenerated()
    {
        var prepared = PrepareBuyoutScenario();
        var result = new BuyoutService().CalculateBuyout(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Buyout!.Calculation.BuyoutCost > 0, "Buyout calculation should produce a positive cost.");
        Assert.True(result.Buyout.Calculation.AnnualPenalty > 0, "Buyout calculation should produce an annual penalty.");
    }

    public void BuyoutCreatesFuturePenalty()
    {
        var prepared = PrepareBuyoutScenario();
        var result = new BuyoutService().CalculateBuyout(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.True(result.Buyout!.Calculation.Penalties.Any(penalty => penalty.SeasonYear > prepared.Scenario.Season.Year), "Buyout should create future penalty seasons.");
    }

    public void BuyoutReleasesPlayerToFreeAgency()
    {
        var prepared = PrepareBuyoutScenario();
        var calculated = new BuyoutService().CalculateBuyout(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var confirmed = new BuyoutService().ConfirmBuyout(prepared.Registry, calculated.ScenarioSnapshot, prepared.PersonId);

        Assert.True(confirmed.Success, confirmed.Message);
        Assert.True(confirmed.ScenarioSnapshot.FreeAgentMarket!.Find(prepared.PersonId) is not null, "Confirmed buyout should release player into free agency.");
        Assert.True(confirmed.ScenarioSnapshot.Contracts.Concat(confirmed.ScenarioSnapshot.AlphaSnapshot.Contracts).Any(contract => contract.PersonId == prepared.PersonId && contract.Status == ContractStatus.Terminated), "Original contract should be terminated.");
    }

    public void BuyoutUpdatesCapSnapshot()
    {
        var prepared = PrepareBuyoutScenario();
        var calculated = new BuyoutService().CalculateBuyout(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var confirmed = new BuyoutService().ConfirmBuyout(prepared.Registry, calculated.ScenarioSnapshot, prepared.PersonId);
        var cap = new SalaryCapService().BuildSnapshot(confirmed.ScenarioSnapshot, prepared.Registry.Rulebook);

        Assert.True(cap.BuyoutPenalties.Count > 0, "Salary cap snapshot should expose buyout penalties.");
        Assert.True(cap.DeadCapPlaceholder > 0, "Current-season buyout penalty should count as dead cap placeholder.");
    }

    public void BuyoutCreatesHistoryEntry()
    {
        var prepared = PrepareBuyoutScenario();
        var calculated = new BuyoutService().CalculateBuyout(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var confirmed = new BuyoutService().ConfirmBuyout(prepared.Registry, calculated.ScenarioSnapshot, prepared.PersonId);

        Assert.True(confirmed.ScenarioSnapshot.BuyoutHistory.ForPlayer(prepared.PersonId).Any(item => item.DecisionType == BuyoutDecisionType.Confirm), "Buyout history should record confirmation.");
        Assert.True(confirmed.ScenarioSnapshot.TransactionHistory.Any(item => item.TransactionType.Contains("Buyout", StringComparison.Ordinal)), "Transaction history should record buyout.");
    }

    public void BuyoutRecordsRelationshipImpact()
    {
        var prepared = PrepareBuyoutScenario();
        var calculated = new BuyoutService().CalculateBuyout(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var confirmed = new BuyoutService().ConfirmBuyout(prepared.Registry, calculated.ScenarioSnapshot, prepared.PersonId);

        Assert.True(confirmed.ScenarioSnapshot.RelationshipChangeHistory.Any(item => item.Reason.Contains("buyout", StringComparison.OrdinalIgnoreCase)), "Player/agent relationship history should record buyout impact.");
    }

    public void ActionCenterShowsBuyoutWindow()
    {
        var prepared = PrepareBuyoutScenario();
        var budget = new BudgetOverviewService().Build(prepared.Scenario, prepared.Registry.Rulebook!);
        var readiness = new SeasonReadinessService().Evaluate(prepared.Registry, prepared.Scenario);
        var items = new ActionCenterService().BuildItems(prepared.Scenario, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Contracts && item.Title.Contains("Buyout", StringComparison.OrdinalIgnoreCase)), "Action Center should expose buyout window/review.");
    }

    public void AlphaDesktopExposesBuyoutUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Contract Buyouts", StringComparison.Ordinal), "Desktop should expose buyout section.");
        Assert.True(source.Contains("Calculate Buyout", StringComparison.Ordinal), "Desktop should expose calculate buyout action.");
        Assert.True(source.Contains("Confirm Buyout", StringComparison.Ordinal), "Desktop should expose confirm buyout action.");
    }

    public void SaveLoadPreservesBuyoutPenalties()
    {
        var prepared = PrepareBuyoutScenario();
        var calculated = new BuyoutService().CalculateBuyout(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var confirmed = new BuyoutService().ConfirmBuyout(prepared.Registry, calculated.ScenarioSnapshot, prepared.PersonId);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha74-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(confirmed.ScenarioSnapshot, prepared.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(confirmed.ScenarioSnapshot, Array.Empty<InboxMessage>(), confirmed.LeagueTransactions, new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, prepared.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.ContractBuyouts.Any(item => item.PersonId == prepared.PersonId && item.Status == BuyoutStatus.Completed), "Completed buyout should survive save/load.");
        Assert.True(loaded.SaveGame.ScenarioSnapshot.ContractBuyouts.SelectMany(item => item.Calculation.Penalties).Any(), "Buyout penalty schedule should survive save/load.");
    }

    public void NoLtirRetainedSalaryOrGodotAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Buyout*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.False(text.Contains("LTIR", StringComparison.OrdinalIgnoreCase), "Alpha 7.4 should not implement LTIR.");
        Assert.False(text.Contains("RetainedSalary", StringComparison.OrdinalIgnoreCase), "Alpha 7.4 should not implement retained salary.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 7.4 should not add Godot.");
    }

    private static PreparedBuyoutScenario PrepareBuyoutScenario(Rulebook? rulebookOverride = null, bool outsideWindow = false)
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Nhl).First();
        var sourceRulebook = rulebookOverride ?? RulebookPresets.CreateNhlStyle();
        var selection = service.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId);
        var created = service.CreateScenario(selection with { LeagueProfile = selection.LeagueProfile with { Rulebook = sourceRulebook }, Rulebook = sourceRulebook });
        var currentDate = created.ScenarioSnapshot.CurrentDate;
        var offset = Math.Max(0, currentDate.DayNumber - created.ScenarioSnapshot.Season.Calendar.SeasonStart.Value.DayNumber);
        var isNhl = sourceRulebook.LeagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase);
        var buyoutRules = new BuyoutRules
        {
            BuyoutsEnabled = isNhl,
            BuyoutWindowStartOffsetDays = outsideWindow ? 0 : Math.Max(0, offset - 5),
            BuyoutWindowEndOffsetDays = outsideWindow ? 1 : offset + 5,
            BuyoutCostPercentage = isNhl ? 0.6667m : 0m,
            PenaltyYearsMultiplier = isNhl ? 2 : 0,
            AgeBasedCostRulePlaceholder = isNhl ? "Test buyout percentage rule." : "Buyouts disabled for test rulebook.",
            CapPenaltyEnabled = isNhl,
            MinimumContractRemainingYears = isNhl ? 1 : 0
        };
        var rulebook = WithBuyoutRules(sourceRulebook, buyoutRules);
        created = service.CreateScenario(selection with { LeagueProfile = selection.LeagueProfile with { Rulebook = rulebook }, Rulebook = rulebook });
        currentDate = created.ScenarioSnapshot.CurrentDate;
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First(item => item.Position != RosterPosition.Goalie);
        var playerName = created.ScenarioSnapshot.AlphaSnapshot.People.First(person => person.PersonId == player.PersonId).Identity.DisplayName;
        var contract = new Contract(
            $"contract-buyout-test:{player.PersonId}:{Guid.NewGuid():N}",
            player.PersonId,
            created.ScenarioSnapshot.Organization.OrganizationId,
            ContractType.JuniorPlayerAgreement,
            ContractStatus.Signed,
            new ContractTerm(currentDate.AddDays(-120), currentDate.AddYears(3)),
            new ContractMoney(3_000_000m, 500_000m, "USD"),
            Array.Empty<ContractClause>(),
            currentDate.AddDays(-130),
            currentDate.AddDays(-120),
            null,
            null,
            null);
        var contracts = created.ScenarioSnapshot.Contracts
            .Where(item => item.PersonId != player.PersonId)
            .Append(contract)
            .ToArray();
        var alpha = created.ScenarioSnapshot.AlphaSnapshot with
        {
            Contracts = created.ScenarioSnapshot.AlphaSnapshot.Contracts
                .Where(item => item.PersonId != player.PersonId)
                .Append(contract)
                .ToArray(),
            Roster = created.ScenarioSnapshot.AlphaSnapshot.Roster with
            {
                Players = created.ScenarioSnapshot.AlphaSnapshot.Roster.Players
                    .Select(item => item.PersonId == player.PersonId ? item with { Age = 28, Status = RosterStatus.Active } : item)
                    .ToArray()
            }
        };
        var freeAgentMarket = created.ScenarioSnapshot.FreeAgentMarket is null
            ? null
            : created.ScenarioSnapshot.FreeAgentMarket with
            {
                FreeAgents = created.ScenarioSnapshot.FreeAgentMarket.FreeAgents
                    .Where(agent => agent.PersonId != player.PersonId)
                    .ToArray()
            };
        var scenario = created.ScenarioSnapshot with
        {
            AlphaSnapshot = alpha,
            Contracts = contracts,
            FreeAgentMarket = freeAgentMarket,
            ContractBuyouts = Array.Empty<ContractBuyout>(),
            BuyoutHistory = BuyoutHistory.Empty,
            ArbitrationCases = Array.Empty<ArbitrationCase>(),
            ArbitrationHistory = ArbitrationHistory.Empty
        };
        scenario.Validate();
        return new PreparedBuyoutScenario(created.Registry with { Rulebook = rulebook }, scenario, player.PersonId, playerName);
    }

    private static Rulebook WithBuyoutRules(Rulebook source, BuyoutRules buyoutRules) =>
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
            SalaryCapRules = source.SalaryCapRules,
            WaiverRules = source.WaiverRules,
            FreeAgentRightsRules = source.FreeAgentRightsRules,
            ArbitrationRules = source.ArbitrationRules,
            BuyoutRules = buyoutRules
        };

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

        throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed record PreparedBuyoutScenario(EngineRegistry Registry, NewGmScenarioSnapshot Scenario, string PersonId, string PlayerName);
}
