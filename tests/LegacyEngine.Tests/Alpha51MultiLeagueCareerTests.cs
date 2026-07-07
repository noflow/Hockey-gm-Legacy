using LegacyEngine.Integration;

internal sealed class Alpha51MultiLeagueCareerTests
{
    public void LeagueSelectionProvidesRequiredProfiles()
    {
        var service = new MultiLeagueCareerService();
        var profiles = service.BuildLeagueProfiles();

        Assert.True(profiles.Any(profile => profile.Experience == LeagueExperience.Nhl), "NHL profile should be available.");
        Assert.True(profiles.Any(profile => profile.Experience == LeagueExperience.Ahl), "AHL profile should be available.");
        Assert.True(profiles.Any(profile => profile.Experience == LeagueExperience.Junior), "Junior profile should be available.");
        Assert.True(profiles.Any(profile => profile.Experience == LeagueExperience.Custom), "Custom placeholder profile should be available.");
        Assert.True(profiles.All(profile => profile.Teams.Count > 0), "Every league should expose team choices.");
    }

    public void TeamSelectionCreatesScenarioSettings()
    {
        var service = new MultiLeagueCareerService();
        var selection = service.SelectLeagueAndTeam(LeagueExperience.Junior, "org-prairie-falcons");

        Assert.Equal(LeagueExperience.Junior, selection.LeagueProfile.Experience);
        Assert.Equal("org-prairie-falcons", selection.SelectedTeam.OrganizationId);
        Assert.Equal(selection.SelectedTeam.TeamName, selection.ScenarioSettings.TeamName);
        Assert.Equal(selection.LeagueProfile.Identity.LeagueId, selection.ScenarioSettings.LeagueId);
    }

    public void NhlScenarioUsesNhlProfileAndRulebook()
    {
        var scenario = ScenarioFor(LeagueExperience.Nhl, "org-seattle-cascades");

        Assert.Equal(LeagueExperience.Nhl, scenario.ScenarioSnapshot.LeagueProfile.Experience);
        Assert.Equal("nhl_style", scenario.Registry.Rulebook!.LeagueType);
        Assert.True(scenario.Registry.Rulebook.DraftRules!.DraftEnabled, "NHL-style career should enable the draft.");
        Assert.Equal(23, scenario.Registry.Rulebook.RosterRules!.ActiveRoster);
        Assert.Equal("Seattle Cascades", scenario.ScenarioSnapshot.Organization.Name);
    }

    public void AhlScenarioDisablesAmateurDraftAndReferencesParent()
    {
        var scenario = ScenarioFor(LeagueExperience.Ahl, "org-evergreen-comets");

        Assert.Equal(LeagueExperience.Ahl, scenario.ScenarioSnapshot.LeagueProfile.Experience);
        Assert.Equal("ahl_style", scenario.Registry.Rulebook!.LeagueType);
        Assert.False(scenario.Registry.Rulebook.DraftRules!.DraftEnabled, "AHL-style career should not create an amateur draft.");
        Assert.Equal("org-seattle-cascades", scenario.ScenarioSnapshot.TeamSelection.ParentOrganizationId);
        Assert.Equal("org-seattle-cascades", scenario.ScenarioSnapshot.Organization.ParentOrganizationId);
    }

    public void JuniorScenarioKeepsJuniorFocus()
    {
        var scenario = ScenarioFor(LeagueExperience.Junior, "org-prairie-falcons");

        Assert.Equal(LeagueExperience.Junior, scenario.ScenarioSnapshot.LeagueProfile.Experience);
        Assert.Equal("junior", scenario.Registry.Rulebook!.LeagueType);
        Assert.True(scenario.ScenarioSnapshot.LeagueProfile.Identity.PrimaryGameplayFocus.Contains("Recruiting"), "Junior should emphasize recruiting.");
        Assert.True(scenario.Registry.Rulebook.DraftRules!.DraftEnabled, "Junior should keep draft behavior.");
    }

    public void PlayerPipelineUsesSharedPersonIds()
    {
        var scenario = ScenarioFor(LeagueExperience.Nhl, "org-seattle-cascades").ScenarioSnapshot;
        var peopleIds = scenario.AlphaSnapshot.People.Select(person => person.PersonId).ToHashSet(StringComparer.Ordinal);
        var draftIds = scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToArray();
        var recruitIds = scenario.AlphaSnapshot.Recruits.Select(recruit => recruit.RecruitPersonId).ToArray();

        Assert.True(draftIds.All(peopleIds.Contains), "Draft prospects should reuse shared person ids.");
        Assert.True(recruitIds.All(peopleIds.Contains), "Recruit profiles should reuse shared person ids.");
        Assert.Equal(peopleIds.Count, scenario.AlphaSnapshot.People.Select(person => person.PersonId).Distinct(StringComparer.Ordinal).Count());
    }

    public void SaveLoadPreservesLeagueAndRulebook()
    {
        var ready = ScenarioFor(LeagueExperience.Ahl, "org-evergreen-comets");
        var service = new SaveGameService();
        var savePath = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha51-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(ready.ScenarioSnapshot, ready.Registry.Rulebook!);
        var saved = service.SaveCareer(ready.ScenarioSnapshot, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, savePath);

        Assert.True(saved.Success, saved.Message);
        var loaded = service.LoadFromFile(savePath);

        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(LeagueExperience.Ahl, loaded.SaveGame!.ScenarioSnapshot.LeagueProfile.Experience);
        Assert.Equal("ahl_style_default", loaded.SaveGame.Metadata.RulebookId);
        Assert.Equal("ahl_style", loaded.Registry!.Rulebook!.LeagueType);
        File.Delete(savePath);
    }

    public void AlphaDesktopExposesLeagueSelectionFlow()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Choose a league, choose a team", StringComparison.Ordinal), "Startup should explain league/team/GM flow.");
        Assert.True(source.Contains("new WorkspaceScreen(\"League Overview\", CreateTextScreen(\"League Overview\"))", StringComparison.Ordinal), "Desktop should expose League Overview.");
        Assert.True(source.Contains("new WorkspaceScreen(\"League Rules\", CreateTextScreen(\"League Rules\"))", StringComparison.Ordinal), "Desktop should expose League Rules.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Teams\", CreateTextScreen(\"Teams\"))", StringComparison.Ordinal), "Desktop should expose Teams.");
        Assert.True(source.Contains("SelectedLeagueExperience", StringComparison.Ordinal), "Startup should bind selected league.");
        Assert.True(source.Contains("SelectedTeamOption", StringComparison.Ordinal), "Startup should bind selected team.");
    }

    private static NewGmScenarioResult ScenarioFor(LeagueExperience experience, string organizationId)
    {
        var service = new MultiLeagueCareerService();
        return service.CreateScenario(service.SelectLeagueAndTeam(experience, organizationId));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HockeyGmLegacy.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
