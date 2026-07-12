using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

internal sealed class Alpha854FirstDayWorkloadTests
{
    public void NhlCareerStartsWithFunctioningInheritedOrganization()
    {
        var scenario = NhlScenario();

        Assert.True(scenario.WorkforceValidation?.IsValid == true, scenario.WorkforceValidation?.Summary ?? "Workforce validation was missing.");
        Assert.True(scenario.AlphaSnapshot.Roster.Players.Count >= 20, "A new NHL GM should inherit a full roster.");
        Assert.True(scenario.StaffMembers.Count > 0, "A new NHL GM should inherit active staff.");
        Assert.True(scenario.CurrentLineup is not null, "A new NHL GM should inherit line combinations.");
        Assert.True(scenario.DevelopmentPlans.Count > 0, "A new NHL GM should inherit development plans.");
        Assert.True(scenario.ScoutingOperations.Count > 0 || scenario.CompletedScoutingReports.Count > 0, "A new NHL GM should inherit scouting work.");
        Assert.True(scenario.Contracts.Count > 0, "A new NHL GM should inherit contract history and active agreements.");
    }

    public void FirstDayActionCenterIsCuratedToThreeToFiveItems()
    {
        var scenario = NhlScenario();
        var visible = FilteredActions(scenario);

        Assert.True(visible.Count is >= 3 and <= 5, "Day 1 should show only three to five meaningful Action Center items.");
        Assert.True(visible.Any(item => item.Category is ActionCenterCategory.Contracts or ActionCenterCategory.Roster), "Day 1 should include an important contract or roster decision.");
        Assert.True(visible.Select(item => item.ActionCenterItemId).Distinct(StringComparer.Ordinal).Count() == visible.Count, "Opening actions must not be duplicated.");
    }

    public void RoutineContractDecisionsStayInTheirWorkspaceOnDayOne()
    {
        var scenario = NhlScenario();
        var raw = RawActions(scenario);
        var visible = new FirstWeekOnboardingService().FilterActionCenterItems(scenario, raw);
        var rawContractItems = raw.Count(item => item.Category == ActionCenterCategory.Contracts);
        var visibleContractItems = visible.Count(item => item.Category == ActionCenterCategory.Contracts);

        Assert.True(rawContractItems > visibleContractItems, "Not every expiring contract should be a Day 1 action.");
        Assert.True(scenario.PlayerRightsDecisions.Count(decision => decision.IsOpenDecision) > visibleContractItems, "Open rights decisions remain available even when they are not all promoted to the Action Center.");
    }

    public void FirstDayInboxIsSmallAndOrganizationFocused()
    {
        var scenario = NhlScenario();
        var inbox = scenario.FirstDayInbox;

        Assert.True(inbox.Count is >= 4 and <= 5, "Starting inbox should contain a small useful briefing set.");
        Assert.True(inbox.Any(item => item.InboxItemId == "new-gm-inbox-owner-welcome"), "Owner welcome should be present.");
        Assert.True(inbox.Any(item => item.InboxItemId == "new-gm-inbox-assistant-gm"), "Assistant GM briefing should be present.");
        Assert.True(inbox.Any(item => item.InboxItemId == "new-gm-inbox-roster-needs"), "Head coach roster assessment should be present.");
        Assert.True(inbox.Any(item => item.InboxItemId == "new-gm-inbox-draft-board"), "Head scout summary should be present.");
        Assert.False(inbox.Any(item => item.Title.Contains("League draft timeline", StringComparison.Ordinal) || item.Title.Contains("transaction", StringComparison.OrdinalIgnoreCase)), "Routine league chatter should not enter the starting GM inbox.");
    }

    public void FirstWeekGraduallySurfacesDeferredWork()
    {
        var scenario = NhlScenario();
        var service = new FirstWeekOnboardingService();
        var raw = RawActions(scenario);
        var dayOne = service.FilterActionCenterItems(scenario, raw);
        var dayThreeScenario = scenario with { OnboardingPlan = scenario.OnboardingPlan! with { StartDate = scenario.CurrentDate.AddDays(-3) } };
        var dayThree = service.FilterActionCenterItems(dayThreeScenario, raw);

        Assert.True(dayThree.Count >= dayOne.Count, "Deferred decisions should begin surfacing during the first week.");
        Assert.True(dayThree.Count <= 5, "The day-three rollout should remain manageable.");
    }

    public void AssistantGmBriefingIsConciseAndActionable()
    {
        var scenario = NhlScenario();
        var briefing = scenario.OnboardingPlan?.AssistantGmBriefing;

        Assert.True(briefing is not null, "Scenario should create an Assistant GM briefing.");
        Assert.True(briefing!.Strengths.Count == 3, "Assistant GM briefing should summarize three strengths.");
        Assert.True(briefing.Concerns.Count == 3, "Assistant GM briefing should summarize three concerns.");
        Assert.False(string.IsNullOrWhiteSpace(briefing.RecommendedFirstAction), "Assistant GM briefing should recommend a first action.");
        Assert.True(scenario.FirstDayInbox.Single(item => item.InboxItemId == "new-gm-inbox-assistant-gm").Summary.Contains(briefing.RecommendedFirstAction, StringComparison.Ordinal), "Inbox briefing should include the recommended first action.");
    }

    public void DesktopRoutesActionCenterThroughFirstWeekOnboarding()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("FilterActionCenterItems(ScenarioSnapshot, allItems)", StringComparison.Ordinal), "Desktop Action Center should use the first-week workload filter.");
        Assert.True(source.Contains("OnboardingPlan?.AssistantGmBriefing", StringComparison.Ordinal), "Dashboard should show the inherited Assistant GM recommendation.");
    }

    private static IReadOnlyList<ActionCenterItem> RawActions(NewGmScenarioSnapshot scenario)
    {
        var ready = Registry();
        var inbox = new InboxManager();
        inbox.AddRange(scenario.FirstDayInbox);
        return new ActionCenterService().BuildItems(
            scenario,
            inbox.AllMessages,
            new BudgetOverviewService().Build(scenario, RulebookPresets.CreateNhlStyle()),
            new SeasonReadinessService().Evaluate(ready.Registry, scenario),
            new StaffOfficeService().BuildVacancies(scenario, RulebookPresets.CreateNhlStyle()));
    }

    private static IReadOnlyList<ActionCenterItem> FilteredActions(NewGmScenarioSnapshot scenario) =>
        new FirstWeekOnboardingService().FilterActionCenterItems(scenario, RawActions(scenario));

    private static NewGmScenarioSnapshot NhlScenario() =>
        Registry().ScenarioSnapshot;

    private static NewGmScenarioResult Registry()
    {
        var careers = new MultiLeagueCareerService();
        var team = careers.TeamsFor(LeagueExperience.Nhl).First();
        return careers.CreateScenario(careers.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId));
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
