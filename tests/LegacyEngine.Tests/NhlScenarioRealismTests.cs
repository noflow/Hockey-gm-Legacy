using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Rosters;

internal sealed class NhlScenarioRealismTests
{
    public void DashboardViewAllActionsTargetsActionCenter()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("View All Actions", StringComparison.Ordinal), "Dashboard should include View All Actions.");
        Assert.True(source.Contains("SelectWorkspaceScreen(\"Dashboard\", \"Action Center / Pending Decisions\")", StringComparison.Ordinal), "View All Actions should open the Action Center sub-screen.");
    }

    public void NhlRosterHasVeteransAndYoungPlayers()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var ages = scenario.AlphaSnapshot.Roster.Players
            .Select(player => scenario.AlphaSnapshot.People.First(person => person.PersonId == player.PersonId).CalculateAge(scenario.CurrentDate))
            .ToArray();

        Assert.True(ages.Any(age => age >= 30), "NHL roster should include older veterans.");
        Assert.True(ages.Any(age => age <= 21), "NHL roster should include young roster players/rookies.");
        Assert.True(ages.Max() > 21, "Oldest NHL roster player should not be only 21.");
    }

    public void NhlRosterDoesNotApplyJuniorOverageLimit()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var validation = new RosterEngine().ValidateRoster(
            scenario.AlphaSnapshot.Roster,
            new RosterRuleValidator(RulebookPresets.CreateNhlStyle()));

        Assert.True(validation.IsValid, validation.Message);
        Assert.Equal(0, RulebookPresets.CreateNhlStyle().RosterRules!.OverageSlots);
    }

    public void NhlScenarioStartsWithProspects()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;

        Assert.True(scenario.ProspectRights.Count >= 8, "Established NHL team should start with a prospect pool.");
        Assert.True(scenario.PlayerPipeline.Count(record => record.PipelineStatus is PlayerPipelineStatus.DraftedRightsHeld or PlayerPipelineStatus.UnsignedProspect or PlayerPipelineStatus.SignedProspect) >= 8, "Prospects should appear in the player pipeline.");
    }

    public void NhlDraftAgeDistributionUsesSeventeenToTwenty()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var ages = scenario.AlphaSnapshot.DraftBoard.Entries
            .Select(entry => scenario.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId).CalculateAge(scenario.CurrentDate))
            .ToArray();

        Assert.True(ages.All(age => age is >= 17 and <= 20), "NHL draft board should show prospects aged 17-20.");
        Assert.True(ages.Count(age => age is 18 or 19) > ages.Count(age => age is 17 or 20), "Most NHL draft prospects should be 18-19.");
        Assert.True(ages.Count(age => age == 17) < ages.Length / 4, "Age-17 prospects should be uncommon.");
        Assert.True(ages.Count(age => age == 20) < ages.Length / 4, "Age-20 prospects should be uncommon.");
    }

    public void NhlRosterContractsUseProfessionalScale()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var rosterIds = scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal);
        var rosterContracts = scenario.Contracts.Where(contract => rosterIds.Contains(contract.PersonId)).ToArray();

        Assert.True(rosterContracts.Any(contract => contract.Money.SalaryOrStipend >= 1_000_000m), "NHL roster should include million-dollar veteran contracts.");
        Assert.True(rosterContracts.All(contract => contract.Money.SalaryOrStipend >= 750_000m), "NHL roster contracts should not use junior stipend scale.");
    }

    public void NhlProspectAskUsesEntryLevelScale()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var firstRounder = scenario.ProspectRights.First(prospect => prospect.RoundNumber == 1);
        var ask = new ContractManagementService().BuildAsk(scenario, ContractAskType.Prospect, firstRounder.ProspectPersonId);

        Assert.True(ask.RequestedSalary >= 900_000m, "First-round NHL prospect ELC ask should be near NHL entry-level scale.");
        Assert.True(ask.RequestedTermYears is >= 1 and <= 3, "NHL ELC term should be age-based and simple for v1.");
    }

    private static NewGmScenarioResult CreateNhlScenario()
    {
        var service = new MultiLeagueCareerService();
        var selection = service.SelectLeagueAndTeam(LeagueExperience.Nhl, "org-seattle-cascades");
        return service.CreateScenario(selection);
    }

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
