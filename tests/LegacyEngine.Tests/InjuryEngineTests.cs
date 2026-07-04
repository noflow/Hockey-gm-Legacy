using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Rosters;

internal sealed class InjuryEngineTests
{
    public void InjuryCreation()
    {
        var result = CreateStandardInjury();

        Assert.True(result.Success, "Injury creation should succeed.");
        Assert.Equal("player-001", result.Injury.PersonId);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Injury.InjuryDate);
        Assert.Equal(InjuryStatus.Active, result.Injury.Status);
    }

    public void SeverityStored()
    {
        var result = CreateStandardInjury(severity: InjurySeverity.Major);

        Assert.Equal(InjurySeverity.Major, result.Injury.Severity);
    }

    public void BodyPartStored()
    {
        var result = CreateStandardInjury(bodyPart: InjuryBodyPart.Knee);

        Assert.Equal(InjuryBodyPart.Knee, result.Injury.BodyPart);
    }

    public void ExpectedReturnDateCalculatedAndStored()
    {
        var result = CreateStandardInjury(severity: InjurySeverity.Moderate);

        Assert.Equal(new DateOnly(2026, 10, 22), result.Injury.ExpectedReturnDate);
    }

    public void RecoveryUpdateChangesStatus()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine).Injury;
        var result = engine.ApplyRecoveryUpdate(
            injury,
            new InjuryRecoveryUpdate(new DateOnly(2026, 10, 8), RecoveryProgressDelta: 30));

        Assert.Equal(InjuryStatus.Recovering, result.Injury.Status);
        Assert.Equal(30, result.Injury.RecoveryProgress);
    }

    public void InjuryCanBeCleared()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine).Injury;
        var result = engine.ClearInjury(injury, new DateOnly(2026, 10, 15));

        Assert.Equal(InjuryStatus.Cleared, result.Injury.Status);
        Assert.Equal(100, result.Injury.RecoveryProgress);
    }

    public void ActualReturnDateIsStored()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine).Injury;
        var result = engine.ClearInjury(injury, new DateOnly(2026, 10, 15));

        Assert.Equal(new DateOnly(2026, 10, 15), result.Injury.ActualReturnDate);
    }

    public void GamesMissedCanIncrease()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine).Injury;
        var result = engine.ApplyRecoveryUpdate(
            injury,
            new InjuryRecoveryUpdate(new DateOnly(2026, 10, 8), RecoveryProgressDelta: 10, GamesMissedIncrease: 3));

        Assert.Equal(3, result.Injury.GamesMissed);
    }

    public void ReAggravationChangesStatus()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine).Injury;
        var result = engine.ReAggravateInjury(injury, new DateOnly(2026, 10, 9));

        Assert.Equal(InjuryStatus.ReAggravated, result.Injury.Status);
    }

    public void RecurrenceRiskIsTracked()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine, recurrenceRisk: 25).Injury;
        var result = engine.ReAggravateInjury(injury, new DateOnly(2026, 10, 9), recurrenceRiskIncrease: 15);

        Assert.Equal(40, result.Injury.RecurrenceRisk);
        Assert.Equal(40, engine.CreateRiskProfile(result.Injury).RecurrenceRisk);
    }

    public void LongTermImpactIsTracked()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine, longTermImpact: 10).Injury;
        var result = engine.ApplyRecoveryUpdate(
            injury,
            new InjuryRecoveryUpdate(new DateOnly(2026, 10, 8), RecoveryProgressDelta: 10, LongTermImpactDelta: 12));

        Assert.Equal(22, result.Injury.LongTermImpact);
        Assert.True(result.Injury.DevelopmentPenalty > 0, "Injury should expose a development penalty for later development use.");
    }

    public void CareerThreateningInjuryCanBeMarked()
    {
        var engine = new InjuryEngine();
        var injury = CreateStandardInjury(engine: engine).Injury;
        var result = engine.MarkCareerThreatening(injury, new DateOnly(2026, 10, 10));

        Assert.Equal(InjurySeverity.CareerThreatening, result.Injury.Severity);
        Assert.Equal(InjuryStatus.CareerThreatening, result.Injury.Status);
    }

    public void InjuryResultIncludesSummary()
    {
        var result = CreateStandardInjury();

        Assert.True(!string.IsNullOrWhiteSpace(result.Summary), "Injury result should include summary text.");
    }

    public void EventsCreatedForInjuryRecoveryReAggravationCareerThreatening()
    {
        var eventEngine = new EventEngine();
        var engine = new InjuryEngine(eventEngine);
        var injury = CreateStandardInjury(engine: engine).Injury;
        injury = engine.ClearInjury(injury, new DateOnly(2026, 10, 15)).Injury;
        injury = engine.ReAggravateInjury(injury, new DateOnly(2026, 10, 18)).Injury;
        engine.MarkCareerThreatening(injury, new DateOnly(2026, 10, 19));

        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerInjured), "Injury event should be queued.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerRecovered), "Recovery event should be queued.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.InjuryReAggravated), "Re-aggravation event should be queued.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.InjuryCareerThreatening), "Career-threatening event should be queued.");
    }

    public void NoRosterMovementOccursAutomatically()
    {
        var rosterEngine = new RosterEngine();
        var roster = rosterEngine.CreateRoster("roster-001", "org-001");
        roster = rosterEngine.AddPlayer(
            roster,
            new RosterMove(
                RosterMoveType.Add,
                "player-001",
                new DateOnly(2026, 9, 1),
                Position: RosterPosition.Center,
                TargetStatus: RosterStatus.Active,
                Age: 18)).Roster;

        CreateStandardInjury(personId: "player-001");

        Assert.Equal(RosterStatus.Active, roster.FindPlayer("player-001")!.Status);
    }

    public void NoUiOrGodotDependencyExists()
    {
        var injuryFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Injuries"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in injuryFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Injury module should not reference Godot.");
            Assert.False(text.Contains("Control", StringComparison.Ordinal), "Injury module should not define UI controls.");
        }
    }

    private static InjuryResult CreateStandardInjury(
        InjuryEngine? engine = null,
        string personId = "player-001",
        InjuryBodyPart bodyPart = InjuryBodyPart.Shoulder,
        InjuryType injuryType = InjuryType.Sprain,
        InjurySeverity severity = InjurySeverity.Moderate,
        int recurrenceRisk = 0,
        int longTermImpact = 0) =>
        (engine ?? new InjuryEngine()).CreateInjury(
            personId,
            new DateOnly(2026, 10, 1),
            bodyPart,
            injuryType,
            severity,
            recurrenceRisk: recurrenceRisk,
            longTermImpact: longTermImpact,
            injuryId: "injury-001");

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
