using LegacyEngine.Integration;

internal sealed class Alpha30ExistingWorldHistoryTests
{
    public void NewGmScenarioRosterPlayersHavePriorStats()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var rosterIds = scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal);

        Assert.True(rosterIds.Count > 0, "Scenario should have roster players.");
        Assert.True(rosterIds.All(id => scenario.PriorSeasonStats.Any(stat => stat.PersonId == id)), "Every roster player should have a prior stat line.");
    }

    public void OlderPlayersHaveMultiYearStats()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var olderIds = scenario.AlphaSnapshot.Roster.Players
            .Where(player => (player.Age ?? 0) >= 20)
            .Select(player => player.PersonId)
            .ToArray();

        Assert.True(olderIds.Length > 0, "Scenario should include older roster players.");
        Assert.True(olderIds.All(id => scenario.CareerStatSummaries.Any(summary => summary.PersonId == id && summary.Seasons >= 3)), "Older players should have multi-year career summaries.");
    }

    public void ProspectsHavePriorYouthStats()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var prospectIds = scenario.AlphaSnapshot.DraftBoard.Entries.Take(20).Select(entry => entry.ProspectPersonId).ToArray();

        Assert.True(prospectIds.All(id => scenario.PriorSeasonStats.Any(stat => stat.PersonId == id && !string.IsNullOrWhiteSpace(stat.LeagueName))), "Draft prospects should have prior youth/junior stats.");
    }

    public void PlayerDossierIncludesCareerHistory()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Roster.Players[0].PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);

        Assert.True(dossier.Sections.Any(section => section.Title == "Career History"), "Player dossier should include a Career History section.");
        Assert.True(dossier.Sections.Single(section => section.Title == "Career History").Lines.Any(line => line.Contains("Last-season stats:", StringComparison.Ordinal)), "Dossier career history should include prior stats.");
    }

    public void OrganizationHasPriorSeasonHistory()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.OrganizationHistory is not null, "Scenario should include prior organization history.");
        Assert.True(scenario.OrganizationHistory!.Wins > 0, "Prior organization history should include wins.");
        Assert.True(!string.IsNullOrWhiteSpace(scenario.OrganizationHistory.PreviousLeagueChampion), "League should include previous champion placeholder.");
    }

    public void HistoryDoesNotExposeHiddenRatings()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Roster.Players[0].PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);
        var text = string.Join(" ", dossier.Sections.SelectMany(section => section.Lines)
            .Concat(scenario.PriorSeasonStats.Select(stat => stat.SummaryText))
            .Concat(scenario.CareerStatSummaries.Select(summary => summary.DisplaySummary))
            .Concat(scenario.PlayerCareerTimelines.SelectMany(timeline => timeline.Entries)));

        Assert.False(text.Contains("CurrentAbility", StringComparison.Ordinal), "History should not expose hidden current ability.");
        Assert.False(text.Contains("Potential =", StringComparison.Ordinal), "History should not expose hidden potential ratings.");
        Assert.False(text.Contains("Hidden", StringComparison.Ordinal), "History should not expose hidden ratings language.");
    }

    public void StaffHaveCareerHistoryWherePossible()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffMembers.Any(), "Scenario should have staff.");
        Assert.True(scenario.StaffMembers.All(staff => scenario.PlayerCareerTimelines.Any(timeline => timeline.PersonId == staff.PersonId)), "Staff should have role-based career history where possible.");
    }

    public void AlphaDesktopExposesExistingWorldHistory()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("LastSeasonStats", StringComparison.Ordinal), "Roster UI should expose last-season stats.");
        Assert.True(source.Contains("CareerStatSummary", StringComparison.Ordinal), "Roster detail should expose career summary.");
        Assert.True(source.Contains("Organization Prior Season", StringComparison.Ordinal), "Reports/history should expose organization prior season.");
        Assert.True(source.Contains("Previous champion", StringComparison.Ordinal), "Desktop should expose previous champion placeholder.");
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
