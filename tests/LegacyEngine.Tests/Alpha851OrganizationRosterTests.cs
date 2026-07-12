using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha851OrganizationRosterTests
{
    public void NhlScenarioStartsWithOrganizationGroups()
    {
        var scenario = CreateNhlScenario();
        var allocation = scenario.OrganizationRoster!;

        Assert.Equal(23, allocation.In(OrganizationRosterGroup.NhlActiveRoster).Count);
        Assert.True(allocation.In(OrganizationRosterGroup.AhlAffiliateRoster).Count >= 15, "NHL scenario should start with a believable AHL group.");
        Assert.True(allocation.In(OrganizationRosterGroup.UnsignedProspectRights).Count >= 8, "Established NHL team should begin with unsigned prospect rights.");
        Assert.True(allocation.In(OrganizationRosterGroup.SignedJuniorReturn).Count >= 2, "NHL team should have signed junior returns.");
        Assert.True(allocation.Players.GroupBy(player => player.PersonId).All(group => group.Count() == 1), "A player must have only one organization allocation.");
    }

    public void ContractInventorySeparatesActiveRosterAndSignedContracts()
    {
        var scenario = CreateNhlScenario();
        var inventory = new RosterAllocationService().BuildContractInventory(scenario, RulebookPresets.CreateNhlStyle());

        Assert.Equal(50, inventory.MaximumContracts);
        Assert.True(inventory.NhlContracts == 23, "NHL active players should be counted separately from organization contracts.");
        Assert.True(inventory.AhlContracts >= 15, "AHL-assigned players should count toward signed contracts.");
        Assert.True(inventory.ExemptJuniorReturns > 0, "Configured junior returns should be evaluated as a separate exemption.");
        Assert.True(inventory.ContractsUsed < inventory.MaximumContracts, "New NHL career should leave contract flexibility.");
    }

    public void UnsignedRightsDoNotCountButAhlContractsDo()
    {
        var scenario = CreateNhlScenario();
        var allocation = scenario.OrganizationRoster!;

        Assert.True(allocation.In(OrganizationRosterGroup.UnsignedProspectRights).All(player => !player.CountsTowardContractLimit), "Unsigned prospect rights must not count as signed contracts.");
        Assert.True(allocation.In(OrganizationRosterGroup.AhlAffiliateRoster).All(player => player.CountsTowardContractLimit), "AHL players under NHL contract must count toward the contract inventory.");
    }

    public void JuniorReturnElcSlidesWithoutReplacingContract()
    {
        var scenario = CreateNhlScenario();
        var player = scenario.OrganizationRoster!.In(OrganizationRosterGroup.SignedJuniorReturn)
            .First(player => player.SlideEligibility?.IsEligible == true);
        var before = scenario.Contracts.Single(contract => contract.ContractId == player.ContractId);

        var result = new EntryLevelSlideService().Evaluate(scenario, player.PersonId, RulebookPresets.CreateNhlStyle());

        Assert.True(result.Slid, result.Summary);
        Assert.Equal(before.ContractId, result.UpdatedContract!.ContractId);
        Assert.True(result.UpdatedContract.Term.EndDate > before.Term.EndDate, "Slide should move expiry forward.");
        Assert.True(result.ScenarioSnapshot.ContractSlideHistory.Any(history => history.ContractId == before.ContractId), "Slide history should record the existing contract.");
        Assert.Equal(before.Money.SalaryOrStipend, result.UpdatedContract.Money.SalaryOrStipend);
    }

    public void SaveLoadPreservesAllocationAndSlideHistory()
    {
        var scenario = CreateNhlScenario();
        var player = scenario.OrganizationRoster!.In(OrganizationRosterGroup.SignedJuniorReturn)
            .First(player => player.SlideEligibility?.IsEligible == true);
        var slid = new EntryLevelSlideService().Evaluate(scenario, player.PersonId, RulebookPresets.CreateNhlStyle()).ScenarioSnapshot;
        var file = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha851-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(slid, RulebookPresets.CreateNhlStyle());

        var saved = new SaveGameService().SaveCareer(slid, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, file);
        var loaded = new SaveGameService().LoadFromFile(file, RulebookPresets.CreateNhlStyle());

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(slid.OrganizationRoster!.Players.Count, loaded.SaveGame!.ScenarioSnapshot.OrganizationRoster!.Players.Count);
        Assert.Equal(slid.ContractSlideHistory.Count, loaded.SaveGame.ScenarioSnapshot.ContractSlideHistory.Count);
    }

    public void SlideEvaluationDoesNotDuplicateContractOrHistory()
    {
        var scenario = CreateNhlScenario();
        var player = scenario.OrganizationRoster!.In(OrganizationRosterGroup.SignedJuniorReturn)
            .First(player => player.SlideEligibility?.IsEligible == true);
        var service = new EntryLevelSlideService();
        var first = service.Evaluate(scenario, player.PersonId, RulebookPresets.CreateNhlStyle());
        var second = service.Evaluate(first.ScenarioSnapshot, player.PersonId, RulebookPresets.CreateNhlStyle());

        Assert.Equal(1, second.ScenarioSnapshot.ContractSlideHistory.Count(history => history.ContractId == player.ContractId));
        Assert.Equal(first.UpdatedContract!.Term.EndDate, second.UpdatedContract!.Term.EndDate);
    }

    public void DesktopExposesOrganizationAllocationSurfaces()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("All Contracted Players", StringComparison.Ordinal), "Hockey Operations should expose all contracted players.");
        Assert.True(source.Contains("Unsigned Prospects", StringComparison.Ordinal), "Hockey Operations should expose unsigned rights.");
        Assert.True(source.Contains("Junior Returns", StringComparison.Ordinal), "Hockey Operations should expose junior returns.");
        Assert.True(source.Contains("Contracts", StringComparison.Ordinal), "Hockey Operations should summarize contract inventory.");
    }

    private static NewGmScenarioSnapshot CreateNhlScenario()
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

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
