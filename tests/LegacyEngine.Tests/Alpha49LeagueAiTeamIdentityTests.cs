using LegacyEngine.Integration;

internal sealed class Alpha49LeagueAiTeamIdentityTests
{
    public void OrganizationIdentityGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profile = new LeagueAiService().BuildOrganizationProfile(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name);

        Assert.Equal(scenario.Organization.OrganizationId, profile.OrganizationId);
        Assert.True(profile.Summary.Contains(profile.TeamName, StringComparison.Ordinal), "Profile summary should name the team.");
        Assert.True(profile.Identity.ToString().Length > 0, "Team identity should be generated.");
    }

    public void GmPersonalityGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profiles = new LeagueAiService().BuildReport(scenario).Profiles;

        Assert.True(profiles.All(profile => profile.GmPersonality.ToString().Length > 0), "Each organization should have AI GM personality.");
        Assert.True(profiles.Select(profile => profile.GmPersonality).Distinct().Count() >= 2, "League should show more than one GM personality.");
    }

    public void OwnerPhilosophyInfluencesProfile()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var overBudget = new BudgetSnapshot(
            scenario.AlphaSnapshot.Owner.Budget.Total,
            scenario.AlphaSnapshot.Owner.Budget.Total + 100_000,
            -100_000,
            0,
            0,
            scenario.AlphaSnapshot.Owner.Budget.Scouting,
            scenario.AlphaSnapshot.Owner.Budget.Operations,
            BudgetStatus.OverBudget,
            "Owner warning: over budget.");
        var profile = new LeagueAiService().BuildOrganizationProfile(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, overBudget);

        Assert.Equal(OwnerInfluencePhilosophy.BudgetDiscipline, profile.OwnerPhilosophy);
        Assert.True(profile.Behavior.FreeAgencyBehavior.Contains("Budget", StringComparison.OrdinalIgnoreCase), "Budget owner influence should affect free agency behavior.");
    }

    public void NeedsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profile = new LeagueAiService().BuildOrganizationProfile(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name);

        Assert.True(profile.CurrentNeeds.Count > 0, "Current needs should be generated.");
        Assert.True(profile.TradeNeedsProfile.Needs.Count > 0, "Trade needs profile should be reused.");
    }

    public void TradeBehaviorGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var behavior = new LeagueAiService().BuildReport(scenario).Profiles.First().Behavior.TradeBehavior;

        Assert.True(behavior.Contains("Trade behavior", StringComparison.Ordinal), "Trade behavior should be readable.");
    }

    public void FreeAgencyBehaviorGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var behavior = new LeagueAiService().BuildReport(scenario).Profiles.First().Behavior.FreeAgencyBehavior;

        Assert.True(behavior.Contains("behavior", StringComparison.OrdinalIgnoreCase), "Free agency behavior should be readable.");
    }

    public void DraftPhilosophyGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profile = new LeagueAiService().BuildReport(scenario).Profiles.First();

        Assert.True(profile.DraftStyle.ToString().Length > 0, "Draft style should be generated.");
        Assert.True(profile.Behavior.DraftBehavior.Contains("Draft behavior", StringComparison.Ordinal), "Draft behavior should be readable.");
    }

    public void ScoutingPhilosophyGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profile = new LeagueAiService().BuildReport(scenario).Profiles.First();

        Assert.True(profile.ScoutingFocus.ToString().Length > 0, "Scouting focus should be generated.");
        Assert.True(profile.Behavior.ScoutingBehavior.Contains("Scouting behavior", StringComparison.Ordinal), "Scouting behavior should be readable.");
    }

    public void LongTermStrategyIsStable()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new LeagueAiService();
        var first = service.BuildReport(scenario).Profiles.First(profile => profile.OrganizationId != scenario.Organization.OrganizationId);
        var later = service.BuildReport(scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { WorldState = scenario.AlphaSnapshot.WorldState with { Clock = scenario.AlphaSnapshot.WorldState.Clock.AdvanceDays(31) } }
        }).Profiles.First(profile => profile.OrganizationId == first.OrganizationId);

        Assert.Equal(first.Identity, later.Identity);
        Assert.Equal(first.CurrentStrategy, later.CurrentStrategy);
    }

    public void OrganizationProfileGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var report = new LeagueAiService().BuildReport(scenario);

        Assert.True(report.Profiles.Count >= 6, "League report should include player team and league opponents.");
        Assert.True(report.Profiles.All(profile => profile.BudgetStyle.Length > 0 && profile.DevelopmentGrade.Length > 0), "Profiles should include budget style and development grade.");
    }

    public void LeagueNewsGeneratedWithoutSpam()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var news = new LeagueAiService().BuildReport(scenario).LeagueNews;

        Assert.True(news.Count <= 3, "League AI news should be occasional, not spam.");
        Assert.True(news.All(item => item.TransactionType == LeagueTransactionType.TeamIdentityUpdate), "League AI news should use team identity update transaction type.");
    }

    public void HistoryRecorded()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new LeagueAiService();
        var profile = service.BuildOrganizationProfile(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name);
        var updated = service.RecordIdentityHistory(scenario, profile);

        Assert.True(updated.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.OrganizationIdentityChanged), "Identity history should be recorded.");
        Assert.True(updated.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.OrganizationStrategyChanged), "Strategy history should be recorded.");
    }

    public void AlphaDesktopExposesLeagueAiUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Team Identity", StringComparison.Ordinal), "Desktop should expose team identity.");
        Assert.True(source.Contains("Current Needs", StringComparison.Ordinal), "Desktop should expose current needs.");
        Assert.True(source.Contains("League Direction", StringComparison.Ordinal), "Desktop should expose league direction news.");
        Assert.True(source.Contains("Scouting focus", StringComparison.Ordinal), "Desktop should expose scouting philosophy.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "LeagueAiService.cs"));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "League AI should not reference Godot.");
        Assert.False(source.Contains("SaveGame", StringComparison.OrdinalIgnoreCase), "League AI should not change save/load.");
        Assert.False(source.Contains("BasicGameSimulator", StringComparison.OrdinalIgnoreCase), "League AI should not change game simulation.");
        Assert.False(source.Contains("MediaEngine", StringComparison.OrdinalIgnoreCase), "League AI should not build a media engine.");
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

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
