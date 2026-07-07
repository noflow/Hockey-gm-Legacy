using LegacyEngine.Integration;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;

internal sealed class Alpha44ScoutingV2Tests
{
    public void MultipleReportsCanExistForPlayer()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new ScoutingIntelligenceService();
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var scouts = ScoutingStaff(scenario).Take(2).ToArray();

        var head = service.CreateReport(scenario, playerId, scouts[0].PersonId, 5, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.FiveGameSample);
        var regional = service.CreateReport(scenario, playerId, scouts[1].PersonId, 5, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.FiveGameSample);

        Assert.True(head.ScoutId != regional.ScoutId, "Different scouts should be able to file separate reports on the same player.");
        Assert.True(head.CurrentPicture != regional.CurrentPicture || head.Recommendation != regional.Recommendation, "Reports should not always agree.");
    }

    public void ScoutPersonalitiesGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profiles = new ScoutingIntelligenceService().BuildScoutProfiles(scenario);

        Assert.True(profiles.Count > 0, "Scouting v2 should build scout intelligence profiles.");
        Assert.True(profiles.All(profile => profile.Traits.Count > 0), "Each scout should have tendencies.");
    }

    public void ConfidenceImprovesWithViewings()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new ScoutingIntelligenceService();
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var scoutId = ScoutingStaff(scenario)[0].PersonId;

        var oneGame = service.CreateReport(scenario, playerId, scoutId, 1, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.SingleGame);
        var fifteenGames = service.CreateReport(scenario, playerId, scoutId, 15, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.FifteenGameSample);

        Assert.True(fifteenGames.Confidence >= oneGame.Confidence, "More viewings should not reduce confidence.");
        Assert.True(fifteenGames.ConfidenceStars.Contains("*", StringComparison.Ordinal), "Confidence should have a star-style display.");
    }

    public void ViewingsImproveReportDetail()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new ScoutingIntelligenceService();
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var scoutId = ScoutingStaff(scenario)[0].PersonId;

        var report = service.CreateReport(scenario, playerId, scoutId, 15, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.FifteenGameSample);

        Assert.True(report.Evidence.Any(item => item.Contains("15 viewing", StringComparison.OrdinalIgnoreCase)), "Report should preserve viewing sample context.");
    }

    public void RegionalBonusImprovesConfidence()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new ScoutingIntelligenceService();
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var scout = ScoutingStaff(scenario)[0];
        var profile = service.BuildScoutProfile(scenario, scout);
        var knownRegion = profile.KnownRegions[0];
        var otherRegion = Enum.GetValues<ScoutingRegionFocus>().First(region => !profile.KnownRegions.Contains(region));

        var fit = service.CreateReport(scenario, playerId, scout.PersonId, 5, knownRegion, ScoutingViewingType.FiveGameSample);
        var miss = service.CreateReport(scenario, playerId, scout.PersonId, 5, otherRegion, ScoutingViewingType.FiveGameSample);

        Assert.True(fit.Confidence >= miss.Confidence, "Known-region fit should improve or preserve confidence.");
    }

    public void TournamentScoutingAddsPressureContext()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new ScoutingIntelligenceService();
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var scoutId = ScoutingStaff(scenario)[0].PersonId;

        var report = service.CreateReport(scenario, playerId, scoutId, 4, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.Tournament, ScoutingTournamentType.U18Championships);

        Assert.True(report.Evidence.Any(item => item.Contains("pressure", StringComparison.OrdinalIgnoreCase)), "Tournament reports should include pressure context.");
        Assert.True(report.Tournament == ScoutingTournamentType.U18Championships, "Tournament type should be stored.");
    }

    public void ScoutHistoryGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var scoutId = ScoutingStaff(scenario)[0].PersonId;
        var career = new ScoutingIntelligenceService().BuildScoutCareer(scenario, scoutId);

        Assert.True(!string.IsNullOrWhiteSpace(career.Summary), "Scout career snapshot should summarize discoveries and history.");
        Assert.True(career.ExperiencePoints >= 0, "Scout career should track experience.");
    }

    public void ScoutExperienceGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var scoutId = ScoutingStaff(scenario)[0].PersonId;
        var update = new ScoutingIntelligenceService().BuildScoutDevelopment(scenario, scoutId);

        Assert.True(update.ExperienceGained > 0, "Scout development should gain experience from scouting work.");
        Assert.True(!string.IsNullOrWhiteSpace(update.NewSpecialization), "Scout development should identify a specialization path.");
    }

    public void ScoutWorkloadReducesConfidence()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var scout = ScoutingStaff(scenario)[0];
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var busy = scenario with
        {
            ScoutingOperations = Enumerable.Range(1, 3)
                .Select(index => ActiveAssignment(scout, scenario, index))
                .ToArray()
        };
        var service = new ScoutingIntelligenceService();

        var normal = service.CreateReport(scenario, playerId, scout.PersonId, 5, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.FiveGameSample);
        var heavy = service.CreateReport(busy, playerId, scout.PersonId, 5, ScoutingRegionFocus.WesternCanada, ScoutingViewingType.FiveGameSample);

        Assert.True(normal.Confidence >= heavy.Confidence, "Heavy workload should reduce or hold down confidence.");
        Assert.True(heavy.WorkloadNote.Contains("heavy", StringComparison.OrdinalIgnoreCase), "Heavy workload should be explained.");
    }

    public void ReportComparisonGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var comparison = new ScoutingIntelligenceService().CompareReports(scenario, playerId);

        Assert.True(comparison.Reports.Count > 0, "Comparison should include reports.");
        Assert.True(comparison.Agreements.Count > 0, "Comparison should explain agreements.");
        Assert.True(comparison.Disagreements.Count > 0, "Comparison should explain disagreements.");
    }

    public void PlayerDossierUsesScoutingV2Language()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.DraftBoard.Entries[0].ProspectPersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);
        var text = string.Join("\n", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Confidence:", StringComparison.Ordinal), "Dossier should show scout confidence.");
        Assert.True(text.Contains("Scout tendency", StringComparison.Ordinal), "Dossier should show scout tendencies.");
        Assert.False(text.Contains("CurrentAbility", StringComparison.Ordinal), "Dossier must not expose hidden ratings.");
        Assert.False(text.Contains("Potential =", StringComparison.Ordinal), "Dossier must not expose hidden potential.");
    }

    public void BudgetEffectsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var impact = new ScoutingIntelligenceService().BuildBudgetImpact(scenario);

        Assert.True(impact.ScoutingBudget >= 0, "Budget impact should include scouting budget.");
        Assert.True(!string.IsNullOrWhiteSpace(impact.TournamentCoverage), "Budget impact should explain tournament coverage.");
        Assert.True(!string.IsNullOrWhiteSpace(impact.InternationalCoverage), "Budget impact should explain international coverage.");
    }

    public void AlphaDesktopExposesScoutingV2Ui()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Scouting v2 is intelligence", StringComparison.Ordinal), "AlphaDesktop should describe scouting v2.");
        Assert.True(source.Contains("Scout Again", StringComparison.Ordinal), "AlphaDesktop should expose Scout Again.");
        Assert.True(source.Contains("Tournament", StringComparison.Ordinal), "AlphaDesktop should expose Tournament scouting.");
        Assert.True(source.Contains("Compare Reports", StringComparison.Ordinal), "AlphaDesktop should expose report comparison.");
    }

    public void Alpha44HasNoHiddenRatingsGodotOrGameSimulation()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "*Scouting*.cs", SearchOption.TopDirectoryOnly)
            .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 4.4 should not add Godot.");
        Assert.False(text.Contains("GameSimulator", StringComparison.Ordinal), "Alpha 4.4 should not add game simulation.");
        Assert.False(text.Contains("CurrentAbilityEstimate", StringComparison.Ordinal), "Scouting v2 integration should not expose current ability estimates.");
    }

    private static IReadOnlyList<StaffMember> ScoutingStaff(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers
            .Where(member => member.Department == StaffDepartment.Scouting && member.EmploymentStatus == StaffEmploymentStatus.Employed)
            .ToArray();

    private static ScoutingOperationAssignment ActiveAssignment(StaffMember scout, NewGmScenarioSnapshot scenario, int index) =>
        new(
            AssignmentId: $"test-scouting-workload-{index}",
            ScoutPersonId: scout.PersonId,
            ScoutName: scenario.AlphaSnapshot.People.First(person => person.PersonId == scout.PersonId).Identity.DisplayName,
            AssignmentType: ScoutingOperationAssignmentType.Player,
            TargetRegion: null,
            TargetPlayerId: scenario.AlphaSnapshot.DraftBoard.Entries[index].ProspectPersonId,
            TargetName: "Workload target",
            StartDate: scenario.CurrentDate,
            ExpectedReportDate: scenario.CurrentDate.AddDays(3),
            Priority: ScoutingOperationPriority.High,
            Notes: "Workload test.",
            Status: ScoutingOperationStatus.Active,
            WorkloadAtAssignment: index,
            RelationshipQualityAtAssignment: 60,
            CommunicationQuality: 60,
            DurationDays: 3,
            ReturnDate: scenario.CurrentDate.AddDays(3));

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
