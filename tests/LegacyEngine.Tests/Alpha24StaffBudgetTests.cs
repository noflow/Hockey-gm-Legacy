using LegacyEngine.Integration;
using LegacyEngine.Owners;
using LegacyEngine.Relationships;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

internal sealed class Alpha24StaffBudgetTests
{
    public void GmSalaryCountedInBudget()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var budget = new StaffBudgetService().Build(scenario.ScenarioSnapshot, scenario.Registry.Rulebook!);

        Assert.True(budget.GmSalary > 0, "GM salary should count against hockey operations budget.");
        Assert.True(budget.UsedBudget >= budget.GmSalary, "Used budget should include GM salary.");
    }

    public void StaffSalaryCountedInBudget()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var budget = new StaffBudgetService().Build(scenario.ScenarioSnapshot, scenario.Registry.Rulebook!);

        Assert.True(budget.CoachingSalaries > 0, "Coaching salaries should be counted.");
        Assert.True(budget.ScoutingSalaries > 0, "Scouting salaries should be counted.");
        Assert.True(budget.StaffTotal >= budget.GmSalary + budget.CoachingSalaries + budget.ScoutingSalaries, "Staff total should include GM, coaching, and scouting salaries.");
    }

    public void HiringStaffIncreasesUsedBudget()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var before = new StaffBudgetService().Build(generated.ScenarioSnapshot, scenario.Registry.Rulebook!);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();

        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);
        var after = new StaffBudgetService().Build(hired.ScenarioSnapshot, scenario.Registry.Rulebook!);

        Assert.True(after.UsedBudget > before.UsedBudget, "Hiring staff should immediately increase used hockey operations budget.");
    }

    public void ReleasingStaffChangesBudgetAndCreatesObligation()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var before = new StaffBudgetService().Build(scenario.ScenarioSnapshot, scenario.Registry.Rulebook!);
        var assistant = scenario.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole == StaffRole.AssistantCoach);

        var released = service.ReleaseStaff(scenario.Registry, scenario.ScenarioSnapshot, assistant.PersonId, "Budget test release.");
        var after = new StaffBudgetService().Build(released.ScenarioSnapshot, scenario.Registry.Rulebook!);

        Assert.True(after.StaffReleaseObligations > 0, "Releasing contracted staff should leave a remaining salary obligation.");
        Assert.True(after.StaffTotal < before.StaffTotal, "Released staff should leave active staff salary total.");
    }

    public void CandidateSalaryDisplayed()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var generated = new StaffOfficeService().GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();
        var source = ReadAlphaDesktopSource();

        Assert.True(candidate.ExpectedSalary.AnnualAmount > 0, "Candidate should have a salary ask.");
        Assert.True(source.Contains("Salary ask", StringComparison.Ordinal), "AlphaDesktop should show candidate salary ask.");
        Assert.True(source.Contains("ExpectedSalary", StringComparison.Ordinal), "AlphaDesktop should bind candidate expected salary.");
    }

    public void OverBudgetWarningGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var owner = scenario.ScenarioSnapshot.AlphaSnapshot.Owner with
        {
            Budget = new OwnerBudget(1m, 0m, 0m, 0m, 0m)
        };
        var snapshot = scenario.ScenarioSnapshot with
        {
            AlphaSnapshot = scenario.ScenarioSnapshot.AlphaSnapshot with { Owner = owner }
        };
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, snapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();

        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);

        Assert.True(hired.InboxItems.Any(item => item.Title.Contains("budget warning", StringComparison.OrdinalIgnoreCase)), "Hiring over budget should create owner warning inbox.");
    }

    public void LeagueSalaryRangesDifferForJuniorAhlAndNhl()
    {
        var service = new StaffBudgetService();
        var junior = service.RangeFor(StaffRole.HeadCoach, RulebookPresets.Create(DraftLeaguePreset.JuniorMajor));
        var ahl = service.RangeFor(StaffRole.HeadCoach, RulebookPresets.Create(DraftLeaguePreset.AhlStyle));
        var nhl = service.RangeFor(StaffRole.HeadCoach, RulebookPresets.Create(DraftLeaguePreset.NhlStyle));
        var juniorGm = service.GmSalaryRange(RulebookPresets.Create(DraftLeaguePreset.JuniorMajor));
        var nhlGm = service.GmSalaryRange(RulebookPresets.Create(DraftLeaguePreset.NhlStyle));

        Assert.Equal(50_000m, junior.Minimum);
        Assert.Equal(125_000m, ahl.Minimum);
        Assert.Equal(1_000_000m, nhl.Minimum);
        Assert.Equal(60_000m, juniorGm.Minimum);
        Assert.Equal(5_000_000m, nhlGm.Maximum);
    }

    public void RelationshipChemistryWarningCanAppear()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var staff = scenario.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole == StaffRole.AssistantCoach);
        var strained = scenario.ScenarioSnapshot.AlphaSnapshot.Relationships
            .Select(relationship => relationship.ToPersonId == staff.PersonId
                ? relationship
                    .ApplyChange(new RelationshipChange(RelationshipDimension.Trust, -100, "Budget test strain.", scenario.ScenarioSnapshot.CurrentDate))
                    .ApplyChange(new RelationshipChange(RelationshipDimension.Confidence, -100, "Budget test strain.", scenario.ScenarioSnapshot.CurrentDate))
                    .ApplyChange(new RelationshipChange(RelationshipDimension.Loyalty, -100, "Budget test strain.", scenario.ScenarioSnapshot.CurrentDate))
                : relationship)
            .ToArray();
        var snapshot = scenario.ScenarioSnapshot with
        {
            AlphaSnapshot = scenario.ScenarioSnapshot.AlphaSnapshot with { Relationships = strained }
        };

        var warning = service.EvaluateChemistry(snapshot, staff.PersonId);

        Assert.True(warning.ConflictWarnings.Count > 0, "Relationship Engine inputs should influence staff chemistry warnings.");
    }

    public void AlphaDesktopExposesHockeyOperationsBudget()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("GM salary", StringComparison.Ordinal), "Budget screen should show GM salary.");
        Assert.True(source.Contains("Coaching salaries", StringComparison.Ordinal), "Budget screen should show coaching salaries.");
        Assert.True(source.Contains("Scouting salaries", StringComparison.Ordinal), "Budget screen should show scouting salaries.");
        Assert.True(source.Contains("Medical/training salaries", StringComparison.Ordinal), "Budget screen should show medical/training salaries.");
        Assert.True(source.Contains("Staff release obligations", StringComparison.Ordinal), "Budget screen should show release obligations.");
    }

    public void NoGodotSaveOrGameSimulationAdded()
    {
        var source = string.Join('\n',
            Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "LegacyEngine should not depend on Godot.");
        Assert.False(source.Contains("GameSimulation", StringComparison.OrdinalIgnoreCase), "Alpha 2.4 should not add game simulation.");
    }

    private static string ReadAlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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
