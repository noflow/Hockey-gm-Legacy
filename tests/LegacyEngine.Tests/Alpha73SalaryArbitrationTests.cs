using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha73SalaryArbitrationTests
{
    public void EligibleRfaCanFileArbitration()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var result = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(ArbitrationCaseStatus.HearingScheduled, result.Case!.Status);
    }

    public void IneligiblePlayerCannotFileArbitration()
    {
        var prepared = PrepareQualifiedScenario(age: 21, seasons: 2);
        var result = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.False(result.Success, "Player below rulebook thresholds should not be able to file arbitration.");
    }

    public void RulebookControlsEligibility()
    {
        var arbitrationRules = new ArbitrationRules
        {
            ArbitrationEnabled = true,
            EligibilityAge = 20,
            AccruedSeasonsThreshold = 1,
            FilingWindowDaysAfterQualifyingOffer = 5,
            HearingStartDaysAfterFiling = 10,
            HearingEndDaysAfterFiling = 20,
            WalkAwayAllowed = true,
            MinimumAward = 775_000m,
            MaximumAward = 5_000_000m
        };
        var prepared = PrepareQualifiedScenario(age: 21, seasons: 2, arbitrationRules: arbitrationRules);
        var eligibility = new ArbitrationService().BuildEligibility(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(ArbitrationEligibilityStatus.Eligible, eligibility.Status);
        Assert.Equal(prepared.Scenario.CurrentDate.AddDays(5), eligibility.FilingDeadline);
    }

    public void ArbitrationCaseCreated()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var scenario = new ArbitrationService().EnsureArbitration(prepared.Scenario, prepared.Registry.Rulebook);

        Assert.True(scenario.ArbitrationCases.Any(item => item.PersonId == prepared.PersonId && item.Status == ArbitrationCaseStatus.Eligible), "Eligible qualified RFA should create an arbitration case.");
    }

    public void HearingDateAssigned()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var result = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.Equal(prepared.Scenario.CurrentDate.AddDays(prepared.Registry.Rulebook!.ArbitrationRules!.HearingStartDaysAfterFiling), result.Case!.HearingDate);
    }

    public void AwardEstimateGenerated()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var scenario = new ArbitrationService().EnsureArbitration(prepared.Scenario, prepared.Registry.Rulebook);
        var arbitrationCase = scenario.ArbitrationCases.Single(item => item.PersonId == prepared.PersonId);

        Assert.True(arbitrationCase.Award is not null, "Arbitration case should include award estimate.");
        Assert.True(arbitrationCase.Award!.PlayerAsk >= arbitrationCase.Award.TeamOffer, "Player ask should be at least the team offer.");
        Assert.True(arbitrationCase.Award.FinalAward > 0, "Projected final award should be positive.");
    }

    public void SettlementResolvesCase()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var filed = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var settled = new ArbitrationService().NegotiateSettlement(prepared.Registry, filed.ScenarioSnapshot, prepared.PersonId);

        Assert.True(settled.Success, settled.Message);
        Assert.Equal(ArbitrationCaseStatus.SettledBeforeHearing, settled.Case!.Status);
        Assert.True(settled.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.PersonId && contract.Status == ContractStatus.Signed), "Settlement should create signed contract.");
    }

    public void AcceptedAwardCreatesContract()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var filed = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var accepted = new ArbitrationService().AcceptAward(prepared.Registry, filed.ScenarioSnapshot, prepared.PersonId);

        Assert.True(accepted.Success, accepted.Message);
        Assert.Equal(ArbitrationCaseStatus.Accepted, accepted.Case!.Status);
        Assert.True(accepted.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.PersonId && contract.Status == ContractStatus.Signed), "Accepted award should create a signed contract.");
    }

    public void WalkAwayReleasesPlayerWhenAllowed()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var filed = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var walked = new ArbitrationService().WalkAway(prepared.Registry, filed.ScenarioSnapshot, prepared.PersonId);

        Assert.True(walked.Success, walked.Message);
        Assert.Equal(ArbitrationCaseStatus.WalkedAway, walked.Case!.Status);
        Assert.True(walked.ScenarioSnapshot.FreeAgentMarket!.Find(prepared.PersonId) is not null, "Walk-away should release player into the market where allowed.");
    }

    public void ActionCenterShowsArbitrationDeadlines()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var scenario = new ArbitrationService().EnsureArbitration(prepared.Scenario, prepared.Registry.Rulebook);
        var budget = new BudgetOverviewService().Build(scenario, prepared.Registry.Rulebook!);
        var readiness = new SeasonReadinessService().Evaluate(prepared.Registry, scenario);
        var items = new ActionCenterService().BuildItems(scenario, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Contracts && item.Title.Contains("Arbitration", StringComparison.Ordinal)), "Action Center should expose arbitration deadline/review.");
    }

    public void DossierAndHistoryRecordArbitration()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var filed = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var dossier = new PlayerDossierService().CreateDossier(filed.ScenarioSnapshot, prepared.PersonId);
        var lines = dossier.Sections.Single(section => section.Title == "Contract / Rights Status").Lines;

        Assert.True(filed.ScenarioSnapshot.ArbitrationHistory.ForPlayer(prepared.PersonId).Count > 0, "Arbitration history should record filing.");
        Assert.True(lines.Any(line => line.Contains("Arbitration", StringComparison.Ordinal)), "Dossier should show arbitration context.");
    }

    public void SaveLoadPreservesActiveCase()
    {
        var prepared = PrepareQualifiedScenario(age: 24, seasons: 5);
        var filed = new ArbitrationService().FileTeamArbitration(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha73-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(filed.ScenarioSnapshot, prepared.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(filed.ScenarioSnapshot, Array.Empty<InboxMessage>(), filed.LeagueTransactions, new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, prepared.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.ArbitrationCases.Any(item => item.PersonId == prepared.PersonId && item.HearingDate is not null), "Active arbitration case should survive save/load.");
    }

    public void AlphaDesktopExposesArbitrationUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Salary Arbitration", StringComparison.Ordinal), "Desktop should expose salary arbitration section.");
        Assert.True(source.Contains("File Arbitration", StringComparison.Ordinal), "Desktop should expose file arbitration action.");
        Assert.True(source.Contains("Accept Award", StringComparison.Ordinal), "Desktop should expose accept award action.");
        Assert.True(source.Contains("Walk Away", StringComparison.Ordinal), "Desktop should expose walk-away action.");
    }

    public void NoOfferSheetsOrGodotAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Arbitration*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.False(text.Contains("OfferSheet", StringComparison.OrdinalIgnoreCase), "Alpha 7.3 should not implement offer-sheet systems.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 7.3 should not add Godot.");
    }

    private static PreparedArbitrationScenario PrepareQualifiedScenario(int age, int seasons, ArbitrationRules? arbitrationRules = null)
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Nhl).First();
        var sourceRulebook = RulebookPresets.CreateNhlStyle();
        var rulebook = WithArbitrationRules(sourceRulebook, arbitrationRules ?? sourceRulebook.ArbitrationRules!);
        var selection = service.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId);
        var created = service.CreateScenario(selection with { LeagueProfile = selection.LeagueProfile with { Rulebook = rulebook }, Rulebook = rulebook });
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First(item => item.Position != RosterPosition.Goalie);
        var currentDate = created.ScenarioSnapshot.CurrentDate;
        var updatedPeople = created.ScenarioSnapshot.AlphaSnapshot.People
            .Select(person => person.PersonId == player.PersonId
                ? person with { Identity = person.Identity with { BirthDate = currentDate.AddYears(-age) } }
                : person)
            .ToArray();
        var updatedPlayers = created.ScenarioSnapshot.AlphaSnapshot.Players
            .Select(person => person.PersonId == player.PersonId
                ? person with { Identity = person.Identity with { BirthDate = currentDate.AddYears(-age) } }
                : person)
            .ToArray();
        var name = updatedPeople.First(person => person.PersonId == player.PersonId).Identity.DisplayName;
        var contract = new Contract(
            $"contract-arbitration-test:{player.PersonId}:{Guid.NewGuid():N}",
            player.PersonId,
            created.ScenarioSnapshot.Organization.OrganizationId,
            ContractType.JuniorPlayerAgreement,
            ContractStatus.Signed,
            new ContractTerm(currentDate.AddDays(-360), currentDate.AddDays(5)),
            new ContractMoney(1_250_000m),
            Array.Empty<ContractClause>(),
            currentDate.AddDays(-360),
            currentDate.AddDays(-350),
            null,
            null,
            null);
        var roster = created.ScenarioSnapshot.AlphaSnapshot.Roster with
        {
            Players = created.ScenarioSnapshot.AlphaSnapshot.Roster.Players
                .Select(item => item.PersonId == player.PersonId ? item with { Age = age, Status = RosterStatus.Active } : item)
                .ToArray()
        };
        var contracts = created.ScenarioSnapshot.Contracts
            .Where(item => item.PersonId != player.PersonId)
            .Append(contract)
            .ToArray();
        var career = created.ScenarioSnapshot.CareerStatSummaries
            .Where(item => item.PersonId != player.PersonId)
            .Append(new CareerStatSummary(player.PersonId, name, player.Position, seasons, Math.Max(1, seasons) * 60, Goals: seasons * 12, Assists: seasons * 18, PrimaryLeague: "NHL"))
            .ToArray();
        var prior = created.ScenarioSnapshot.PriorSeasonStats
            .Where(item => item.PersonId != player.PersonId)
            .Append(new PriorSeasonStatLine(player.PersonId, name, currentDate.Year - 1, created.ScenarioSnapshot.Organization.Name, "NHL", player.Position, 74, Goals: 18, Assists: 24, PlusMinus: 6, PenaltyMinutes: 20))
            .ToArray();
        var freeAgentMarket = created.ScenarioSnapshot.FreeAgentMarket is null
            ? null
            : created.ScenarioSnapshot.FreeAgentMarket with
            {
                FreeAgents = created.ScenarioSnapshot.FreeAgentMarket.FreeAgents
                    .Where(agent => agent.PersonId != player.PersonId)
                    .ToArray()
            };
        var alpha = created.ScenarioSnapshot.AlphaSnapshot with
        {
            Roster = roster,
            Contracts = contracts,
            People = updatedPeople,
            Players = updatedPlayers
        };
        var scenario = created.ScenarioSnapshot with
        {
            AlphaSnapshot = alpha,
            Contracts = contracts,
            CareerStatSummaries = career,
            PriorSeasonStats = prior,
            FreeAgentMarket = freeAgentMarket,
            PlayerRightsDecisions = Array.Empty<PlayerRightsDecision>(),
            RightsHistory = RightsHistory.Empty,
            ArbitrationCases = Array.Empty<ArbitrationCase>(),
            ArbitrationHistory = ArbitrationHistory.Empty
        };
        var qualified = new RfaUfaService().IssueQualifyingOffer(created.Registry with { Rulebook = rulebook }, scenario, player.PersonId);

        return new PreparedArbitrationScenario(created.Registry with { Rulebook = rulebook }, qualified.ScenarioSnapshot, player.PersonId);
    }

    private static Rulebook WithArbitrationRules(Rulebook source, ArbitrationRules arbitrationRules) =>
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
            ArbitrationRules = arbitrationRules
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

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record PreparedArbitrationScenario(EngineRegistry Registry, NewGmScenarioSnapshot Scenario, string PersonId);
}
