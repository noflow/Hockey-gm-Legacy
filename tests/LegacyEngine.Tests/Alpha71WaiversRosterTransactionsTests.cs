using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha71WaiversRosterTransactionsTests
{
    public void ProfessionalRulebookEnablesWaivers()
    {
        var nhl = RulebookPresets.CreateNhlStyle();
        var ahl = RulebookPresets.CreateAhlStyle();

        Assert.True(nhl.WaiverRules?.WaiversEnabled == true, "NHL-style rulebooks should enable waivers.");
        Assert.True(ahl.WaiverRules?.WaiversEnabled == true, "AHL-style rulebooks should enable waivers.");
        Assert.Equal("reverse_standings", nhl.WaiverRules!.WaiverOrder);
    }

    public void JuniorRulebookDisablesWaivers()
    {
        var junior = RulebookPresets.CreateJuniorMajor();

        Assert.False(junior.WaiverRules?.WaiversEnabled == true, "Junior-style rulebooks should not use the waiver wire.");
    }

    public void YoungSignedPlayerCanBeWaiverExempt()
    {
        var prepared = PrepareScenario(age: 20, seasons: 1, games: 12);
        var eligibility = new WaiverService().EvaluateEligibility(prepared.Scenario, prepared.PersonId, prepared.Registry.Rulebook);

        Assert.True(eligibility.IsWaiverExempt, eligibility.Reason);
        Assert.False(eligibility.RequiresWaivers, "Young, low-experience pro should be waiver exempt.");
    }

    public void VeteranSignedPlayerRequiresWaivers()
    {
        var prepared = PrepareScenario(age: 27, seasons: 6, games: 260);
        var eligibility = new WaiverService().EvaluateEligibility(prepared.Scenario, prepared.PersonId, prepared.Registry.Rulebook);

        Assert.True(eligibility.RequiresWaivers, eligibility.Reason);
        Assert.True(eligibility.Reason.Contains("requires waivers", StringComparison.OrdinalIgnoreCase), "Eligibility explanation should name the waiver requirement.");
    }

    public void ExemptAssignmentMovesPlayerToAffiliate()
    {
        var prepared = PrepareScenario(age: 20, seasons: 1, games: 12);
        var result = new WaiverService().AssignToAffiliate(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var player = result.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(prepared.PersonId)!;
        var pipeline = result.ScenarioSnapshot.PlayerPipeline.First(record => record.PersonId == prepared.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(RosterStatus.AssignedToAffiliate, player.Status);
        Assert.Equal(PlayerPipelineStatus.AssignedToAhl, pipeline.PipelineStatus);
        Assert.True(result.LeagueTransactions.Any(transaction => transaction.TransactionType == LeagueTransactionType.PlayerAssigned), "Affiliate assignment should feed league news.");
    }

    public void RequiredAssignmentPlacesPlayerOnWaivers()
    {
        var prepared = PrepareScenario(age: 27, seasons: 6, games: 260);
        var result = new WaiverService().AssignToAffiliate(prepared.Registry, prepared.Scenario, prepared.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(WaiverStatus.OnWaivers, result.Transaction?.Status);
        Assert.True(result.ScenarioSnapshot.WaiverWire.OpenTransactions.Any(transaction => transaction.PersonId == prepared.PersonId), "Veteran assignment should open a waiver transaction.");
    }

    public void ClaimMovesPlayerToClaimingOrganization()
    {
        var prepared = PrepareScenario(age: 27, seasons: 6, games: 260);
        var service = new WaiverService();
        var placed = service.PlaceOnWaivers(prepared.Registry, prepared.Scenario, prepared.PersonId).ScenarioSnapshot;
        var claimant = placed.LeagueProfile.Teams.First(team => team.OrganizationId != placed.Organization.OrganizationId);
        var claimed = service.SubmitClaim(placed, prepared.PersonId, claimant.OrganizationId).ScenarioSnapshot;
        var processed = service.ProcessWaivers(prepared.Registry, claimed);
        var pipeline = processed.ScenarioSnapshot.PlayerPipeline.First(record => record.PersonId == prepared.PersonId);

        Assert.True(processed.Success, processed.Message);
        Assert.Equal(claimant.OrganizationId, pipeline.CurrentOrganizationId);
        Assert.True(processed.ScenarioSnapshot.WaiverHistory.ForPlayer(prepared.PersonId).Any(entry => entry.Status == WaiverStatus.Claimed), "Claim should be recorded in waiver history.");
    }

    public void ClearingWaiversAssignsToAffiliate()
    {
        var prepared = PrepareScenario(age: 27, seasons: 6, games: 260);
        var service = new WaiverService();
        var placed = service.PlaceOnWaivers(prepared.Registry, prepared.Scenario, prepared.PersonId).ScenarioSnapshot;
        var processed = service.ProcessWaivers(prepared.Registry, placed);
        var player = processed.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(prepared.PersonId)!;

        Assert.True(processed.Success, processed.Message);
        Assert.Equal(RosterStatus.AssignedToAffiliate, player.Status);
        Assert.True(processed.ScenarioSnapshot.WaiverHistory.ForPlayer(prepared.PersonId).Any(entry => entry.Status == WaiverStatus.Cleared), "Waiver clear should be recorded.");
    }

    public void RecallFromAffiliateReturnsPlayerToRoster()
    {
        var prepared = PrepareScenario(age: 20, seasons: 1, games: 12);
        var service = new WaiverService();
        var assigned = service.AssignToAffiliate(prepared.Registry, prepared.Scenario, prepared.PersonId).ScenarioSnapshot;
        var recalled = service.RecallFromAffiliate(prepared.Registry, assigned, prepared.PersonId);
        var player = recalled.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(prepared.PersonId)!;

        Assert.True(recalled.Success, recalled.Message);
        Assert.Equal(RosterStatus.Active, player.Status);
        Assert.True(recalled.LeagueTransactions.Any(transaction => transaction.TransactionType == LeagueTransactionType.PlayerRecalled), "Recall should feed transaction wire.");
    }

    public void PlayerDossierShowsWaiverStatus()
    {
        var prepared = PrepareScenario(age: 27, seasons: 6, games: 260);
        var dossier = new PlayerDossierService().CreateDossier(prepared.Scenario, prepared.PersonId);

        Assert.True(dossier.Sections.Any(section => section.Title == "Waivers / Assignments"), "Player dossier should expose waiver status/history.");
    }

    public void SaveLoadPreservesWaiverWireAndHistory()
    {
        var prepared = PrepareScenario(age: 27, seasons: 6, games: 260);
        var result = new WaiverService().PlaceOnWaivers(prepared.Registry, prepared.Scenario, prepared.PersonId);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha71-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(result.ScenarioSnapshot, prepared.Registry.Rulebook ?? RulebookPresets.CreateNhlStyle());
        var saved = new SaveGameService().SaveCareer(result.ScenarioSnapshot, Array.Empty<InboxMessage>(), result.LeagueTransactions, new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, prepared.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.SaveGame!.ScenarioSnapshot.WaiverWire.Transactions.Any(transaction => transaction.PersonId == prepared.PersonId), "Loaded save should preserve waiver wire.");
        Assert.True(loaded.SaveGame.ScenarioSnapshot.WaiverHistory.ForPlayer(prepared.PersonId).Count > 0, "Loaded save should preserve waiver history.");
    }

    public void AlphaDesktopExposesWaiverUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Waiver Wire", StringComparison.Ordinal), "AlphaDesktop should expose a waiver wire screen.");
        Assert.True(source.Contains("Place On Waivers", StringComparison.Ordinal), "Player action panel should expose waiver placement.");
        Assert.True(source.Contains("Assign Affiliate", StringComparison.Ordinal), "Player action panel should expose affiliate assignment.");
        Assert.True(source.Contains("Waiver status", StringComparison.Ordinal), "Player detail should show waiver status.");
    }

    public void NoForbiddenWaiverSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Waiver*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.False(text.Contains("LTIR", StringComparison.OrdinalIgnoreCase), "Alpha 7.1 should not implement LTIR.");
        Assert.False(text.Contains("EmergencyRecall", StringComparison.OrdinalIgnoreCase), "Alpha 7.1 should not implement emergency recalls.");
        Assert.False(text.Contains("ConditionalWaiver", StringComparison.OrdinalIgnoreCase), "Alpha 7.1 should not implement conditional waivers.");
        Assert.False(text.Contains("Buyout", StringComparison.OrdinalIgnoreCase), "Alpha 7.1 should not implement buyouts.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 7.1 should not add Godot.");
    }

    private static PreparedWaiverScenario PrepareScenario(int age, int seasons, int games)
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Nhl).First();
        var created = service.CreateScenario(service.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId));
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First(item => item.Position != RosterPosition.Goalie);
        var name = created.ScenarioSnapshot.AlphaSnapshot.People.First(person => person.PersonId == player.PersonId).Identity.DisplayName;
        var contract = new Contract(
            $"contract-waiver-test:{player.PersonId}:{Guid.NewGuid():N}",
            player.PersonId,
            created.ScenarioSnapshot.Organization.OrganizationId,
            ContractType.JuniorPlayerAgreement,
            ContractStatus.Signed,
            ContractExpiryCalendar.TermForYears(created.ScenarioSnapshot.CurrentDate, created.ScenarioSnapshot.Season.Settings, 1),
            new ContractMoney(950_000m),
            Array.Empty<ContractClause>(),
            created.ScenarioSnapshot.CurrentDate,
            created.ScenarioSnapshot.CurrentDate,
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
            .Append(new CareerStatSummary(player.PersonId, name, player.Position, seasons, games, PrimaryLeague: "NHL"))
            .ToArray();
        var pipeline = created.ScenarioSnapshot.PlayerPipeline.Any(record => record.PersonId == player.PersonId)
            ? created.ScenarioSnapshot.PlayerPipeline.Select(record => record.PersonId == player.PersonId
                ? record with
                {
                    CurrentOrganizationId = created.ScenarioSnapshot.Organization.OrganizationId,
                    CurrentTeamName = created.ScenarioSnapshot.Organization.Name,
                    CurrentLevel = "NHL",
                    PipelineStatus = PlayerPipelineStatus.NhlRoster,
                    AssignmentStatus = PlayerAssignmentStatus.NhlRoster,
                    IsSigned = true,
                    IsAhlEligible = true
                }
                : record).ToArray()
            : created.ScenarioSnapshot.PlayerPipeline.Append(new PlayerPipelineRecord(
                player.PersonId,
                name,
                created.ScenarioSnapshot.Organization.OrganizationId,
                created.ScenarioSnapshot.Organization.Name,
                "NHL",
                created.ScenarioSnapshot.Organization.OrganizationId,
                created.ScenarioSnapshot.Organization.Name,
                null,
                null,
                PlayerPipelineStatus.NhlRoster,
                PlayerAssignmentStatus.NhlRoster,
                Array.Empty<string>(),
                IsSigned: true,
                IsAhlEligible: true)).ToArray();
        var alpha = created.ScenarioSnapshot.AlphaSnapshot with { Roster = roster, Contracts = contracts };
        var scenario = created.ScenarioSnapshot with
        {
            AlphaSnapshot = alpha,
            Contracts = contracts,
            CareerStatSummaries = career,
            PlayerPipeline = pipeline
        };

        return new PreparedWaiverScenario(created.Registry, scenario, player.PersonId);
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

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record PreparedWaiverScenario(EngineRegistry Registry, NewGmScenarioSnapshot Scenario, string PersonId);
}
