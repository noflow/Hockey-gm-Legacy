using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha75OfferSheetTests
{
    public void EligibleRfaCanReceiveOfferSheet()
    {
        var prepared = PrepareQualifiedScenario();
        var eligibility = new OfferSheetService().BuildEligibility(prepared.Scenario, prepared.Registry.Rulebook).Single(item => item.PersonId == prepared.PersonId);

        Assert.Equal(OfferSheetStatus.Eligible, eligibility.Status);
        Assert.True(prepared.Registry.Rulebook!.OfferSheetRules!.OfferSheetsEnabled, "NHL-style rulebook should enable offer sheets.");
    }

    public void IneligiblePlayerCannotReceiveOfferSheet()
    {
        var prepared = PrepareQualifiedScenario(rulebookOverride: RulebookPresets.CreateJuniorMajor());
        var eligibility = new OfferSheetService().EvaluateEligibility(prepared.Scenario, prepared.Registry.Rulebook, prepared.PersonId);

        Assert.False(eligibility.IsEligible, "Junior-style rulebook should not allow offer sheets.");
        Assert.True(eligibility.Reason.Contains("disabled", StringComparison.OrdinalIgnoreCase) || eligibility.Reason.Contains("No RFA", StringComparison.OrdinalIgnoreCase), "Ineligible reason should be readable.");
    }

    public void RulebookControlsOfferSheetEnablement()
    {
        var rules = new OfferSheetRules { OfferSheetsEnabled = false };
        var prepared = PrepareQualifiedScenario(offerSheetRules: rules);
        var eligibility = new OfferSheetService().EvaluateEligibility(prepared.Scenario, prepared.Registry.Rulebook, prepared.PersonId);

        Assert.Equal(OfferSheetStatus.NotEligible, eligibility.Status);
    }

    public void CompensationCalculatedFromAav()
    {
        var prepared = PrepareQualifiedScenario();
        var compensation = new OfferSheetService().CalculateCompensation(prepared.Scenario, prepared.Registry.Rulebook, "org-rival", "Rival Club", 5_500_000m, 3);

        Assert.True(compensation.RequiredRounds.Contains(1), "AAV tier should require a first-round pick.");
        Assert.True(compensation.RequiredRounds.Contains(3), "AAV tier should require a third-round pick.");
        Assert.True(compensation.Summary.Contains("requires", StringComparison.OrdinalIgnoreCase), "Compensation summary should be readable.");
    }

    public void MissingRequiredPicksBlocksSubmission()
    {
        var prepared = PrepareQualifiedScenario();
        var result = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 5_500_000m, 3, ownedCompensationRounds: Array.Empty<int>());

        Assert.False(result.Success, "Offer sheet should be blocked when required compensation picks are missing.");
        Assert.True(result.Message.Contains("missing", StringComparison.OrdinalIgnoreCase), "Missing pick message should be explicit.");
    }

    public void CapValidationBlocksImpossibleOffer()
    {
        var prepared = PrepareQualifiedScenario();
        var result = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 150_000_000m, 3, ownedCompensationRounds: new[] { 1, 2, 3 });

        Assert.False(result.Success, "Offer sheet should be blocked when cap validation fails.");
        Assert.True(result.Message.Contains("cap", StringComparison.OrdinalIgnoreCase), "Cap block should explain cap pressure.");
    }

    public void AcceptedOfferSheetCreatesRightsHolderDecision()
    {
        var prepared = PrepareQualifiedScenario();
        var result = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 3_500_000m, 2, ownedCompensationRounds: new[] { 2 });

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.OfferSheets.Any(item => item.PersonId == prepared.PersonId && item.IsActive), "Accepted offer sheet should remain active for rights-holder decision.");
        Assert.True(result.InboxItems.Any(item => item.Title.Contains("Offer sheet decision", StringComparison.OrdinalIgnoreCase)), "Rights holder should get an inbox decision.");
    }

    public void MatchOfferKeepsPlayerAndCreatesContract()
    {
        var prepared = PrepareQualifiedScenario();
        var submitted = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 3_500_000m, 2, ownedCompensationRounds: new[] { 2 });
        var matched = new OfferSheetService().MatchOffer(prepared.Registry, submitted.ScenarioSnapshot, prepared.PersonId);

        Assert.True(matched.Success, matched.Message);
        Assert.True(matched.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.PersonId && contract.OrganizationId == prepared.Scenario.Organization.OrganizationId && contract.Status == ContractStatus.Signed), "Matching should create a signed contract with the rights holder.");
        Assert.True(matched.ScenarioSnapshot.OfferSheets.Any(item => item.PersonId == prepared.PersonId && item.Status == OfferSheetStatus.MatchedByTeam), "Offer sheet should record matched status.");
    }

    public void DeclineMovesPlayerAndRecordsCompensation()
    {
        var prepared = PrepareQualifiedScenario();
        var submitted = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 5_500_000m, 3, "org-rival", "Rival Club", new[] { 1, 3 });
        var declined = new OfferSheetService().DeclineAndTakeCompensation(prepared.Registry, submitted.ScenarioSnapshot, prepared.PersonId);

        Assert.True(declined.Success, declined.Message);
        Assert.True(declined.ScenarioSnapshot.Contracts.Any(contract => contract.PersonId == prepared.PersonId && contract.OrganizationId == "org-rival" && contract.Status == ContractStatus.Signed), "Declining should create the contract with the offering team.");
        Assert.True(declined.OfferSheet!.Compensation.RequiredPicks.Count >= 2, "Compensation picks should be recorded.");
    }

    public void AiOfferSheetsAreRare()
    {
        var prepared = PrepareQualifiedScenario();
        var offers = new OfferSheetService().GenerateAiOfferSheets(prepared.Scenario, maxOffers: 3);

        Assert.True(offers.Count <= 1, "AI offer sheets should be rare in v1.");
    }

    public void ActionCenterShowsUrgentOfferSheet()
    {
        var prepared = PrepareQualifiedScenario();
        var submitted = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 3_500_000m, 2, ownedCompensationRounds: new[] { 2 });
        var budget = new BudgetOverviewService().Build(submitted.ScenarioSnapshot, prepared.Registry.Rulebook!);
        var readiness = new SeasonReadinessService().Evaluate(prepared.Registry, submitted.ScenarioSnapshot);
        var items = new ActionCenterService().BuildItems(submitted.ScenarioSnapshot, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Title.Contains("Offer sheet decision", StringComparison.OrdinalIgnoreCase)), "Action Center should show offer sheet decision.");
    }

    public void DossierExposesOfferSheetState()
    {
        var prepared = PrepareQualifiedScenario();
        var submitted = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 3_500_000m, 2, ownedCompensationRounds: new[] { 2 });
        var dossier = new PlayerDossierService().CreateDossier(submitted.ScenarioSnapshot, prepared.PersonId);
        var lines = dossier.Sections.Single(section => section.Title == "Contract / Rights Status").Lines;

        Assert.True(lines.Any(line => line.Contains("Offer sheet", StringComparison.OrdinalIgnoreCase)), "Dossier should expose offer sheet status.");
    }

    public void SaveLoadPreservesActiveOfferSheet()
    {
        var prepared = PrepareQualifiedScenario();
        var submitted = new OfferSheetService().SubmitOfferSheet(prepared.Registry, prepared.Scenario, prepared.PersonId, 3_500_000m, 2, ownedCompensationRounds: new[] { 2 });
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha75-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(submitted.ScenarioSnapshot, prepared.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(submitted.ScenarioSnapshot, Array.Empty<InboxMessage>(), submitted.LeagueTransactions, new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, prepared.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.OfferSheets.Any(item => item.PersonId == prepared.PersonId && item.IsActive), "Active offer sheet should survive save/load.");
    }

    public void AlphaDesktopExposesOfferSheetUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Offer Sheets", StringComparison.Ordinal), "Desktop should expose offer sheets section.");
        Assert.True(source.Contains("Submit Offer Sheet", StringComparison.Ordinal), "Desktop should expose submit action.");
        Assert.True(source.Contains("Match Offer", StringComparison.Ordinal), "Desktop should expose match action.");
        Assert.True(source.Contains("Take Compensation", StringComparison.Ordinal), "Desktop should expose decline/take compensation action.");
    }

    public void NoFullCbaOfferSheetEdgesOrGodotAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "OfferSheet*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.False(text.Contains("FrontLoaded", StringComparison.OrdinalIgnoreCase), "Alpha 7.5 should not implement front-loaded offer-sheet edge cases.");
        Assert.False(text.Contains("RealNhlFormula", StringComparison.OrdinalIgnoreCase), "Alpha 7.5 should not implement real NHL offer-sheet formulas.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 7.5 should not add Godot.");
    }

    private static PreparedOfferSheetScenario PrepareQualifiedScenario(Rulebook? rulebookOverride = null, OfferSheetRules? offerSheetRules = null)
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Nhl).First();
        var sourceRulebook = rulebookOverride ?? RulebookPresets.CreateNhlStyle();
        var rulebook = WithOfferSheetRules(sourceRulebook, offerSheetRules ?? sourceRulebook.OfferSheetRules ?? new OfferSheetRules { OfferSheetsEnabled = sourceRulebook.LeagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase) });
        var selection = service.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId);
        var created = service.CreateScenario(selection with { LeagueProfile = selection.LeagueProfile with { Rulebook = rulebook }, Rulebook = rulebook });
        var currentDate = created.ScenarioSnapshot.CurrentDate;
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First(item => item.Position != RosterPosition.Goalie);
        var playerName = created.ScenarioSnapshot.AlphaSnapshot.People.First(person => person.PersonId == player.PersonId).Identity.DisplayName;
        var birthDate = currentDate.AddYears(-23);
        var contract = new Contract(
            $"contract-offer-sheet-test:{player.PersonId}:{Guid.NewGuid():N}",
            player.PersonId,
            created.ScenarioSnapshot.Organization.OrganizationId,
            ContractType.JuniorPlayerAgreement,
            ContractStatus.Expired,
            new ContractTerm(currentDate.AddYears(-1), currentDate),
            new ContractMoney(1_250_000m, 0m, "USD"),
            Array.Empty<ContractClause>(),
            currentDate.AddYears(-1),
            currentDate.AddYears(-1),
            null,
            null,
            currentDate);
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
            People = created.ScenarioSnapshot.AlphaSnapshot.People
                .Select(person => person.PersonId == player.PersonId ? person with { Identity = person.Identity with { BirthDate = birthDate } } : person)
                .ToArray(),
            Players = created.ScenarioSnapshot.AlphaSnapshot.Players
                .Select(person => person.PersonId == player.PersonId ? person with { Identity = person.Identity with { BirthDate = birthDate } } : person)
                .ToArray(),
            Roster = created.ScenarioSnapshot.AlphaSnapshot.Roster with
            {
                Players = created.ScenarioSnapshot.AlphaSnapshot.Roster.Players
                    .Select(item => item.PersonId == player.PersonId ? item with { Age = 23, Status = RosterStatus.Reserve } : item)
                    .ToArray()
            }
        };
        var scenario = created.ScenarioSnapshot with
        {
            AlphaSnapshot = alpha,
            Contracts = contracts,
            CareerStatSummaries = created.ScenarioSnapshot.CareerStatSummaries
                .Where(summary => summary.PersonId != player.PersonId)
                .Append(new CareerStatSummary(player.PersonId, playerName, player.Position, 3, 180, Goals: 24, Assists: 38, PrimaryLeague: "NHL"))
                .ToArray(),
            PlayerRightsDecisions = Array.Empty<PlayerRightsDecision>(),
            RightsHistory = RightsHistory.Empty,
            OfferSheets = Array.Empty<OfferSheet>(),
            OfferSheetHistory = OfferSheetHistory.Empty,
            ArbitrationCases = Array.Empty<ArbitrationCase>()
        };
        scenario = new RfaUfaService().EnsureRights(scenario, rulebook);
        var qualified = new RfaUfaService().IssueQualifyingOffer(created.Registry with { Rulebook = rulebook }, scenario, player.PersonId);
        var finalScenario = qualified.Success ? qualified.ScenarioSnapshot : scenario;
        finalScenario.Validate();
        return new PreparedOfferSheetScenario(created.Registry with { Rulebook = rulebook }, finalScenario, player.PersonId);
    }

    private static Rulebook WithOfferSheetRules(Rulebook source, OfferSheetRules offerSheetRules) =>
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
            BuyoutRules = source.BuyoutRules,
            OfferSheetRules = offerSheetRules
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

    private sealed record PreparedOfferSheetScenario(EngineRegistry Registry, NewGmScenarioSnapshot Scenario, string PersonId);
}
