using LegacyEngine.Integration;

internal sealed class Alpha46StaffCoachingV3Tests
{
    public void CoachPhilosophyIsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profile = new StaffCoachingService().BuildCoachProfiles(scenario).First();

        Assert.True(Enum.IsDefined(profile.Philosophy), "Coach philosophy should be generated.");
        Assert.True(profile.PhilosophySummary.Contains(profile.Philosophy.ToString(), StringComparison.Ordinal), "Philosophy summary should name the philosophy.");
    }

    public void CoachSpecialtiesAndPersonalityAreGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profile = new StaffCoachingService().BuildCoachProfiles(scenario).First();

        Assert.True(profile.Specialties.Count > 0, "Coach specialties should be generated.");
        Assert.True(Enum.IsDefined(profile.Personality), "Coach personality should be generated.");
    }

    public void StaffChemistryUsesRelationships()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var links = new StaffCoachingService().BuildStaffChemistry(scenario);

        Assert.True(links.Count > 0, "Staff chemistry should produce relationship links or a staff-room baseline.");
        Assert.True(links.All(link => link.Trust is >= 0 and <= 100), "Chemistry trust should be bounded.");
    }

    public void PlayerCoachFitIsExplainable()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var fit = new StaffCoachingService().EvaluatePlayerFit(scenario, playerId);

        Assert.Equal(playerId, fit.PersonId);
        Assert.True(fit.Reasons.Count > 0, "Player fit should include reasons.");
        Assert.False(fit.Summary.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase), "Coach fit must not expose hidden ratings.");
        Assert.False(fit.Summary.Contains("Potential =", StringComparison.OrdinalIgnoreCase), "Coach fit must not expose hidden ratings.");
    }

    public void MonthlyStaffMeetingProducesRecommendations()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var report = new StaffCoachingService().GenerateMonthlyMeetingReport(scenario);

        Assert.True(report.Recommendations.Count > 0, "Monthly staff meeting should include recommendations.");
        Assert.True(report.DevelopmentNotes.Count > 0, "Monthly staff meeting should include development notes.");
        Assert.True(report.RosterNotes.Count > 0, "Monthly staff meeting should include roster notes.");
    }

    public void DepartmentGradesAreGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var grades = new StaffCoachingService().BuildDepartmentGrades(scenario);

        Assert.Contains("Coaching", grades.Select(grade => grade.DepartmentName));
        Assert.Contains("Development", grades.Select(grade => grade.DepartmentName));
        Assert.Contains("Scouting", grades.Select(grade => grade.DepartmentName));
        Assert.Contains("Medical", grades.Select(grade => grade.DepartmentName));
    }

    public void OrganizationChartIsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var chart = new StaffCoachingService().BuildOrganizationChart(scenario);

        Assert.True(chart.Any(node => node.Role == "Owner"), "Organization chart should include owner.");
        Assert.True(chart.Any(node => node.Role == "General Manager"), "Organization chart should include GM.");
        Assert.True(chart.Any(node => node.Role.Contains("Coach", StringComparison.Ordinal)), "Organization chart should include coaching staff.");
    }

    public void StaffPerformanceReviewIsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var staffId = scenario.StaffMembers.First().PersonId;
        var review = new StaffCoachingService().BuildPerformanceReview(scenario, staffId);

        Assert.Equal(staffId, review.PersonId);
        Assert.True(!string.IsNullOrWhiteSpace(review.Recommendation), "Performance review should include a recommendation.");
    }

    public void HiringFitConsidersSalaryAndChemistry()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var office = new StaffOfficeService();
        var candidateResult = office.GenerateCandidatePool(scenarioResult.Registry, scenarioResult.ScenarioSnapshot);
        var candidate = candidateResult.ScenarioSnapshot.StaffCandidates.First();
        var fit = new StaffCoachingService().EvaluateHiringFit(candidateResult.ScenarioSnapshot, candidate);

        Assert.Equal(candidate.CandidateId, fit.CandidateId);
        Assert.True(fit.SalaryImpact.Contains("$", StringComparison.Ordinal), "Hiring fit should include salary impact.");
        Assert.True(fit.Reasons.Count > 0, "Hiring fit should include reasons.");
    }

    public void PlayerDossierIncludesStaffOpinions()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);
        var staffSection = dossier.Sections.Single(section => section.Title == "Staff Opinions");
        var text = string.Join(" ", staffSection.Lines);

        Assert.True(text.Contains("Head coach/development fit", StringComparison.Ordinal), "Dossier should include coach/development fit.");
        Assert.True(text.Contains("Scout view", StringComparison.Ordinal), "Dossier should include scout view.");
        Assert.True(text.Contains("Medical view", StringComparison.Ordinal), "Dossier should include medical view.");
    }

    public void ActionCenterIncludesStaffCoachingReview()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = scenarioResult.ScenarioSnapshot;
        var budget = new BudgetOverviewService().Build(scenario, scenarioResult.Registry.Rulebook!);
        var readiness = new SeasonReadinessService().Evaluate(scenarioResult.Registry, scenario);
        var vacancies = new StaffOfficeService().BuildVacancies(scenario, scenarioResult.Registry.Rulebook!);
        var items = new ActionCenterService().BuildItems(scenario, Array.Empty<InboxMessage>(), budget, readiness, vacancies);

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Staff && item.Title.Contains("staff meeting", StringComparison.OrdinalIgnoreCase)), "Action Center should include staff coaching review.");
    }

    public void AlphaDesktopExposesStaffCoachingUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Coaching Philosophy", StringComparison.Ordinal), "AlphaDesktop should expose coaching philosophy.");
        Assert.True(source.Contains("Staff Chemistry", StringComparison.Ordinal), "AlphaDesktop should expose staff chemistry.");
        Assert.True(source.Contains("Department Grades", StringComparison.Ordinal), "AlphaDesktop should expose department grades.");
        Assert.True(source.Contains("Organization Chart", StringComparison.Ordinal), "AlphaDesktop should expose organization chart.");
        Assert.True(source.Contains("Performance Review", StringComparison.Ordinal), "AlphaDesktop should expose performance review.");
    }

    public void NoGodotSaveOrGameSimulationChanges()
    {
        var serviceSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "StaffCoachingService.cs"));
        Assert.False(serviceSource.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Staff coaching service should not reference Godot.");
        Assert.False(serviceSource.Contains("SaveGame", StringComparison.OrdinalIgnoreCase), "Staff coaching service should not change save/load.");
        Assert.False(serviceSource.Contains("BasicGameSimulator", StringComparison.OrdinalIgnoreCase), "Staff coaching service should not change game simulation.");
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
