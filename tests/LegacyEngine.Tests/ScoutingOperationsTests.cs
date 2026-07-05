using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Relationships;
using LegacyEngine.Scouting;

internal sealed class ScoutingOperationsTests
{
    public void ScoutCanBeAssignedToRegion()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var scout = RegionalScoutPersonId(scenario.ScenarioSnapshot);
        var result = new ScoutingOperationsService().AssignScoutToRegion(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            scout,
            ScoutingRegionFocus.WesternCanada,
            ScoutingOperationPriority.High,
            "Cover Western Canada before draft day.");

        Assert.True(result.Success, result.Message);
        Assert.Equal(ScoutingOperationAssignmentType.Region, result.Assignment!.AssignmentType);
        Assert.Equal(ScoutingRegionFocus.WesternCanada, result.Assignment.TargetRegion);
    }

    public void ScoutCanBeAssignedToPlayer()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var scout = RegionalScoutPersonId(scenario.ScenarioSnapshot);
        var player = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var result = new ScoutingOperationsService().AssignScoutToPlayer(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            scout,
            player,
            ScoutingOperationPriority.High,
            "Get one more live viewing.");

        Assert.True(result.Success, result.Message);
        Assert.Equal(ScoutingOperationAssignmentType.Player, result.Assignment!.AssignmentType);
        Assert.Equal(player, result.Assignment.TargetPlayerId);
    }

    public void AssignmentStoresPriorityNotesAndDates()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var due = scenario.ScenarioSnapshot.CurrentDate.AddDays(4);
        var result = new ScoutingOperationsService().AssignScoutToRegion(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            RegionalScoutPersonId(scenario.ScenarioSnapshot),
            ScoutingRegionFocus.EasternCanada,
            ScoutingOperationPriority.Urgent,
            "Urgent eastern swing.",
            due);

        Assert.Equal(ScoutingOperationPriority.Urgent, result.Assignment!.Priority);
        Assert.Equal("Urgent eastern swing.", result.Assignment.Notes);
        Assert.Equal(scenario.ScenarioSnapshot.CurrentDate, result.Assignment.StartDate);
        Assert.Equal(due, result.Assignment.ExpectedReportDate);
    }

    public void AssignmentProgressesOverDays()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new ScoutingOperationsService();
        var assigned = service.AssignScoutToRegion(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            RegionalScoutPersonId(scenario.ScenarioSnapshot),
            ScoutingRegionFocus.WesternCanada,
            ScoutingOperationPriority.High,
            "Progress check.",
            scenario.ScenarioSnapshot.CurrentDate.AddDays(1));
        var advanced = new DailySimulationCoordinator().AdvanceScenarioOneDay(scenario.Registry, assigned.ScenarioSnapshot);

        Assert.True(advanced.ScenarioSnapshot.ScoutingOperations.Any(item => item.Status == ScoutingOperationStatus.Completed), "Assignment should complete after the expected report date.");
    }

    public void CompletedAssignmentCreatesReport()
    {
        var result = CompleteSingleRegionAssignment(ScoutingRegionFocus.WesternCanada);

        Assert.True(result.ScenarioSnapshot.CompletedScoutingReports.Count > 0, "Completed assignment should create report.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.ScoutAssignmentCompleted), "Completed assignment should create inbox item.");
    }

    public void RegionFitImprovesConfidence()
    {
        var good = CompleteSingleRegionAssignment(ScoutingRegionFocus.Character);
        var poor = CompleteSingleRegionAssignment(ScoutingRegionFocus.Medical);
        var goodScore = ConfidenceScore(good.ScenarioSnapshot.CompletedScoutingReports.Last().Confidence);
        var poorScore = ConfidenceScore(poor.ScenarioSnapshot.CompletedScoutingReports.Last().Confidence);

        Assert.True(goodScore > poorScore, $"Good fit confidence {goodScore} should beat poor fit confidence {poorScore}.");
    }

    public void HeavyWorkloadDelaysReport()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new ScoutingOperationsService();
        var scout = RegionalScoutPersonId(scenario.ScenarioSnapshot);
        var current = scenario.ScenarioSnapshot;
        for (var index = 0; index < 3; index++)
        {
            current = service.AssignScoutToRegion(
                scenario.Registry,
                current,
                scout,
                ScoutingRegionFocus.WesternCanada,
                ScoutingOperationPriority.High,
                $"Workload test {index}.",
                current.CurrentDate).ScenarioSnapshot;
        }

        var progressed = service.AdvanceAssignments(scenario.Registry, current);

        Assert.True(progressed.ScenarioSnapshot.ScoutingOperations.Any(item => item.Status == ScoutingOperationStatus.Delayed), "Heavy workload should delay at least one assignment.");
    }

    public void PoorRelationshipAffectsCommunicationQuality()
    {
        var scenario = WithPoorScoutRelationship(NewGmScenarioBootstrapper.CreateScenario());
        var result = new ScoutingOperationsService().AssignScoutToRegion(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            RegionalScoutPersonId(scenario.ScenarioSnapshot),
            ScoutingRegionFocus.WesternCanada,
            ScoutingOperationPriority.High,
            "Relationship test.");

        Assert.True(result.Assignment!.CommunicationQuality < 50, "Poor GM relationship should reduce communication quality.");
    }

    public void StaffRelationshipWarningCanBeGenerated()
    {
        var scenario = WithPoorScoutRelationship(NewGmScenarioBootstrapper.CreateScenario());
        var result = new ScoutingOperationsService().GenerateStaffConflictWarning(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.StaffConflictWarning), "Staff conflict warning should create inbox item.");
    }

    public void StaffRoleCanBeReassigned()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var staff = scenario.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole == LegacyEngine.Staff.StaffRole.AssistantCoach);
        var result = new ScoutingOperationsService().ReassignStaffRole(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            staff.PersonId,
            LegacyEngine.Staff.StaffRole.DevelopmentCoach);

        Assert.True(result.Success, result.Message);
        Assert.Equal(LegacyEngine.Staff.StaffRole.DevelopmentCoach, result.ScenarioSnapshot.StaffMembers.Single(member => member.PersonId == staff.PersonId).CurrentRole);
    }

    public void StaffCanBeReleased()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var staff = scenario.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole == LegacyEngine.Staff.StaffRole.AssistantCoach);
        var result = new ScoutingOperationsService().ReleaseStaff(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            staff.PersonId,
            "Testing staff control.");

        Assert.True(result.Success, result.Message);
        Assert.Equal(LegacyEngine.Staff.StaffEmploymentStatus.Released, result.ScenarioSnapshot.StaffMembers.Single(member => member.PersonId == staff.PersonId).EmploymentStatus);
    }

    public void PlaceholderStaffCandidateCanBeHired()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var before = scenario.ScenarioSnapshot.StaffMembers.Count;
        var result = new ScoutingOperationsService().HirePlaceholderStaffCandidate(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            LegacyEngine.Staff.StaffRole.Scout);

        Assert.True(result.Success, result.Message);
        Assert.Equal(before + 1, result.ScenarioSnapshot.StaffMembers.Count);
        Assert.True(result.ScenarioSnapshot.StaffMembers.Any(member => member.CurrentRole == LegacyEngine.Staff.StaffRole.Scout && member.Profile.Reputation == 42), "Placeholder scout should be hired.");
    }

    public void AlphaDesktopExposesScoutAssignmentUi()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Scouting Operations", StringComparison.Ordinal), "AlphaDesktop should expose Scouting Operations.");
        Assert.True(text.Contains("Scout list on left", StringComparison.Ordinal), "AlphaDesktop should describe scout selection.");
        Assert.True(text.Contains("Assign Region", StringComparison.Ordinal), "AlphaDesktop should expose region assignment.");
        Assert.True(text.Contains("Assign Player", StringComparison.Ordinal), "AlphaDesktop should expose player assignment.");
        Assert.True(text.Contains("priority, notes, Assign button", StringComparison.Ordinal), "AlphaDesktop should expose assignment controls.");
        Assert.True(text.Contains("Reassign Staff", StringComparison.Ordinal), "AlphaDesktop should expose staff reassignment.");
        Assert.True(text.Contains("Release Staff", StringComparison.Ordinal), "AlphaDesktop should expose staff release.");
        Assert.True(text.Contains("Hire Staff", StringComparison.Ordinal), "AlphaDesktop should expose placeholder staff hire.");
    }

    public void ScoutingOperationsHaveNoGodotSaveOrGameSimulationDependency()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "ScoutingOperation*.cs");
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Scouting operations should not depend on Godot.");
        Assert.False(text.Contains("Save", StringComparison.Ordinal), "Scouting operations should not implement save/load.");
        Assert.False(text.Contains("GameSimulation", StringComparison.Ordinal), "Scouting operations should not implement game simulation.");
    }

    private static ScoutingOperationResult CompleteSingleRegionAssignment(ScoutingRegionFocus region)
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new ScoutingOperationsService();
        var assigned = service.AssignScoutToRegion(
            scenario.Registry,
            scenario.ScenarioSnapshot,
            RegionalScoutPersonId(scenario.ScenarioSnapshot),
            region,
            ScoutingOperationPriority.High,
            $"Complete {region}.",
            scenario.ScenarioSnapshot.CurrentDate);
        return service.AdvanceAssignments(scenario.Registry, assigned.ScenarioSnapshot);
    }

    private static NewGmScenarioResult WithPoorScoutRelationship(NewGmScenarioResult scenario)
    {
        var scoutId = RegionalScoutPersonId(scenario.ScenarioSnapshot);
        var gmId = scenario.ScenarioSnapshot.AlphaSnapshot.GeneralManager.PersonId;
        var relationships = scenario.ScenarioSnapshot.AlphaSnapshot.Relationships
            .Select(relationship => relationship.FromPersonId == gmId && relationship.ToPersonId == scoutId
                ? relationship with { Trust = 20, Respect = 35, Confidence = 25, Loyalty = 25 }
                : relationship)
            .ToArray();
        var alpha = scenario.ScenarioSnapshot.AlphaSnapshot with { Relationships = relationships };
        var snapshot = scenario.ScenarioSnapshot with { AlphaSnapshot = alpha };
        return scenario with
        {
            AlphaSnapshot = alpha,
            ScenarioSnapshot = snapshot
        };
    }

    private static string RegionalScoutPersonId(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers
            .Where(member => member.CurrentRole == LegacyEngine.Staff.StaffRole.Scout)
            .Select(member => member.PersonId)
            .First();

    private static int ConfidenceScore(ScoutingConfidenceLevel confidence) =>
        confidence switch
        {
            ScoutingConfidenceLevel.VeryHigh => 5,
            ScoutingConfidenceLevel.High => 4,
            ScoutingConfidenceLevel.Medium => 3,
            ScoutingConfidenceLevel.Low => 2,
            _ => 1
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
