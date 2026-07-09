using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha72RfaUfaContractRightsTests
{
    public void ExpiringYoungPlayerBecomesPendingRfa()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var decision = new RfaUfaService().BuildRightsDecisions(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(FreeAgentRightsStatus.PendingRfa, decision.RightsStatus);
        Assert.True(decision.QualifyingOfferRequired, "Young expiring NHL-style player should require a qualifying-offer decision.");
    }

    public void ExpiringOlderPlayerBecomesPendingUfa()
    {
        var prepared = PrepareScenario(age: 29, seasons: 8);
        var decision = new RfaUfaService().BuildRightsDecisions(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(FreeAgentRightsStatus.PendingUfa, decision.RightsStatus);
        Assert.False(decision.QualifyingOfferRequired, "UFA player should not require a qualifying offer.");
    }

    public void RulebookControlsThresholds()
    {
        var rules = new FreeAgentRightsRules
        {
            RfaUfaSystemEnabled = true,
            UfaAge = 21,
            UfaAccruedSeasonsThreshold = 3,
            QualifyingOfferRequired = true,
            QualifyingOfferDeadlineDaysAfterExpiry = 7,
            QualifyingOfferSalaryMultiplier = 1.05m,
            MinimumQualifyingOffer = 775_000m,
            RightsExpiryRule = "qualify_by_deadline_or_release",
            ContractTenderWindowDays = 30
        };
        var prepared = PrepareScenario(age: 22, seasons: 2, overrideRules: rules);
        var decision = new RfaUfaService().BuildRightsDecisions(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(FreeAgentRightsStatus.PendingUfa, decision.RightsStatus);
    }

    public void QualifyingOfferPreservesRights()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var result = new RfaUfaService().IssueQualifyingOffer(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var decision = result.ScenarioSnapshot.PlayerRightsDecisions.Single(item => item.PersonId == prepared.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(FreeAgentRightsStatus.Qualified, decision.RightsStatus);
        Assert.Equal(prepared.Scenario.Organization.OrganizationId, decision.RightsHolderOrganizationId);
        Assert.True(decision.QualifyingOffer?.IsIssued == true, "Qualifying offer should be marked issued.");
    }

    public void DecliningQualifyingOfferReleasesRights()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var result = new RfaUfaService().DeclineQualifyingOffer(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var decision = result.ScenarioSnapshot.PlayerRightsDecisions.Single(item => item.PersonId == prepared.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(FreeAgentRightsStatus.NotQualified, decision.RightsStatus);
        Assert.Equal(ContractRightsStatus.RightsReleased, decision.ContractRightsStatus);
        Assert.Equal("none", decision.RightsHolderOrganizationId);
    }

    public void UfaEntersFreeAgentMarket()
    {
        var prepared = PrepareScenario(age: 29, seasons: 8);
        var result = new RfaUfaService().DeclineQualifyingOffer(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(FreeAgentRightsStatus.UnrestrictedFreeAgent, result.Decision!.RightsStatus);
        Assert.True(result.ScenarioSnapshot.FreeAgentMarket!.Find(prepared.PersonId) is not null, "UFA should enter the free-agent market.");
    }

    public void RfaRemainsTiedToRightsHolder()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var result = new RfaUfaService().IssueQualifyingOffer(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var decision = result.ScenarioSnapshot.PlayerRightsDecisions.Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(FreeAgentRightsStatus.Qualified, decision.RightsStatus);
        Assert.Equal(prepared.Scenario.Organization.Name, decision.RightsHolderTeamName);
        Assert.False(result.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.Any(agent => agent.PersonId == prepared.PersonId), "Qualified RFA should not become a UFA market entry.");
    }

    public void ContractScreenExposesRights()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Contract Rights / Expiring Contracts", StringComparison.Ordinal), "Desktop should expose a Contract Rights view.");
        Assert.True(source.Contains("Qualify", StringComparison.Ordinal), "Desktop should expose Qualify action.");
        Assert.True(source.Contains("Do Not Qualify", StringComparison.Ordinal), "Desktop should expose Do Not Qualify action.");
    }

    public void PlayerDossierExposesRfaUfaStatus()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var dossier = new PlayerDossierService().CreateDossier(prepared.Scenario, prepared.PersonId);
        var contractSection = dossier.Sections.Single(section => section.Title == "Contract / Rights Status");

        Assert.True(contractSection.Lines.Any(line => line.Contains("RFA/UFA status", StringComparison.Ordinal)), "Dossier should expose RFA/UFA status.");
        Assert.True(contractSection.Lines.Any(line => line.Contains("Qualifying offer", StringComparison.Ordinal)), "Dossier should expose qualifying-offer context.");
    }

    public void ActionCenterWarnsBeforeQualifyingDeadline()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var scenario = new RfaUfaService().EnsureRights(prepared.Scenario, prepared.Registry.Rulebook);
        var budget = new BudgetOverviewService().Build(scenario, prepared.Registry.Rulebook!);
        var readiness = new SeasonReadinessService().Evaluate(prepared.Registry, scenario);
        var items = new ActionCenterService().BuildItems(scenario, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Contracts && item.RelatedPersonId == prepared.PersonId), "Action Center should warn before the qualifying deadline.");
    }

    public void LeagueNewsRecordsNotQualifiedPlayer()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var result = new RfaUfaService().DeclineQualifyingOffer(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.True(result.LeagueTransactions.Any(transaction => transaction.TransactionType == LeagueTransactionType.RfaNotQualified), "Not-qualified player should create league news.");
    }

    public void SaveLoadPreservesRightsStatus()
    {
        var prepared = PrepareScenario(age: 23, seasons: 3);
        var result = new RfaUfaService().IssueQualifyingOffer(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha72-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(result.ScenarioSnapshot, prepared.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(result.ScenarioSnapshot, Array.Empty<InboxMessage>(), result.LeagueTransactions, new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, prepared.Registry.Rulebook);
        var decision = loaded.SaveGame!.ScenarioSnapshot.PlayerRightsDecisions.Single(item => item.PersonId == prepared.PersonId);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(FreeAgentRightsStatus.Qualified, decision.RightsStatus);
        Assert.True(loaded.SaveGame.ScenarioSnapshot.RightsHistory.ForPlayer(prepared.PersonId).Count > 0, "Rights history should survive save/load.");
    }

    public void NoOfferSheetsArbitrationOrGodotAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "RfaUfa*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.False(text.Contains("OfferSheet", StringComparison.OrdinalIgnoreCase), "Alpha 7.2 should not implement offer sheets.");
        Assert.False(text.Contains("Arbitration", StringComparison.OrdinalIgnoreCase), "Alpha 7.2 should not implement arbitration.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 7.2 should not add Godot.");
    }

    private static PreparedRightsScenario PrepareScenario(int age, int seasons, int daysUntilExpiry = 5, FreeAgentRightsRules? overrideRules = null)
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Nhl).First();
        var rulebook = overrideRules is null
            ? RulebookPresets.CreateNhlStyle()
            : WithRightsRules(RulebookPresets.CreateNhlStyle(), overrideRules);
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
            $"contract-rights-test:{player.PersonId}:{Guid.NewGuid():N}",
            player.PersonId,
            created.ScenarioSnapshot.Organization.OrganizationId,
            ContractType.JuniorPlayerAgreement,
            ContractStatus.Signed,
            new ContractTerm(currentDate.AddDays(-360), currentDate.AddDays(daysUntilExpiry)),
            new ContractMoney(950_000m),
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
            .Append(new CareerStatSummary(player.PersonId, name, player.Position, seasons, Math.Max(1, seasons) * 50, PrimaryLeague: "NHL"))
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
            FreeAgentMarket = freeAgentMarket,
            PlayerRightsDecisions = Array.Empty<PlayerRightsDecision>(),
            RightsHistory = RightsHistory.Empty
        };

        return new PreparedRightsScenario(created.Registry with { Rulebook = rulebook }, scenario, player.PersonId);
    }

    private static Rulebook WithRightsRules(Rulebook source, FreeAgentRightsRules rightsRules) =>
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
            FreeAgentRightsRules = rightsRules
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

    private sealed record PreparedRightsScenario(EngineRegistry Registry, NewGmScenarioSnapshot Scenario, string PersonId);
}
