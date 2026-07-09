using LegacyEngine.Injuries;
using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

internal sealed class Alpha617DevelopmentCurveTests
{
    public void PlayerCanExceedVisiblePotentialRange()
    {
        var scenario = ScenarioWithForcedCurve(DevelopmentCurveType.BoomBust, DevelopmentPace.Unpredictable, hiddenCeiling: 99, plateauRisk: 25);
        var personId = scenario.DevelopmentCurves[0].PersonId;
        var rating = Rating(scenario, personId);
        var outcome = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 4, DevelopmentCurveContext.Strong);

        Assert.True(outcome.ProjectedCeiling > rating.Potential.High || outcome.Result is PotentialOutcomeResult.ExceededProjection or PotentialOutcomeResult.BrokeOut, "Strong environment should allow some players to exceed visible potential projection.");
    }

    public void PlayerCanMissHighPotentialDueToPoorDevelopment()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var rating = Rating(scenario, personId);
        var outcome = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 4, DevelopmentCurveContext.Poor);

        Assert.True(outcome.ProjectedOverall < rating.Potential.High, "Poor environment should allow a high-potential player to miss projection.");
        Assert.True(outcome.Result is PotentialOutcomeResult.BelowProjection or PotentialOutcomeResult.Plateaued or PotentialOutcomeResult.Bust, "Poor environment should explain the missed projection.");
    }

    public void RushedPlayerCanPlateau()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var context = new DevelopmentCurveContext(45, false, false, false, false, false, false, true, true);
        var outcome = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 3, context);

        Assert.True(outcome.Result is PotentialOutcomeResult.Plateaued or PotentialOutcomeResult.Bust or PotentialOutcomeResult.BelowProjection, "Rushing a player should create plateau or missed-projection risk.");
    }

    public void LateBloomerDevelopsAfterSeveralYears()
    {
        var scenario = ScenarioWithForcedCurve(DevelopmentCurveType.LateBloomer, DevelopmentPace.Slow, hiddenCeiling: 92, plateauRisk: 20);
        var personId = scenario.DevelopmentCurves[0].PersonId;
        var outcome = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 6, DevelopmentCurveContext.Strong);

        Assert.Equal(PotentialOutcomeResult.LateBloomer, outcome.Result);
    }

    public void FastDeveloperImprovesQuickly()
    {
        var scenario = ScenarioWithForcedCurve(DevelopmentCurveType.EarlyBloomer, DevelopmentPace.Fast, hiddenCeiling: 91, plateauRisk: 15);
        var personId = scenario.DevelopmentCurves[0].PersonId;
        var rating = Rating(scenario, personId);
        var outcome = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 2, DevelopmentCurveContext.Strong);

        Assert.True(outcome.ProjectedOverall >= rating.Overall.Midpoint + 8, "Fast developer should gain useful level quickly.");
    }

    public void SlowBurnTakesLonger()
    {
        var scenario = ScenarioWithForcedCurve(DevelopmentCurveType.SlowBurn, DevelopmentPace.Slow, hiddenCeiling: 91, plateauRisk: 20);
        var personId = scenario.DevelopmentCurves[0].PersonId;
        var early = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 2, DevelopmentCurveContext.Strong);
        var later = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 6, DevelopmentCurveContext.Strong);

        Assert.True(later.ProjectedOverall > early.ProjectedOverall, "Slow-burn player should show more impact over the longer window.");
    }

    public void CoachingEnvironmentAffectsOutcome()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var strong = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 4, DevelopmentCurveContext.Strong);
        var poor = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 4, DevelopmentCurveContext.Poor);

        Assert.True(strong.ProjectedOverall > poor.ProjectedOverall, "Coaching and development environment should affect projection.");
    }

    public void InjuryReducesGrowthChance()
    {
        var scenario = ScenarioWithForcedCurve(DevelopmentCurveType.SteadyDeveloper, DevelopmentPace.Normal, hiddenCeiling: 92, plateauRisk: 20);
        var personId = scenario.DevelopmentCurves[0].PersonId;
        var healthy = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 3, DevelopmentCurveContext.Strong);
        var injuredContext = DevelopmentCurveContext.Strong with { MajorInjury = true };
        var injured = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 3, injuredContext);

        Assert.True(injured.ProjectedOverall < healthy.ProjectedOverall, "Major injury should reduce growth outcome.");
    }

    public void ProperRoleImprovesGrowthChance()
    {
        var scenario = ScenarioWithForcedCurve(DevelopmentCurveType.SteadyDeveloper, DevelopmentPace.Normal, hiddenCeiling: 92, plateauRisk: 20);
        var personId = scenario.DevelopmentCurves[0].PersonId;
        var proper = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 3, DevelopmentCurveContext.Strong);
        var wrongRole = DevelopmentCurveContext.Strong with { CorrectRole = false, EnoughIceTime = false };
        var poorFit = new DevelopmentCurveService().ProjectOutcome(scenario, personId, 3, wrongRole);

        Assert.True(proper.ProjectedOverall > poorFit.ProjectedOverall, "Correct role and ice time should improve growth chance.");
    }

    public void DossierExposesDevelopmentCurve()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, personId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(dossier.Sections.Any(section => section.Title == "Development Curve"), "Dossier should include development curve section.");
        Assert.True(text.Contains("Development curve:", StringComparison.Ordinal), "Dossier should show curve type.");
        Assert.True(text.Contains("ETA:", StringComparison.Ordinal), "Dossier should show time-to-impact estimate.");
    }

    public void ActionCenterWarnsOnMajorDevelopmentRisk()
    {
        var seed = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = ScenarioWithForcedCurve(DevelopmentCurveType.NeedsPatience, DevelopmentPace.Slow, hiddenCeiling: 84, plateauRisk: 90, seed.ScenarioSnapshot);
        var budget = new BudgetOverviewService().Build(scenario, seed.Registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());
        var readiness = new SeasonReadinessService().Evaluate(seed.Registry, scenario);
        var items = new ActionCenterService().BuildItems(scenario, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Category == ActionCenterCategory.PlayerDevelopment && item.Title.Contains("Development risk", StringComparison.Ordinal)), "Action Center should warn on major development risk.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        new PlayerRatingService().EnsureRatings(new DevelopmentCurveService().EnsureCurves(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot));

    private static PlayerRatingSnapshot Rating(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerRatings.FirstOrDefault(rating => rating.PersonId == personId)
        ?? new PlayerRatingService().BuildSnapshot(scenario, personId);

    private static NewGmScenarioSnapshot ScenarioWithForcedCurve(
        DevelopmentCurveType curveType,
        DevelopmentPace pace,
        int hiddenCeiling,
        int plateauRisk,
        NewGmScenarioSnapshot? input = null)
    {
        var scenario = input ?? Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.Skip(10).First().ProspectPersonId;
        var draftBoard = scenario.AlphaSnapshot.DraftBoard with
        {
            Entries = scenario.AlphaSnapshot.DraftBoard.Entries
                .Select(entry => entry.ProspectPersonId == personId ? entry with { ScoutingConfidence = ScoutingConfidenceLevel.VeryHigh } : entry)
                .ToArray()
        };
        scenario = scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = draftBoard } };
        var service = new DevelopmentCurveService();
        var baseCurve = service.BuildCurve(scenario, personId);
        var rating = Rating(scenario, personId);
        var variance = new PotentialVariance(
            Math.Max(rating.Potential.Low - 2, rating.Overall.Midpoint),
            Math.Max(rating.Potential.High, hiddenCeiling - 2),
            Math.Max(rating.Potential.Midpoint, hiddenCeiling - 4),
            hiddenCeiling,
            20,
            55,
            20,
            plateauRisk,
            55);
        var setbacks = plateauRisk >= 80
            ? new[]
            {
                new DevelopmentSetback(
                    $"test-setback:{personId}",
                    personId,
                    scenario.CurrentDate,
                    DevelopmentEventType.PlateauWarning,
                    "Test plateau warning.",
                    0,
                    20)
            }
            : Array.Empty<DevelopmentSetback>();
        var forced = baseCurve with
        {
            CurveType = curveType,
            Pace = pace,
            Age = 21,
            Variance = variance,
            Setbacks = setbacks,
            StaffDevelopmentNote = "Test curve note for development risk and upside.",
            BestDevelopmentPath = "Keep the player in a patient, properly matched development role."
        };
        forced.Validate();
        return new PlayerRatingService().EnsureRatings(scenario with { DevelopmentCurves = new[] { forced } });
    }
}
