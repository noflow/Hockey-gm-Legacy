using LegacyEngine.Integration;
using LegacyEngine.Owners;

internal sealed class Alpha48OwnerJobSecurityV2Tests
{
    public void OwnerPersonalityGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var summary = new OwnerOfficeService().BuildSummary(scenario);

        Assert.Equal(OwnerPersonalityType.PatientBuilder, summary.Personality.PersonalityType);
        Assert.True(summary.Personality.Vision.Contains("sustainable", StringComparison.OrdinalIgnoreCase), "Owner personality should include a clear vision.");
        Assert.True(summary.Personality.RiskTolerance is >= 0 and <= 100, "Owner risk tolerance should be bounded.");
    }

    public void SeasonExpectationsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var expectations = new OwnerOfficeService().BuildSummary(scenario).Expectations;

        Assert.True(expectations.Count >= 3, "Owner should generate multiple season expectations.");
        Assert.True(expectations.Any(item => item.ExpectationType == OwnerExpectationType.DevelopYoungPlayers), "Owner should include development expectation.");
        Assert.True(expectations.All(item => item.Deadline >= scenario.CurrentDate), "Expectations should have forward deadlines.");
    }

    public void ConfidenceChangesWithBudget()
    {
        var result = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = result.ScenarioSnapshot;
        var service = new OwnerOfficeService();
        var normal = service.BuildSummary(scenario, new BudgetOverviewService().Build(scenario, result.Registry.Rulebook!)).Confidence;
        var overBudget = service.BuildSummary(scenario, new BudgetSnapshot(
            scenario.AlphaSnapshot.Owner.Budget.Total,
            scenario.AlphaSnapshot.Owner.Budget.Total + 50_000,
            -50_000,
            0,
            0,
            scenario.AlphaSnapshot.Owner.Budget.Scouting,
            scenario.AlphaSnapshot.Owner.Budget.Operations,
            BudgetStatus.OverBudget,
            "Owner warning: over budget.")).Confidence;

        Assert.True(overBudget.Confidence < normal.Confidence, "Over-budget state should reduce owner confidence.");
        Assert.True(overBudget.Pressure > normal.Pressure, "Over-budget state should increase pressure.");
    }

    public void MeetingsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var meetings = new OwnerOfficeService().BuildSummary(scenario).Meetings;

        Assert.True(meetings.Any(item => item.MeetingType == OwnerMeetingType.Preseason), "Preseason owner meeting should exist.");
        Assert.True(meetings.Any(item => item.MeetingType == OwnerMeetingType.BudgetReview), "Budget review owner meeting should exist.");
        Assert.True(meetings.All(item => item.Recommendations.Count > 0), "Meetings should include recommendations.");
    }

    public void LettersGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var letters = new OwnerOfficeService().BuildSummary(scenario).Letters;

        Assert.True(letters.Count > 0, "Owner letters should be generated.");
        Assert.True(letters.All(item => item.Body.Contains(scenario.AlphaSnapshot.Owner.Name, StringComparison.Ordinal)), "Owner letters should identify the owner.");
    }

    public void PerformanceReviewGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var review = new OwnerOfficeService().BuildSummary(scenario).PerformanceReview;

        Assert.True(review.CategoryGrades.ContainsKey("Budget"), "Performance review should grade budget.");
        Assert.True(review.CategoryGrades.ContainsKey("Drafting"), "Performance review should grade drafting.");
        Assert.True(review.Narrative.Contains("Job security", StringComparison.Ordinal), "Performance review should explain job security.");
    }

    public void JobSecurityExplained()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var security = new OwnerOfficeService().BuildSummary(scenario).JobSecurity;

        Assert.True(security.Score is >= 0 and <= 100, "Job security score should be bounded.");
        Assert.True(security.Reasons.Count >= 3, "Job security should include multiple reasons.");
        Assert.True(security.Explanation.Contains("because", StringComparison.OrdinalIgnoreCase), "Job security explanation should explain why.");
    }

    public void ActionCenterIncludesOwnerItems()
    {
        var result = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = result.ScenarioSnapshot;
        var items = new ActionCenterService().BuildItems(
            scenario,
            Array.Empty<InboxMessage>(),
            new BudgetOverviewService().Build(scenario, result.Registry.Rulebook!),
            new SeasonReadinessService().Evaluate(result.Registry, scenario),
            new StaffOfficeService().BuildVacancies(scenario, result.Registry.Rulebook!));

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Owner), "Action Center should include owner items.");
    }

    public void ExecutiveReportIncludesOwnerJobSecurity()
    {
        var result = NewGmScenarioBootstrapper.CreateScenario();
        var completed = result.ScenarioSnapshot with { Season = result.ScenarioSnapshot.Season with { Status = LegacyEngine.Seasons.SeasonStatus.Completed } };
        var report = new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(result.Registry, completed).Report!;
        var section = report.FindSection("Owner & Job Security");

        Assert.True(section is not null, "Executive report should include owner and job security section.");
        Assert.True(section!.Items.ContainsKey("Owner Grade"), "Owner report should include owner grade.");
        Assert.True(section.Items.ContainsKey("Job Security"), "Owner report should include job security.");
        Assert.True(section.Items.ContainsKey("Future Expectations"), "Owner report should include future expectations.");
    }

    public void CareerHistoryStoresOwnerItems()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new OwnerOfficeService();
        var summary = service.BuildSummary(scenario);
        var withMeeting = service.RecordOwnerMeeting(scenario, summary.Meetings.First());
        var withLetter = service.RecordOwnerLetter(withMeeting, summary.Letters.First());
        var withReview = service.RecordPerformanceReview(withLetter, summary.PerformanceReview);

        Assert.True(withReview.CareerTimeline.Entries.Any(item => item.EntryType == CareerTimelineEntryType.OwnerMeeting), "Owner meeting should be stored in career timeline.");
        Assert.True(withReview.CareerTimeline.Entries.Any(item => item.EntryType == CareerTimelineEntryType.OwnerLetter), "Owner letter should be stored in career timeline.");
        Assert.True(withReview.CareerTimeline.Entries.Any(item => item.EntryType == CareerTimelineEntryType.OwnerPerformanceReview), "Owner review should be stored in career timeline.");
    }

    public void AlphaDesktopExposesOwnerV2Ui()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Personality:", StringComparison.Ordinal), "Desktop owner view should expose owner personality.");
        Assert.True(source.Contains("Season Expectations", StringComparison.Ordinal), "Desktop owner view should expose expectations.");
        Assert.True(source.Contains("Job security", StringComparison.Ordinal), "Desktop owner view should expose job security.");
        Assert.True(source.Contains("Meeting History / Schedule", StringComparison.Ordinal), "Desktop owner view should expose meetings.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "OwnerOfficeService.cs"));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Owner v2 should not reference Godot.");
        Assert.False(source.Contains("SaveGame", StringComparison.OrdinalIgnoreCase), "Owner v2 should not change save/load.");
        Assert.False(source.Contains("BasicGameSimulator", StringComparison.OrdinalIgnoreCase), "Owner v2 should not change game simulation.");
        Assert.False(source.Contains("Fired", StringComparison.OrdinalIgnoreCase), "Owner v2 should not build an actual firing system.");
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
