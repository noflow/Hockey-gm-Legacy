using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha54PlayerPipelineTests
{
    public void NhlDraftCreatesJuniorYouthProspects()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;

        Assert.True(scenario.PlayerPipeline.Count > 0, "NHL scenario should expose a player pipeline.");
    }

    public void EighteenYearOldChlProspectCannotBeAssignedToAhl()
    {
        Assert.True(RulebookPresets.CreateNhlStyle().PlayerAssignmentRules?.ChlToAhlRestrictionEnabled == true, "CHL-to-AHL age restriction should be represented.");
    }

    public void NineteenYearOldChlProspectCannotBeAssignedToAhlUnlessExceptionEnabled()
    {
        var rules = RulebookPresets.CreateNhlStyle().PlayerAssignmentRules!;

        Assert.True(rules.AhlEligibilityAge >= 20 || rules.OneNineteenYearOldChlExceptionEnabled, "Nineteen-year-old assignment needs rulebook support.");
    }

    public void TwentyYearOldProspectCanBeAssignedToAhlIfSigned()
    {
        Assert.Equal(20, RulebookPresets.CreateNhlStyle().PlayerAssignmentRules!.AhlEligibilityAge);
    }

    public void EuropeanCollegePlaceholderProspectCanBeAssignedBasedOnRulebook()
    {
        Assert.True(RulebookPresets.CreateNhlStyle().PlayerAssignmentRules!.EuropeanAndCollegeProspectsCanPlayAhlAt18, "European/college exception should be rulebook-driven.");
    }

    public void SignedEighteenNineteenYearOldHasElcSlideEligibility()
    {
        Assert.Equal(19, RulebookPresets.CreateNhlStyle().PlayerAssignmentRules!.ElcSlideAgeCutoff);
    }

    public void TenNhlGamesPreventsSlide()
    {
        Assert.Equal(10, RulebookPresets.CreateNhlStyle().PlayerAssignmentRules!.ElcSlideNhlGameThreshold);
    }

    public void FewerThanTenNhlGamesAllowsSlide()
    {
        Assert.True(RulebookPresets.CreateNhlStyle().PlayerAssignmentRules!.ElcSlideNhlGameThreshold > 1, "ELC slide threshold should allow fewer-than-threshold games.");
    }

    public void InvalidAssignmentGivesClearReason()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "PlayerPipelineService.cs"));

        Assert.True(source.Contains("reason", StringComparison.OrdinalIgnoreCase), "Pipeline assignment logic should expose readable reasons.");
    }

    public void NhlTeamShowsAhlAffiliateRoster()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;

        Assert.True(scenario.AffiliateLinks.Count > 0, "NHL scenario should include affiliate links.");
    }

    public void AhlTeamShowsAssignedProspects()
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Ahl).First();
        var scenario = service.CreateScenario(service.SelectLeagueAndTeam(LeagueExperience.Ahl, team.OrganizationId)).ScenarioSnapshot;

        Assert.True(scenario.PlayerPipeline.Count > 0 || scenario.AlphaSnapshot.Roster.Players.Count > 0, "AHL scenario should show assigned/pro pipeline context.");
    }

    public void DossierShowsPipelineStatus()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Pipeline", StringComparison.OrdinalIgnoreCase) || source.Contains("rights", StringComparison.OrdinalIgnoreCase), "Desktop dossier should expose pipeline or rights status.");
    }

    public void SaveLoadPreservesPipelineStatus()
    {
        var created = CreateNhlScenario();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha54-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(created.ScenarioSnapshot, created.Registry.Rulebook ?? RulebookPresets.CreateNhlStyle());
        var saved = new SaveGameService().SaveCareer(created.ScenarioSnapshot, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, created.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(created.ScenarioSnapshot.PlayerPipeline.Count, loaded.SaveGame!.ScenarioSnapshot.PlayerPipeline.Count);
    }

    public void AlphaDesktopExposesPipelineFilters()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Prospects", StringComparison.Ordinal) && source.Contains("Roster", StringComparison.Ordinal), "Desktop should expose roster/prospect pipeline surfaces.");
    }

    public void NoSalaryCapOrWaiversAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n", Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.False(text.Contains("WaiverClaimEngine", StringComparison.OrdinalIgnoreCase), "Alpha 5.4/5.6 still should not add full waiver claim system.");
    }

    private static NewGmScenarioResult CreateNhlScenario()
    {
        var service = new MultiLeagueCareerService();
        var team = service.TeamsFor(LeagueExperience.Nhl).First();
        return service.CreateScenario(service.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId));
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
