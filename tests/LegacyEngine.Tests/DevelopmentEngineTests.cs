using LegacyEngine.Development;
using LegacyEngine.Events;

internal sealed class DevelopmentEngineTests
{
    public void DevelopmentProfileCreation()
    {
        var profile = BuildProfile();

        profile.Validate();
        Assert.Equal("player-001", profile.PersonId);
        Assert.Equal(45, profile.CurrentAbility);
        Assert.Equal(70, profile.Potential);
        Assert.Equal(DevelopmentStage.Junior, profile.Stage);
    }

    public void MonthlyUpdateChangesAttributes()
    {
        var result = new DevelopmentEngine().ApplyMonthlyUpdate(
            BuildProfile(workEthic: 85, coachability: 80, confidence: 75),
            GoodFactors());

        Assert.True(result.Updates.Count > 0, "Monthly update should change at least one development attribute.");
    }

    public void HighWorkEthicImprovesDevelopment()
    {
        var engine = new DevelopmentEngine();
        var low = engine.ApplyMonthlyUpdate(BuildProfile(workEthic: 35), NeutralFactors());
        var high = engine.ApplyMonthlyUpdate(BuildProfile(workEthic: 90), NeutralFactors());

        Assert.True(high.CurrentAbilityChange > low.CurrentAbilityChange, "Higher work ethic should improve development outcome.");
    }

    public void LowCoachabilitySlowsDevelopment()
    {
        var engine = new DevelopmentEngine();
        var low = engine.ApplyMonthlyUpdate(BuildProfile(coachability: 20), GoodFactors());
        var high = engine.ApplyMonthlyUpdate(BuildProfile(coachability: 90), GoodFactors());

        Assert.True(low.CurrentAbilityChange < high.CurrentAbilityChange, "Low coachability should slow development.");
    }

    public void ConfidenceAffectsDevelopment()
    {
        var engine = new DevelopmentEngine();
        var low = engine.ApplyMonthlyUpdate(BuildProfile(confidence: 20), NeutralFactors());
        var high = engine.ApplyMonthlyUpdate(BuildProfile(confidence: 90), NeutralFactors());

        Assert.True(high.CurrentAbilityChange > low.CurrentAbilityChange, "Confidence should affect development.");
    }

    public void InjuryPenaltyReducesDevelopment()
    {
        var engine = new DevelopmentEngine();
        var healthy = engine.ApplyMonthlyUpdate(BuildProfile(), GoodFactors(injuryPenalty: 0));
        var injured = engine.ApplyMonthlyUpdate(BuildProfile(), GoodFactors(injuryPenalty: 80));

        Assert.True(injured.CurrentAbilityChange < healthy.CurrentAbilityChange, "Injury penalty should reduce development.");
    }

    public void FacilityBonusImprovesDevelopment()
    {
        var engine = new DevelopmentEngine();
        var noBonus = engine.ApplyMonthlyUpdate(BuildProfile(), GoodFactors(facilityBonus: 0, coachingBonus: 0));
        var facility = engine.ApplyMonthlyUpdate(BuildProfile(), GoodFactors(facilityBonus: 80, coachingBonus: 0));

        Assert.True(facility.CurrentAbilityChange > noBonus.CurrentAbilityChange, "Facility bonus should improve development.");
    }

    public void CoachingBonusImprovesDevelopment()
    {
        var engine = new DevelopmentEngine();
        var noBonus = engine.ApplyMonthlyUpdate(BuildProfile(), GoodFactors(facilityBonus: 0, coachingBonus: 0));
        var coaching = engine.ApplyMonthlyUpdate(BuildProfile(), GoodFactors(facilityBonus: 0, coachingBonus: 80));

        Assert.True(coaching.CurrentAbilityChange > noBonus.CurrentAbilityChange, "Coaching bonus should improve development.");
    }

    public void CurrentAbilityCannotExceedPotential()
    {
        var result = new DevelopmentEngine().ApplyMonthlyUpdate(
            BuildProfile(currentAbility: 69, potential: 70, workEthic: 95, coachability: 95, confidence: 95),
            GoodFactors(facilityBonus: 100, coachingBonus: 100, randomModifier: 20));

        Assert.Equal(70, result.UpdatedProfile.CurrentAbility);
    }

    public void VeteranDecliningStageCanRegress()
    {
        var result = new DevelopmentEngine().ApplyMonthlyUpdate(
            BuildProfile(currentAbility: 62, potential: 70, stage: DevelopmentStage.Declining, workEthic: 35, coachability: 30, confidence: 25),
            new DevelopmentFactor(
                Age: 35,
                UpdateDate: new DateOnly(2026, 12, 1),
                IceTimeScore: 20,
                FacilityBonus: 0,
                CoachingBonus: 0,
                InjuryPenalty: 70,
                RandomModifier: -10));

        Assert.True(result.CurrentAbilityChange < 0, "Declining players should be able to regress.");
    }

    public void DevelopmentResultIncludesSummary()
    {
        var result = new DevelopmentEngine().ApplyMonthlyUpdate(BuildProfile(), GoodFactors());

        Assert.True(!string.IsNullOrWhiteSpace(result.Summary), "Development result should include an internal summary.");
        Assert.True(!string.IsNullOrWhiteSpace(result.PlayerFacingSummary), "Development result should include a player-facing summary.");
    }

    public void EventsCreatedForDevelopmentUpdate()
    {
        var eventEngine = new EventEngine();
        var engine = new DevelopmentEngine(eventEngine);

        engine.ApplyMonthlyUpdate(BuildProfile(), GoodFactors());

        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerDevelopmentUpdated), "Development update event should be queued.");
    }

    public void BreakoutEventCanBeCreated()
    {
        var eventEngine = new EventEngine();
        var engine = new DevelopmentEngine(eventEngine);

        var result = engine.ApplyMonthlyUpdate(
            BuildProfile(workEthic: 100, coachability: 100, confidence: 100),
            GoodFactors(facilityBonus: 100, coachingBonus: 100, randomModifier: 20));

        Assert.True(result.IsBreakout, "Result should identify a breakout.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerBreakout), "Breakout event should be queued.");
    }

    public void RegressionEventCanBeCreated()
    {
        var eventEngine = new EventEngine();
        var engine = new DevelopmentEngine(eventEngine);

        var result = engine.ApplyMonthlyUpdate(
            BuildProfile(currentAbility: 62, potential: 70, stage: DevelopmentStage.Declining, workEthic: 20, coachability: 20, confidence: 20),
            new DevelopmentFactor(
                Age: 36,
                UpdateDate: new DateOnly(2026, 12, 1),
                IceTimeScore: 0,
                FacilityBonus: 0,
                CoachingBonus: 0,
                InjuryPenalty: 100,
                RandomModifier: -20));

        Assert.True(result.IsRegression, "Result should identify a regression.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerRegression), "Regression event should be queued.");
    }

    public void TrueRatingsAreNotExposedInDossierFacingOutput()
    {
        var profile = BuildProfile(currentAbility: 47, potential: 83);
        var engine = new DevelopmentEngine();
        var result = engine.ApplyMonthlyUpdate(profile, GoodFactors());
        var dossierSummary = engine.CreateDossierDevelopmentSummary(result);

        Assert.False(dossierSummary.Contains("47", StringComparison.Ordinal), "Dossier-facing output must not expose current ability.");
        Assert.False(dossierSummary.Contains("83", StringComparison.Ordinal), "Dossier-facing output must not expose potential.");
        Assert.False(dossierSummary.Contains("potential", StringComparison.OrdinalIgnoreCase), "Dossier-facing output must not expose potential language as a true rating.");
    }

    public void NoUiOrGodotDependencyExists()
    {
        var developmentFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Development"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in developmentFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Development module should not reference Godot.");
            Assert.False(text.Contains("Control", StringComparison.Ordinal), "Development module should not define UI controls.");
        }
    }

    private static PlayerDevelopmentProfile BuildProfile(
        int currentAbility = 45,
        int potential = 70,
        DevelopmentStage stage = DevelopmentStage.Junior,
        int workEthic = 65,
        int coachability = 65,
        int confidence = 60)
    {
        var traits = new[]
        {
            new DevelopmentTrait(DevelopmentAttribute.Skating, 52),
            new DevelopmentTrait(DevelopmentAttribute.Shooting, 50),
            new DevelopmentTrait(DevelopmentAttribute.Passing, 51),
            new DevelopmentTrait(DevelopmentAttribute.Defense, 49),
            new DevelopmentTrait(DevelopmentAttribute.Physicality, 48),
            new DevelopmentTrait(DevelopmentAttribute.HockeyIQ, 53),
            new DevelopmentTrait(DevelopmentAttribute.WorkEthic, workEthic),
            new DevelopmentTrait(DevelopmentAttribute.Coachability, coachability),
            new DevelopmentTrait(DevelopmentAttribute.Confidence, confidence)
        };

        return new DevelopmentEngine().CreateProfile(
            "player-001",
            currentAbility,
            potential,
            stage,
            traits,
            new DateOnly(2026, 9, 1));
    }

    private static DevelopmentFactor NeutralFactors() =>
        new(
            Age: 18,
            UpdateDate: new DateOnly(2026, 10, 1),
            IceTimeScore: 50,
            FacilityBonus: 0,
            CoachingBonus: 0,
            InjuryPenalty: 0,
            RandomModifier: 0);

    private static DevelopmentFactor GoodFactors(
        int facilityBonus = 40,
        int coachingBonus = 40,
        int injuryPenalty = 0,
        int randomModifier = 0) =>
        new(
            Age: 18,
            UpdateDate: new DateOnly(2026, 10, 1),
            IceTimeScore: 70,
            FacilityBonus: facilityBonus,
            CoachingBonus: coachingBonus,
            InjuryPenalty: injuryPenalty,
            RandomModifier: randomModifier);

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
