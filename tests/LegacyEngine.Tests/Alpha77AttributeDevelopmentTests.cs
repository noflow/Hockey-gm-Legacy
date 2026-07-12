using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.World;

internal sealed class Alpha77AttributeDevelopmentTests
{
    public void AttributeDevelopmentResultIsCreated()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 18));

        Assert.Equal(personId, result.Snapshot.PersonId);
        Assert.True(result.Snapshot.Events.Count > 0, "Attribute development should create attribute change events.");
        Assert.True(result.Summary.Contains(result.Snapshot.PlayerName, StringComparison.Ordinal), "Result summary should name the player.");
    }

    public void MissingPlanForKnownPlayerIsCreatedBeforeAttributeReport()
    {
        var scenario = Scenario(out var registry);
        var personId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var withoutPlan = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                DevelopmentProfiles = scenario.AlphaSnapshot.DevelopmentProfiles
                    .Where(profile => profile.PersonId != personId)
                    .ToArray()
            },
            DevelopmentPlans = scenario.DevelopmentPlans
                .Where(plan => plan.PersonId != personId)
                .ToArray()
        };

        var result = new AttributeDevelopmentService().ApplyMonthlyDevelopment(
            registry,
            withoutPlan,
            personId,
            Modifier(age: 22, updateVisible: true));

        Assert.True(result.ScenarioSnapshot.DevelopmentPlans.Any(plan => plan.PersonId == personId), "A known player should receive a default development plan before a report is generated.");
        Assert.True(result.ScenarioSnapshot.AlphaSnapshot.DevelopmentProfiles.Any(profile => profile.PersonId == personId), "A known player should receive a generated development profile when an older save lacks one.");
    }

    public void SpeedDevelopsEarlierThanLateCareer()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var early = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 18));
        var late = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 31));

        Assert.True(Delta(early, PlayerAttributeKey.Speed) > Delta(late, PlayerAttributeKey.Speed), "Speed should develop more strongly early than late.");
    }

    public void StrengthImprovesWithAgeAndTraining()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Strength, Modifier(age: 22, staff: 80, work: 80));

        Assert.True(Delta(result, PlayerAttributeKey.Strength) > 0, "Strength training should improve strength for maturing players.");
    }

    public void HockeyIqImprovesWithExperience()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.HockeyIQ, Modifier(age: 23));

        Assert.True(Delta(result, PlayerAttributeKey.HockeyIQ) > 0, "Hockey IQ should improve with experience.");
    }

    public void LeadershipImprovesForVeterans()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Leadership, Modifier(age: 27, professionalism: 80));

        Assert.True(Delta(result, PlayerAttributeKey.Leadership) > 0, "Veterans should have a path to leadership growth.");
    }

    public void InjuryReducesDurabilityGrowth()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var healthy = Apply(scenario, registry, personId, DevelopmentPlanFocus.Conditioning, Modifier(age: 20, injury: 0));
        var injured = Apply(scenario, registry, personId, DevelopmentPlanFocus.Conditioning, Modifier(age: 20, injury: 30));

        Assert.True(Delta(injured, PlayerAttributeKey.Durability) < Delta(healthy, PlayerAttributeKey.Durability), "Injuries should reduce durability growth.");
    }

    public void TrainingFocusImprovesRelatedAttributes()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var shooting = Apply(scenario, registry, personId, DevelopmentPlanFocus.Shooting, Modifier(age: 18, work: 80));

        Assert.True(Delta(shooting, PlayerAttributeKey.Shooting) > 0, "Training focus should boost related attributes.");
    }

    public void CoachSpecialtyBoostsRelatedGrowth()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var generic = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 20, specialty: null));
        var specialist = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 20, specialty: DevelopmentCoachSpecialty.Skating));

        Assert.True(Delta(specialist, PlayerAttributeKey.Speed) > Delta(generic, PlayerAttributeKey.Speed), "Coach specialty should boost matching attribute growth.");
    }

    public void PoorRoleCreatesPlateauRisk()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Confidence, Modifier(age: 19, poorRole: true));

        Assert.True(result.Snapshot.Events.Any(item => item.IsPlateau), "Poor role usage should create plateau risk.");
    }

    public void RushedPlayerCanStallBelowPotential()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Confidence, Modifier(age: 18, rushed: true, role: DevelopmentIceTimeRole.TopSix));

        Assert.True(result.Snapshot.Events.Any(item => item.RegressionReason == AttributeRegressionReason.RushedTooEarly || item.IsPlateau), "Rushed prospects should create stall or regression signals.");
        Assert.True(result.Snapshot.OverallAfter < result.Snapshot.PotentialAfter, "Rushed player can remain below potential.");
    }

    public void LateBloomerImprovesAfterSeveralYears()
    {
        var scenario = ForceCurve(Scenario(out var registry), ProspectId, DevelopmentCurveType.LateBloomer, DevelopmentPace.Normal);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.HockeyIQ, Modifier(age: 25, years: 6));

        Assert.True(result.Snapshot.OverallDelta > 0 || result.Snapshot.Events.Any(item => item.Delta > 0), "Late bloomer should still be able to improve after several years.");
    }

    public void FastDeveloperImprovesQuickly()
    {
        var scenario = ForceCurve(Scenario(out var registry), ProspectId, DevelopmentCurveType.EarlyBloomer, DevelopmentPace.Fast);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 18, years: 1));

        Assert.True(Delta(result, PlayerAttributeKey.Speed) >= 3, "Fast developers should show quick early attribute jumps.");
    }

    public void VisibleRatingDoesNotAutoUpdateWithoutReport()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var before = scenario.ScoutedRatings.First(item => item.PersonId == personId);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Shooting, Modifier(age: 18, updateVisible: false));
        var after = result.ScenarioSnapshot.ScoutedRatings.First(item => item.PersonId == personId);

        Assert.Equal(before.Overall.Display, after.Overall.Display);
        Assert.True(result.Snapshot.VisibleEstimateStale, "Visible estimates should be stale until a development or scouting report updates them.");
    }

    public void DevelopmentReportUpdatesVisibleEstimate()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Shooting, Modifier(age: 18, updateVisible: true));
        var visible = result.ScenarioSnapshot.ScoutedRatings.First(item => item.PersonId == personId);

        Assert.Equal(PlayerRatingSource.DevelopmentReport, visible.Source);
        Assert.True(result.Snapshot.VisibleEstimateUpdated, "Development report should update the visible estimate.");
    }

    public void ActionCenterOnlyShowsMeaningfulEvents()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var routine = Apply(scenario, registry, personId, DevelopmentPlanFocus.Balanced, Modifier(age: 24, staff: 45, work: 45, coachability: 45));
        var meaningful = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 18, staff: 85, work: 85, coachability: 80));

        Assert.Equal(0, routine.ActionItems.Count);
        Assert.True(meaningful.ActionItems.Count > 0, "Meaningful development changes should enter Action Center.");
    }

    public void DossierExposesAttributeTrends()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 18, updateVisible: true));
        var dossier = new PlayerDossierService().CreateDossier(result.ScenarioSnapshot, personId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(dossier.Sections.Any(section => section.Title == "Attribute Development"), "Dossier should include attribute development section.");
        Assert.True(text.Contains("Attribute trend:", StringComparison.Ordinal), "Dossier should include recent attribute trends.");
    }

    public void HistoryRecordsBreakthroughOrSetback()
    {
        var scenario = Scenario(out var registry);
        var personId = ProspectId(scenario);
        var result = Apply(scenario, registry, personId, DevelopmentPlanFocus.Skating, Modifier(age: 18, staff: 90, work: 90, coachability: 85));

        Assert.True(result.ScenarioSnapshot.CareerTimeline.Entries.Any(entry => entry.PersonId == personId), "Meaningful attribute changes should be written to career history.");
    }

    public void HiddenTrueRatingsAreNotRenderedDirectly()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Attribute Report", StringComparison.Ordinal), "AlphaDesktop should expose attribute development report action.");
        Assert.False(source.Contains("ScenarioSnapshot.TrueRatings", StringComparison.Ordinal), "AlphaDesktop must not render hidden true ratings.");
        Assert.False(source.Contains("PlayerTrueRatings", StringComparison.Ordinal), "AlphaDesktop must not reference hidden true rating types.");
    }

    private static AttributeDevelopmentResult Apply(
        NewGmScenarioSnapshot scenario,
        EngineRegistry registry,
        string personId,
        DevelopmentPlanFocus focus,
        AttributeDevelopmentModifier modifier)
    {
        scenario = new DevelopmentPlanningService()
            .SetPlan(registry, scenario, personId, new[] { focus, DevelopmentPlanFocus.Confidence }, modifier.LineupRole, "test focus")
            .ScenarioSnapshot;
        return new AttributeDevelopmentService().ApplyMonthlyDevelopment(registry, scenario, personId, modifier);
    }

    private static AttributeDevelopmentModifier Modifier(
        int age,
        int years = 0,
        int staff = 65,
        int work = 65,
        int coachability = 65,
        int professionalism = 65,
        int injury = 0,
        bool rushed = false,
        bool poorRole = false,
        bool updateVisible = false,
        DevelopmentCoachSpecialty? specialty = null,
        DevelopmentIceTimeRole role = DevelopmentIceTimeRole.MiddleSix) =>
        new(
            age,
            years,
            LeagueExperience.Junior,
            role,
            PowerPlayUsage: false,
            PenaltyKillUsage: false,
            specialty,
            staff,
            Morale: poorRole ? 30 : 60,
            RelationshipTrust: 60,
            injury,
            FatiguePenalty: 0,
            work,
            coachability,
            professionalism,
            TeamCulture: 65,
            rushed,
            poorRole,
            updateVisible);

    private static int Delta(AttributeDevelopmentResult result, PlayerAttributeKey key) =>
        result.Snapshot.Events.FirstOrDefault(item => item.Attribute == key)?.Delta ?? 0;

    private static string ProspectId(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DraftBoard.Entries
            .First(entry => entry.Bio?.Position != RosterPosition.Goalie)
            .ProspectPersonId;

    private static NewGmScenarioSnapshot ForceCurve(
        NewGmScenarioSnapshot scenario,
        Func<NewGmScenarioSnapshot, string> personSelector,
        DevelopmentCurveType type,
        DevelopmentPace pace)
    {
        var personId = personSelector(scenario);
        var curve = scenario.DevelopmentCurves.First(item => item.PersonId == personId) with
        {
            CurveType = type,
            Pace = pace
        };

        return scenario with
        {
            DevelopmentCurves = scenario.DevelopmentCurves
                .Where(item => item.PersonId != personId)
                .Append(curve)
                .ToArray()
        };
    }

    private static NewGmScenarioSnapshot Scenario(out EngineRegistry registry)
    {
        var bootstrap = NewGmScenarioBootstrapper.CreateScenario();
        var eventEngine = new EventEngine();
        registry = EngineRegistry.Create(new WorldEngine(bootstrap.ScenarioSnapshot.AlphaSnapshot.WorldState, eventEngine), bootstrap.ScenarioSnapshot.LeagueProfile.Rulebook);
        return new PlayerRatingService().EnsureRatings(
            new DevelopmentCurveService().EnsureCurves(
                new HockeyIntelligenceRatingService().EnsureRatings(
                    new DevelopmentPlanningService().EnsureScenarioPlans(bootstrap.ScenarioSnapshot))));
    }

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "HockeyGmLegacy.slnx")))
        {
            current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }

        return current;
    }
}
