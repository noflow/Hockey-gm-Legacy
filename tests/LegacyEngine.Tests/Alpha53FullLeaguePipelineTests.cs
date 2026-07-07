using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

internal sealed class Alpha53FullLeaguePipelineTests
{
    public void NhlLeagueHasThirtyTwoTeams()
    {
        var profile = new MultiLeagueCareerService().GetProfile(LeagueExperience.Nhl);

        Assert.Equal(32, profile.Teams.Count);
    }

    public void AhlLeagueHasThirtyTwoTeams()
    {
        var profile = new MultiLeagueCareerService().GetProfile(LeagueExperience.Ahl);

        Assert.Equal(32, profile.Teams.Count);
    }

    public void JuniorLeaguesHaveRequiredTeamCounts()
    {
        var teams = new MultiLeagueCareerService().GetProfile(LeagueExperience.Junior).Teams;

        Assert.Equal(23, teams.Count(team => team.LeagueName == "WHL"));
        Assert.Equal(20, teams.Count(team => team.LeagueName == "OHL"));
        Assert.Equal(18, teams.Count(team => team.LeagueName == "QMJHL"));
    }

    public void TeamSelectionListsAllTeams()
    {
        var service = new MultiLeagueCareerService();

        Assert.Equal(32, service.TeamsFor(LeagueExperience.Nhl).Count);
        Assert.Equal(32, service.TeamsFor(LeagueExperience.Ahl).Count);
        Assert.Equal(61, service.TeamsFor(LeagueExperience.Junior).Count);
    }

    public void NhlTeamHasAhlAffiliate()
    {
        var team = new MultiLeagueCareerService().GetProfile(LeagueExperience.Nhl).Teams.Single(team => team.OrganizationId == "org-seattle-cascades");

        Assert.Equal("org-evergreen-comets", team.AffiliateOrganizationId);
    }

    public void AhlTeamHasNhlParent()
    {
        var team = new MultiLeagueCareerService().GetProfile(LeagueExperience.Ahl).Teams.Single(team => team.OrganizationId == "org-evergreen-comets");

        Assert.Equal("org-seattle-cascades", team.ParentOrganizationId);
    }

    public void DraftedNhlProspectCanBeReturnedToJunior()
    {
        var ready = ScenarioWithNhlProspect();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();

        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.ReturnToJunior, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(ProspectStatus.ReturnedToJunior, result.Prospect.Status);
        Assert.True(result.ScenarioSnapshot.PlayerPipeline.Any(record => record.PersonId == prospect.ProspectPersonId && record.PipelineStatus == PlayerPipelineStatus.ReturnedToJunior), "Pipeline should record returned-to-junior status.");
    }

    public void DraftedNhlProspectCanBeAssignedToAhl()
    {
        var ready = ScenarioWithNhlProspect();
        var prospect = ready.ScenarioSnapshot.ProspectRights.First();

        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(ProspectStatus.AssignedToAffiliate, result.Prospect.Status);
        Assert.True(result.ScenarioSnapshot.PlayerPipeline.Any(record => record.PersonId == prospect.ProspectPersonId && record.PipelineStatus == PlayerPipelineStatus.AssignedToAhl), "Pipeline should record AHL assignment.");
    }

    public void AhlCareerStartsWithAssignedPlayers()
    {
        var ready = ScenarioFor(LeagueExperience.Ahl, "org-evergreen-comets").ScenarioSnapshot;

        Assert.True(ready.AlphaSnapshot.Roster.Players.Any(player => player.AcquisitionSource == PlayerAcquisitionSource.AssignedFromParentClub), "AHL roster should include parent-assigned players.");
        Assert.True(ready.PlayerPipeline.Any(record => record.ParentOrganization is not null && record.PipelineStatus == PlayerPipelineStatus.AhlRoster), "AHL pipeline should reference parent-club assigned players.");
    }

    public void JuniorCareerStillSupportsRecruitingAndDraft()
    {
        var ready = ScenarioFor(LeagueExperience.Junior, "org-prairie-falcons").ScenarioSnapshot;

        Assert.True(ready.AlphaSnapshot.Recruits.Count > 0, "Junior should still have recruits.");
        Assert.True(ready.LeagueProfile.DraftEnabled, "Junior should still have draft enabled.");
    }

    public void PlayerDossierShowsPipelineStatus()
    {
        var ready = ScenarioWithNhlProspect().ScenarioSnapshot;
        var prospect = ready.ProspectRights.First();
        var dossier = new PlayerDossierService().CreateDossier(ready, prospect.ProspectPersonId);
        var lines = dossier.Sections.SelectMany(section => section.Lines).ToArray();

        Assert.True(lines.Any(line => line.Contains("Pipeline status", StringComparison.Ordinal)), "Dossier should show pipeline status.");
        Assert.True(lines.Any(line => line.Contains("Current level", StringComparison.Ordinal)), "Dossier should show current level.");
    }

    public void SaveLoadPreservesFullLeagueAndAffiliateSetup()
    {
        var ready = ScenarioFor(LeagueExperience.Nhl, "org-seattle-cascades");
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha53-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(ready.ScenarioSnapshot, ready.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(ready.ScenarioSnapshot, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);

        Assert.True(saved.Success, saved.Message);
        var loaded = new SaveGameService().LoadFromFile(path);

        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(32, loaded.SaveGame!.ScenarioSnapshot.LeagueProfile.Teams.Count);
        Assert.True(loaded.SaveGame.ScenarioSnapshot.AffiliateLinks.Any(link => link.ParentOrganizationId == "org-seattle-cascades" && link.AffiliateOrganizationId == "org-evergreen-comets"), "Affiliate link should survive save/load.");
        Assert.True(loaded.SaveGame.ScenarioSnapshot.PlayerPipeline.Count > 0, "Player pipeline should survive save/load.");
        File.Delete(path);
    }

    public void AlphaDesktopExposesFullTeamBrowser()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("_teamSearchInput", StringComparison.Ordinal), "Team selection should support search.");
        Assert.True(source.Contains("_teamDivisionFilterInput", StringComparison.Ordinal), "Team selection should support league/division filtering.");
        Assert.True(source.Contains("_teamSortInput", StringComparison.Ordinal), "Team selection should support sorting.");
        Assert.True(source.Contains("Pipeline", StringComparison.Ordinal), "Desktop should expose pipeline context.");
    }

    public void GeneratedPeopleDoNotUseRealStarPlayerNames()
    {
        var ready = ScenarioFor(LeagueExperience.Nhl, "org-seattle-cascades").ScenarioSnapshot;
        var names = ready.AlphaSnapshot.People.Select(person => person.Identity.DisplayName).ToArray();

        Assert.False(names.Contains("Connor McDavid", StringComparer.Ordinal), "Generated scenario should not use real star player names.");
        Assert.False(names.Contains("Sidney Crosby", StringComparer.Ordinal), "Generated scenario should not use real star player names.");
        Assert.False(names.Contains("Auston Matthews", StringComparer.Ordinal), "Generated scenario should not use real star player names.");
    }

    private static NewGmScenarioResult ScenarioWithNhlProspect()
    {
        var ready = ScenarioFor(LeagueExperience.Nhl, "org-seattle-cascades");
        var scenario = ready.ScenarioSnapshot;
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.First();
        var person = scenario.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId);
        var prospect = new DraftRightsRecord(
            entry.ProspectPersonId,
            person.Identity.DisplayName,
            person.CalculateAge(scenario.CurrentDate),
            entry.Bio?.Position ?? RosterPosition.Center,
            1,
            1,
            ProspectStatus.DraftRightsHeld,
            entry.ProjectionText,
            entry.ScoutingConfidence ?? ScoutingConfidenceLevel.Medium,
            "Test prospect rights.");
        var updated = scenario with { ProspectRights = new[] { prospect } };
        updated = new PlayerPipelineService().EnsurePipeline(updated);
        updated = new PlayerPipelineService().UpsertProspect(updated, prospect, "Test drafted prospect entered rights pipeline.");
        return ready with { ScenarioSnapshot = updated, AlphaSnapshot = updated.AlphaSnapshot };
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
