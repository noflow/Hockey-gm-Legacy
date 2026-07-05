using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

internal sealed class ExecutiveReportTests
{
    public void ReadinessReportGenerated()
    {
        var ready = ReadyScenario();
        var result = new ExecutiveReportService().GenerateFrontOfficeReadinessReport(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Success, result.Message);
        Assert.Equal(ExecutiveReportKind.FrontOfficeReadiness, result.Report!.Kind);
        Assert.Equal("Front Office Readiness Report", result.Report.Title);
    }

    public void SeasonReviewGenerated()
    {
        var completed = CompletedScenario();
        var result = new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(completed.Registry, completed.ScenarioSnapshot);

        Assert.True(result.Success, result.Message);
        Assert.Equal(ExecutiveReportKind.EndOfSeasonExecutiveReview, result.Report!.Kind);
        Assert.Equal("End of Season Executive Review", result.Report.Title);
    }

    public void OwnerReviewGenerated()
    {
        var report = ReadinessReport();
        var section = report.FindSection("Owner Review");

        Assert.True(section is not null, "Owner Review section should exist.");
        Assert.True(section!.Items.ContainsKey("Owner Satisfaction"), "Owner satisfaction should be included.");
        Assert.True(section.Items.ContainsKey("Expectations"), "Owner expectations should be included.");
        Assert.True(section.Items.ContainsKey("Recommendation"), "Owner recommendation should be included.");
    }

    public void CoachReviewGenerated()
    {
        var report = ReadinessReport();
        var section = report.FindSection("Head Coach Report");

        Assert.True(section is not null, "Head Coach Report section should exist.");
        Assert.True(section!.Items.ContainsKey("Roster readiness"), "Roster readiness should be included.");
        Assert.True(section.Items.ContainsKey("Forward depth"), "Forward depth should be included.");
        Assert.True(section.Items.ContainsKey("Goaltending"), "Goaltending should be included.");
    }

    public void ScoutReviewGenerated()
    {
        var report = ReadinessReport();
        var section = report.FindSection("Head Scout Report");

        Assert.True(section is not null, "Head Scout Report section should exist.");
        Assert.True(section!.Items.ContainsKey("Draft Grade"), "Draft grade should be included.");
        Assert.True(section.Items.ContainsKey("Top Prospect"), "Top prospect should be included.");
        Assert.True(section.Items.ContainsKey("Hidden Gem"), "Hidden gem should be included.");
    }

    public void MedicalSummaryGenerated()
    {
        var report = ReadinessReport();
        var section = report.FindSection("Medical Department");

        Assert.True(section is not null, "Medical Department section should exist.");
        Assert.True(section!.Items.ContainsKey("Current Injuries"), "Current injuries should be included.");
        Assert.True(section.Items.ContainsKey("Risk Players"), "Risk players should be included.");
        Assert.True(section.Items.ContainsKey("Recovery Outlook"), "Recovery outlook should be included.");
    }

    public void DevelopmentSummaryGenerated()
    {
        var report = ReadinessReport();
        var section = report.FindSection("Development Department");

        Assert.True(section is not null, "Development Department section should exist.");
        Assert.True(section!.Items.ContainsKey("Top Development Story"), "Top development story should be included.");
        Assert.True(section.Items.ContainsKey("Player Ready to Break Out"), "Breakout player should be included.");
        Assert.True(section.Items.ContainsKey("Player Needing More Time"), "Player needing more time should be included.");
    }

    public void RosterComplianceIncluded()
    {
        var report = ReadinessReport();
        var section = report.FindSection("Roster Compliance");

        Assert.True(section is not null, "Roster Compliance section should exist.");
        Assert.True(section!.Items.ContainsKey("Current Size"), "Current size should be included.");
        Assert.True(section.Items.ContainsKey("Required Size"), "Required size should be included.");
        Assert.True(section.Items.ContainsKey("Goalie Count"), "Goalie count should be included.");
        Assert.Equal("READY", section.Items["Status"]);
    }

    public void OrganizationHealthCalculated()
    {
        var report = ReadinessReport();
        var section = report.FindSection("Organization Health");

        Assert.True(report.OrganizationHealthPercent > 0, "Organization health should be calculated.");
        Assert.True(section is not null, "Organization Health section should exist.");
        Assert.Equal(report.OrganizationHealthPercent.ToString(), section!.Items["Overall %"]);
    }

    public void PreviousSeasonComparisonWorks()
    {
        var completed = CompletedScenario();
        var previous = MinimalReport("executive-report:previous:EndOfSeasonExecutiveReview", ExecutiveReportKind.EndOfSeasonExecutiveReview, completed.ScenarioSnapshot.Season.Year - 1, 40);
        var scenario = completed.ScenarioSnapshot with
        {
            ExecutiveReports = ExecutiveReportArchive.Empty.AddOrReplace(previous)
        };
        var result = new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(completed.Registry, scenario);
        var comparison = result.Report!.FindSection("Organization Progress");

        Assert.True(comparison is not null, "Organization Progress section should exist.");
        Assert.False(comparison!.Items.Values.All(value => value == "Baseline"), "Previous season comparison should not be baseline when a prior report exists.");
    }

    public void ReportsStoredPermanently()
    {
        var ready = ReadyScenario();
        var front = new ExecutiveReportService().GenerateFrontOfficeReadinessReport(ready.Registry, ready.ScenarioSnapshot);
        var completed = ToCompletedScenario(ready.Registry, front.ScenarioSnapshot);
        var end = new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(completed.Registry, completed.ScenarioSnapshot);

        Assert.Equal(2, end.ScenarioSnapshot.ExecutiveReports.Reports.Count);
        Assert.True(end.ScenarioSnapshot.ExecutiveReports.Reports.Any(report => report.Kind == ExecutiveReportKind.FrontOfficeReadiness), "Front office report should remain in archive.");
        Assert.True(end.ScenarioSnapshot.ExecutiveReports.Reports.Any(report => report.Kind == ExecutiveReportKind.EndOfSeasonExecutiveReview), "End season report should be in archive.");
    }

    public void ArchiveRetrievalWorks()
    {
        var report = ReadinessReport();
        var archive = ExecutiveReportArchive.Empty.AddOrReplace(report);

        Assert.True(archive.Find(report.ReportId) is not null, "Report should be retrievable by id.");
        Assert.Equal(1, archive.CurrentSeason(report.SeasonYear).Count);
        Assert.Equal(0, archive.PreviousSeasons(report.SeasonYear).Count);
    }

    public void BeginSeasonStoresReadinessReport()
    {
        var ready = ReadyScenario();
        var result = new SeasonReadinessService().BeginSeason(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.ExecutiveReports.Reports.Any(report => report.Kind == ExecutiveReportKind.FrontOfficeReadiness), "Begin Season should archive the readiness report.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.FrontOfficeReadinessReportCreated), "Readiness report should create inbox item.");
    }

    public void AlphaDesktopExposesExecutiveReports()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Executive Reports", StringComparison.Ordinal), "AlphaDesktop should expose Executive Reports.");
        Assert.True(text.Contains("Current Season", StringComparison.Ordinal), "Executive Reports should show current season.");
        Assert.True(text.Contains("Previous Seasons", StringComparison.Ordinal), "Executive Reports should show previous seasons.");
        Assert.True(text.Contains("Front Office Readiness", StringComparison.Ordinal), "Executive Reports should show front office readiness.");
        Assert.True(text.Contains("End of Season Review", StringComparison.Ordinal), "Executive Reports should show end of season review.");
    }

    private static ExecutiveReportRecord ReadinessReport()
    {
        var ready = ReadyScenario();
        return new ExecutiveReportService().GenerateFrontOfficeReadinessReport(ready.Registry, ready.ScenarioSnapshot).Report!;
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) CompletedScenario()
    {
        var ready = ReadyScenario();
        return ToCompletedScenario(ready.Registry, ready.ScenarioSnapshot);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ToCompletedScenario(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario)
    {
        var season = scenario.Season with
        {
            Status = SeasonStatus.Completed,
            CurrentPhase = SeasonPhase.Offseason
        };
        var alpha = scenario.AlphaSnapshot with { Season = season };
        var completed = scenario with
        {
            AlphaSnapshot = alpha,
            Season = season
        };
        completed.Validate();
        return (registry, completed);
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyScenario()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var roster = scenario.ScenarioSnapshot.AlphaSnapshot.Roster;
        var total = roster.CurrentPlayers.Count;
        var active = roster.ActivePlayers.Count;
        var goalies = roster.CurrentPlayers.Count(player => player.IsGoalie);
        var overage = roster.CurrentPlayers.Count(player => player.IsOverage());
        var imports = roster.CurrentPlayers.Count(player => player.IsImport);
        var rulebook = WithRosterRules(
            RulebookPresets.Create(DraftLeaguePreset.JuniorMajor),
            new RosterRules
            {
                MinRoster = total,
                MaxRoster = total,
                ActiveRoster = active,
                GoaliesRequired = goalies,
                OverageSlots = Math.Max(3, overage),
                ImportSlots = Math.Max(2, imports),
                InjuredReserveEnabled = true,
                ReserveListEnabled = true
            });
        var registry = scenario.Registry with { Rulebook = rulebook };
        var camp = new TrainingCamp(
            "camp-executive-ready",
            scenario.ScenarioSnapshot.Organization.OrganizationId,
            scenario.ScenarioSnapshot.CurrentDate,
            Array.Empty<TrainingCampPlayer>(),
            Array.Empty<TrainingCampEvaluation>(),
            CompletedOn: scenario.ScenarioSnapshot.CurrentDate);
        var snapshot = scenario.ScenarioSnapshot with
        {
            TrainingCamp = camp,
            PendingActions = Array.Empty<PendingGmAction>(),
            ProspectRights = Array.Empty<DraftRightsRecord>(),
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true)
        };
        snapshot.Validate();
        return (registry, snapshot);
    }

    private static ExecutiveReportRecord MinimalReport(
        string reportId,
        ExecutiveReportKind kind,
        int seasonYear,
        int health) =>
        new(
            reportId,
            kind,
            new DateTimeOffset(seasonYear, 6, 1, 12, 0, 0, TimeSpan.Zero),
            "org-test",
            "Prairie Falcons",
            "league-test",
            $"season-{seasonYear}",
            seasonYear,
            "Test GM",
            "Test Owner",
            "Previous Report",
            "Baseline",
            health,
            new[] { new ExecutiveReportSection("Summary", new Dictionary<string, string> { ["Overall %"] = health.ToString() }, "Previous season baseline.") },
            "Previous executive report.");

    private static Rulebook WithRosterRules(Rulebook source, RosterRules rosterRules) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = rosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules,
            SeasonRules = source.SeasonRules,
            AffiliateRules = source.AffiliateRules
        };

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
