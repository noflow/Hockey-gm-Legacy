using LegacyEngine.Integration;
using LegacyEngine.Injuries;

internal sealed class Alpha47InjuryMedicalV2Tests
{
    public void HealthProfileGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var profile = new MedicalHealthService().BuildHealthProfile(scenario, playerId);

        Assert.Equal(playerId, profile.PersonId);
        Assert.True(profile.Durability is >= 0 and <= 100, "Durability should be bounded.");
        Assert.True(profile.MedicalConfidence is >= 0 and <= 100, "Medical confidence should be bounded.");
    }

    public void RecurringInjuriesIncreaseRisk()
    {
        var scenario = ScenarioWithRecurringInjury();
        var playerId = scenario.AlphaSnapshot.Injuries.First().PersonId;
        var profile = new MedicalHealthService().BuildHealthProfile(scenario, playerId);

        Assert.True(profile.RecurringInjuryRisk >= 60, "Knee/concussion/back style injuries should raise recurring risk.");
        Assert.True(profile.RecurringConcerns.Any(item => item.Contains("Knee", StringComparison.Ordinal) || item.Contains("Concussion", StringComparison.Ordinal)), "Recurring concern should name injury area/type.");
    }

    public void MedicalReportExplainsWhy()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Injuries.First().PersonId;
        var report = new MedicalHealthService().BuildMedicalReport(scenario, playerId);

        Assert.True(report.WhyItMatters.Contains(report.PlayerName, StringComparison.Ordinal), "Medical report should name player in explanation.");
        Assert.True(report.ReturnRecommendation.Length > 10, "Medical report should include return recommendation.");
        Assert.True(report.AvailableOptions.Count > 0, "Medical report should include return options.");
    }

    public void ReturnDecisionCanClearPlayer()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var playerId = scenarioResult.ScenarioSnapshot.AlphaSnapshot.Injuries.First().PersonId;
        var result = new MedicalHealthService().ApplyReturnDecision(
            scenarioResult.Registry,
            scenarioResult.ScenarioSnapshot,
            playerId,
            ReturnToPlayOption.MedicalClearance);

        Assert.True(result.Success, result.Message);
        Assert.False(result.ScenarioSnapshot.AlphaSnapshot.Injuries.First(injury => injury.PersonId == playerId).IsActive, "Medical clearance should clear active injury.");
    }

    public void EarlyReturnRaisesRisk()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var playerId = scenarioResult.ScenarioSnapshot.AlphaSnapshot.Injuries.First().PersonId;
        var before = scenarioResult.ScenarioSnapshot.AlphaSnapshot.Injuries.First(injury => injury.PersonId == playerId).RecurrenceRisk;
        var result = new MedicalHealthService().ApplyReturnDecision(
            scenarioResult.Registry,
            scenarioResult.ScenarioSnapshot,
            playerId,
            ReturnToPlayOption.ReturnImmediately);
        var after = result.ScenarioSnapshot.AlphaSnapshot.Injuries.First(injury => injury.PersonId == playerId).RecurrenceRisk;

        Assert.True(after > before, "Returning immediately should raise recurrence risk.");
    }

    public void ConditioningDecisionUpdatesPlan()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var playerId = scenarioResult.ScenarioSnapshot.AlphaSnapshot.Injuries.First().PersonId;
        var result = new MedicalHealthService().ApplyReturnDecision(
            scenarioResult.Registry,
            scenarioResult.ScenarioSnapshot,
            playerId,
            ReturnToPlayOption.ConditioningAssignment);
        var injury = result.ScenarioSnapshot.AlphaSnapshot.Injuries.First(injury => injury.PersonId == playerId);

        Assert.True(injury.RecoveryPlan.Summary.Contains("Conditioning", StringComparison.OrdinalIgnoreCase), "Conditioning decision should update recovery plan.");
    }

    public void MedicalStaffInfluencesConfidence()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var confidence = new MedicalHealthService().BuildHealthProfile(scenario, playerId).MedicalConfidence;

        Assert.True(confidence >= 45, "Medical staff should provide at least baseline confidence.");
    }

    public void MedicalSummaryIncludesBudgetAndDepartmentGrade()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var summary = new MedicalHealthService().BuildMedicalSummary(scenario);

        Assert.True(summary.MedicalDepartmentGrade.Length > 0, "Medical summary should include department grade.");
        Assert.True(summary.MedicalBudgetImpact.Contains("Medical/training salaries", StringComparison.Ordinal), "Medical summary should include budget impact.");
    }

    public void PlayerDossierIncludesMedicalV2()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var playerId = scenario.AlphaSnapshot.Injuries.First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);
        var section = dossier.Sections.Single(item => item.Title == "Injuries / Medical");
        var text = string.Join(" ", section.Lines);

        Assert.True(text.Contains("Current Health", StringComparison.Ordinal), "Dossier should include health status.");
        Assert.True(text.Contains("Return recommendation", StringComparison.Ordinal), "Dossier should include return recommendation.");
        Assert.True(text.Contains("Recurring concerns", StringComparison.Ordinal), "Dossier should include recurring concerns.");
    }

    public void ActionCenterIncludesMedicalV2Items()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = scenarioResult.ScenarioSnapshot;
        var items = new ActionCenterService().BuildItems(
            scenario,
            Array.Empty<InboxMessage>(),
            new BudgetOverviewService().Build(scenario, scenarioResult.Registry.Rulebook!),
            new SeasonReadinessService().Evaluate(scenarioResult.Registry, scenario),
            new StaffOfficeService().BuildVacancies(scenario, scenarioResult.Registry.Rulebook!));

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.Medical && item.Title.Contains("Medical", StringComparison.OrdinalIgnoreCase)), "Action Center should include medical review items.");
    }

    public void ExecutiveMedicalReportIncludesV2Fields()
    {
        var scenarioResult = NewGmScenarioBootstrapper.CreateScenario();
        var completed = scenarioResult.ScenarioSnapshot with
        {
            Season = scenarioResult.ScenarioSnapshot.Season with { Status = LegacyEngine.Seasons.SeasonStatus.Completed }
        };
        var report = new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(scenarioResult.Registry, completed).Report!;
        var section = report.FindSection("Medical Report");

        Assert.True(section is not null, "End-season report should include medical report.");
        Assert.True(section!.Items.ContainsKey("Medical Department Grade"), "Medical report should include department grade.");
        Assert.True(section.Items.ContainsKey("Medical Budget"), "Medical report should include medical budget.");
    }

    public void AlphaDesktopExposesMedicalV2Ui()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Health & Medical", StringComparison.Ordinal), "Desktop should expose health panel.");
        Assert.True(source.Contains("Medical Report", StringComparison.Ordinal), "Desktop should expose medical report.");
        Assert.True(source.Contains("Return Now", StringComparison.Ordinal), "Desktop should expose return decision.");
        Assert.True(source.Contains("Conditioning", StringComparison.Ordinal), "Desktop should expose conditioning decision.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "MedicalHealthService.cs"));
        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Medical v2 should not reference Godot.");
        Assert.False(source.Contains("SaveGame", StringComparison.OrdinalIgnoreCase), "Medical v2 should not change save/load.");
        Assert.False(source.Contains("BasicGameSimulator", StringComparison.OrdinalIgnoreCase), "Medical v2 should not change game simulation.");
    }

    private static NewGmScenarioSnapshot ScenarioWithRecurringInjury()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var injury = scenario.AlphaSnapshot.Injuries.First();
        var recurring = injury with
        {
            BodyPart = InjuryBodyPart.Knee,
            InjuryType = InjuryType.Concussion,
            RecurrenceRisk = 75,
            LongTermImpact = 45
        };
        return scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                Injuries = scenario.AlphaSnapshot.Injuries.Select(item => item.InjuryId == injury.InjuryId ? recurring : item).ToArray()
            }
        };
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
