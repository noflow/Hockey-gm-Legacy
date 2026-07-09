using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Injuries;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class DevelopmentCurveService
{
    public NewGmScenarioSnapshot EnsureCurves(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var curves = PlayerIds(scenario)
            .Select(id => BuildCurve(scenario, id))
            .ToArray();
        var updated = scenario with { DevelopmentCurves = curves };
        updated.Validate();
        return updated;
    }

    public PlayerDevelopmentCurve BuildCurve(NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        var name = PersonName(scenario, personId);
        var position = ResolvePosition(scenario, personId);
        var age = PersonAge(scenario, personId);
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        var rating = scenario.PlayerRatings.FirstOrDefault(rating => rating.PersonId == personId)
            ?? new PlayerRatingService().BuildSnapshot(scenario, personId);
        var injuries = scenario.AlphaSnapshot.Injuries.Where(injury => injury.PersonId == personId).ToArray();
        var plan = scenario.DevelopmentPlans.FirstOrDefault(plan => plan.PersonId == personId);
        var context = BuildCurrentContext(scenario, personId, plan, injuries);
        var curveType = CurveTypeFor(scenario, personId, age, profile, rating, injuries, context);
        var pace = PaceFor(curveType, profile);
        var eta = TimeToImpact(curveType, pace, age);
        var variance = BuildVariance(rating, curveType, profile, context, injuries);
        var breakthroughs = BuildBreakthroughs(scenario, personId, curveType, variance, context);
        var setbacks = BuildSetbacks(scenario, personId, curveType, variance, context, injuries);
        var curve = new PlayerDevelopmentCurve(
            personId,
            name,
            position,
            age,
            curveType,
            pace,
            variance,
            eta.Low,
            eta.High,
            StaffNote(curveType, variance, context, age),
            BestPath(curveType, position, plan, context),
            breakthroughs,
            setbacks,
            scenario.CurrentDate);
        curve.Validate();
        return curve;
    }

    public PotentialOutcome ProjectOutcome(
        NewGmScenarioSnapshot scenario,
        string personId,
        int yearsElapsed,
        DevelopmentCurveContext context)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(context);
        if (yearsElapsed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(yearsElapsed), "Years elapsed cannot be negative.");
        }

        var curve = scenario.DevelopmentCurves.FirstOrDefault(curve => curve.PersonId == personId)
            ?? BuildCurve(scenario, personId);
        var rating = scenario.PlayerRatings.FirstOrDefault(rating => rating.PersonId == personId)
            ?? new PlayerRatingService().BuildSnapshot(scenario, personId);
        var age = (curve.Age ?? 18) + yearsElapsed;
        var visibleHigh = rating.Potential.High;
        var visibleLow = rating.Potential.Low;
        var growth = GrowthFor(curve, yearsElapsed, age);
        var environment = context.Score;
        var injuryDrag = context.MajorInjury ? 6 : 0;
        var rushedDrag = context.RushedTooEarly ? 7 : 0;
        var plateauRisk = Math.Clamp(curve.Variance.PlateauRisk - environment + injuryDrag + rushedDrag, 0, 100);
        var projectedCeiling = Math.Clamp(curve.Variance.HiddenTrueCeiling + environment / 3 - injuryDrag, 45, 100);
        var projectedOverall = Math.Clamp(rating.Overall.Midpoint + growth + environment / 2 - injuryDrag - rushedDrag, 35, projectedCeiling);

        var result = DetermineOutcome(curve, projectedOverall, projectedCeiling, visibleLow, visibleHigh, plateauRisk, yearsElapsed, age, context);
        var reasons = ReasonsFor(curve, context, plateauRisk, projectedCeiling, visibleHigh);
        var summary = result switch
        {
            PotentialOutcomeResult.ExceededProjection => $"{curve.PlayerName} is tracking above the visible projection because the development environment is strong.",
            PotentialOutcomeResult.BrokeOut => $"{curve.PlayerName} has a realistic breakout path if the current support holds.",
            PotentialOutcomeResult.LateBloomer => $"{curve.PlayerName} may need patience, with impact more likely later than most prospects.",
            PotentialOutcomeResult.Plateaued => $"{curve.PlayerName} is at risk of plateauing before reaching the visible projection.",
            PotentialOutcomeResult.Bust => $"{curve.PlayerName} is trending toward a missed projection unless the environment changes.",
            PotentialOutcomeResult.BelowProjection => $"{curve.PlayerName} projects below the visible ceiling in this environment.",
            PotentialOutcomeResult.RevivedCareer => $"{curve.PlayerName} has a revived path after earlier risk.",
            _ => $"{curve.PlayerName} is tracking close to the current visible projection."
        };

        var outcome = new PotentialOutcome(personId, curve.PlayerName, result, projectedOverall, projectedCeiling, yearsElapsed, summary, reasons);
        outcome.Validate();
        return outcome;
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var curve = scenario.DevelopmentCurves.FirstOrDefault(curve => curve.PersonId == personId)
            ?? BuildCurve(scenario, personId);
        return new[]
        {
            $"Development curve: {Display(curve.CurveType)}",
            $"Development pace: {Display(curve.Pace)}",
            $"ETA: {curve.TimeToImpactDisplay}",
            $"Projected ceiling: {curve.Variance.ProjectedCeilingLow}-{curve.Variance.ProjectedCeilingHigh}",
            $"Current estimated ceiling: {curve.Variance.CurrentEstimatedCeiling}",
            $"Plateau risk: {curve.Variance.PlateauRisk}/100",
            $"Breakout chance: {curve.Variance.BreakoutChance}/100",
            $"Chance to exceed projection: {curve.Variance.ProbabilityExceedingProjection}/100",
            $"Chance to miss projection: {curve.Variance.ProbabilityMissingProjection}/100",
            $"Staff development note: {curve.StaffDevelopmentNote}",
            $"Best development path: {curve.BestDevelopmentPath}"
        }
        .Concat(curve.Breakthroughs.Select(item => $"Development event: {Display(item.EventType)} - {item.Summary}"))
        .Concat(curve.Setbacks.Select(item => $"Development risk: {Display(item.EventType)} - {item.Summary}"))
        .ToArray();
    }

    public IReadOnlyList<ActionCenterItem> BuildActionCenterItems(NewGmScenarioSnapshot scenario)
    {
        var curves = scenario.DevelopmentCurves.Count == 0
            ? EnsureCurves(scenario).DevelopmentCurves
            : scenario.DevelopmentCurves;
        return curves
            .Where(curve => curve.Variance.PlateauRisk >= 72 || curve.Setbacks.Any(setback => setback.EventType is DevelopmentEventType.RushedDevelopmentWarning or DevelopmentEventType.PlateauWarning))
            .OrderByDescending(curve => curve.Variance.PlateauRisk)
            .Take(4)
            .Select(curve => new ActionCenterItem(
                $"action-center:development-curve:{curve.PersonId}",
                $"Development risk: {curve.PlayerName}",
                ActionCenterCategory.PlayerDevelopment,
                curve.Variance.PlateauRisk >= 85 ? ActionCenterPriority.Important : ActionCenterPriority.Normal,
                scenario.CurrentDate.AddDays(7),
                curve.PersonId,
                curve.PlayerName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                curve.StaffDevelopmentNote,
                "Poor role, rushed usage, injuries, or weak development support can cause a player to miss or stall below projection.",
                curve.BestDevelopmentPath,
                null,
                null,
                null))
            .ToArray();
    }

    private static IEnumerable<string> PlayerIds(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DevelopmentProfiles.Select(profile => profile.PersonId)
            .Concat(scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId))
            .Concat(scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Concat(scenario.ProspectRights.Select(record => record.ProspectPersonId))
            .Distinct(StringComparer.Ordinal);

    private static DevelopmentCurveContext BuildCurrentContext(
        NewGmScenarioSnapshot scenario,
        string personId,
        PlayerDevelopmentPlan? plan,
        IReadOnlyList<Injury> injuries)
    {
        var coach = new DevelopmentPlanningService().BuildCoachProfile(scenario);
        var lineupAssignment = scenario.CurrentLineup?.Assignments.FirstOrDefault(assignment => assignment.PersonId == personId);
        var correctRole = lineupAssignment is null || lineupAssignment.CurrentRole == lineupAssignment.PotentialRole;
        var enoughIceTime = plan?.IceTimeRole is DevelopmentIceTimeRole.MiddleSix
            or DevelopmentIceTimeRole.TopSix
            or DevelopmentIceTimeRole.TopPair
            or DevelopmentIceTimeRole.Starter
            or DevelopmentIceTimeRole.Backup
            or DevelopmentIceTimeRole.JuniorReturn
            or DevelopmentIceTimeRole.AhlAssignment
            or null;
        var strongPlan = plan is not null && plan.FocusAreas.Count > 0 && plan.Confidence >= 55;
        var goodMorale = plan is null || plan.Morale is DevelopmentMorale.Average or DevelopmentMorale.Good or DevelopmentMorale.Excellent;
        var majorInjury = injuries.Any(injury => injury.Severity is InjurySeverity.Major or InjurySeverity.Severe or InjurySeverity.CareerThreatening && injury.IsActive);
        var rushedRole = plan?.IceTimeRole is DevelopmentIceTimeRole.TopSix or DevelopmentIceTimeRole.TopPair or DevelopmentIceTimeRole.Starter;
        var rushed = rushedRole && (scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate) ?? 22) <= 18;
        return new DevelopmentCurveContext(coach.FitScore, correctRole, strongPlan, goodMorale, enoughIceTime, true, majorInjury, rushed, false);
    }

    private static DevelopmentCurveType CurveTypeFor(
        NewGmScenarioSnapshot scenario,
        string personId,
        int? age,
        PlayerDevelopmentProfile? profile,
        PlayerRatingSnapshot rating,
        IReadOnlyList<Injury> injuries,
        DevelopmentCurveContext context)
    {
        if (injuries.Any(injury => injury.Severity is InjurySeverity.Major or InjurySeverity.Severe or InjurySeverity.CareerThreatening))
        {
            return DevelopmentCurveType.InjuryRisk;
        }

        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId);
        if (draft is not null && age >= 20)
        {
            return DevelopmentCurveType.OverAger;
        }

        var workEthic = SafeTrait(profile, DevelopmentAttribute.WorkEthic, 55);
        var coachability = SafeTrait(profile, DevelopmentAttribute.Coachability, 55);
        var confidence = SafeTrait(profile, DevelopmentAttribute.Confidence, 55);
        var skillSpread = SkillSpread(profile);

        if (workEthic >= 78 && coachability >= 78 && confidence >= 70)
        {
            return DevelopmentCurveType.MentallyMature;
        }

        if (rating.Overall.Midpoint >= 74 && age <= 18)
        {
            return DevelopmentCurveType.EarlyBloomer;
        }

        if (rating.Potential.High >= 92 && rating.Confidence is PlayerRatingConfidence.Low or PlayerRatingConfidence.Medium)
        {
            return DevelopmentCurveType.BoomBust;
        }

        if (skillSpread >= 35)
        {
            return DevelopmentCurveType.RawToolsyProspect;
        }

        if (rating.Overall.Midpoint >= rating.Potential.Midpoint - 8)
        {
            return DevelopmentCurveType.HighFloor;
        }

        if (age >= 20 && rating.Overall.Midpoint < 72)
        {
            return DevelopmentCurveType.LateBloomer;
        }

        if (context.Score < -8)
        {
            return DevelopmentCurveType.NeedsPatience;
        }

        return profile?.Stage is DevelopmentStage.Prospect or DevelopmentStage.Junior
            ? DevelopmentCurveType.SteadyDeveloper
            : DevelopmentCurveType.SlowBurn;
    }

    private static DevelopmentPace PaceFor(DevelopmentCurveType curveType, PlayerDevelopmentProfile? profile) =>
        curveType switch
        {
            DevelopmentCurveType.EarlyBloomer or DevelopmentCurveType.MentallyMature => DevelopmentPace.Fast,
            DevelopmentCurveType.BoomBust or DevelopmentCurveType.RawToolsyProspect => DevelopmentPace.Unpredictable,
            DevelopmentCurveType.LateBloomer or DevelopmentCurveType.SlowBurn or DevelopmentCurveType.NeedsPatience => DevelopmentPace.Slow,
            DevelopmentCurveType.InjuryRisk => DevelopmentPace.VerySlow,
            _ => SafeTrait(profile, DevelopmentAttribute.WorkEthic, 55) >= 75 ? DevelopmentPace.Fast : DevelopmentPace.Normal
        };

    private static (int Low, int High) TimeToImpact(DevelopmentCurveType curveType, DevelopmentPace pace, int? age)
    {
        if (curveType == DevelopmentCurveType.LateBloomer)
        {
            var yearsToTwentyFour = Math.Max(1, 24 - (age ?? 20));
            return (yearsToTwentyFour, yearsToTwentyFour + 3);
        }

        return pace switch
        {
            DevelopmentPace.Fast => (2, 3),
            DevelopmentPace.Normal => (3, 5),
            DevelopmentPace.Slow => (5, 8),
            DevelopmentPace.VerySlow => (6, 9),
            _ => (2, 8)
        };
    }

    private static PotentialVariance BuildVariance(
        PlayerRatingSnapshot rating,
        DevelopmentCurveType curveType,
        PlayerDevelopmentProfile? profile,
        DevelopmentCurveContext context,
        IReadOnlyList<Injury> injuries)
    {
        var workEthic = SafeTrait(profile, DevelopmentAttribute.WorkEthic, 55);
        var coachability = SafeTrait(profile, DevelopmentAttribute.Coachability, 55);
        var confidence = SafeTrait(profile, DevelopmentAttribute.Confidence, 55);
        var traitModifier = (workEthic + coachability + confidence - 165) / 12;
        var environment = context.Score;
        var volatility = curveType is DevelopmentCurveType.BoomBust or DevelopmentCurveType.RawToolsyProspect ? 7 : 3;
        var injuryPenalty = injuries.Any(injury => injury.Severity is InjurySeverity.Major or InjurySeverity.Severe or InjurySeverity.CareerThreatening) ? 5 : 0;
        var projectedLow = Math.Clamp(rating.Potential.Low - injuryPenalty - (curveType == DevelopmentCurveType.InjuryRisk ? 3 : 0), 45, 99);
        var projectedHigh = Math.Clamp(rating.Potential.High + volatility + Math.Max(0, environment / 8), projectedLow, 99);
        var hidden = Math.Clamp(rating.Potential.High + traitModifier + environment / 5 + volatility / 2 - injuryPenalty, projectedLow, 99);
        var currentEstimate = Math.Clamp((projectedLow + projectedHigh) / 2 + environment / 8 - injuryPenalty, projectedLow, projectedHigh);
        var exceed = Math.Clamp(20 + traitModifier * 4 + Math.Max(0, environment) + volatility - injuryPenalty * 3, 2, 75);
        var miss = Math.Clamp(25 - traitModifier * 3 + Math.Max(0, -environment) + injuryPenalty * 5 + (curveType == DevelopmentCurveType.BoomBust ? 12 : 0), 2, 85);
        var plateau = Math.Clamp(20 + Math.Max(0, -environment) + injuryPenalty * 6 + (curveType is DevelopmentCurveType.HighFloor or DevelopmentCurveType.OverAger ? 12 : 0), 5, 90);
        var breakout = Math.Clamp(exceed + (curveType is DevelopmentCurveType.LateBloomer or DevelopmentCurveType.BoomBust ? 10 : 0), 2, 80);
        return new PotentialVariance(projectedLow, projectedHigh, currentEstimate, hidden, environment, exceed, miss, plateau, breakout);
    }

    private static IReadOnlyList<DevelopmentBreakthrough> BuildBreakthroughs(NewGmScenarioSnapshot scenario, string personId, DevelopmentCurveType curveType, PotentialVariance variance, DevelopmentCurveContext context)
    {
        var items = new List<DevelopmentBreakthrough>();
        if (variance.BreakoutChance >= 55)
        {
            items.Add(new DevelopmentBreakthrough(
                $"development-breakthrough:{personId}:{scenario.CurrentDate:yyyyMMdd}:breakout-path",
                personId,
                scenario.CurrentDate,
                curveType == DevelopmentCurveType.LateBloomer ? DevelopmentEventType.LateBloomerEmerging : DevelopmentEventType.PotentialRevisedUpward,
                "Staff see enough traits and support for this player to exceed the current public projection.",
                1,
                2));
        }

        if (context.CoachingQuality >= 75 && context.StrongDevelopmentPlan)
        {
            items.Add(new DevelopmentBreakthrough(
                $"development-breakthrough:{personId}:{scenario.CurrentDate:yyyyMMdd}:coach-fit",
                personId,
                scenario.CurrentDate,
                DevelopmentEventType.CoachUnlocksPotential,
                "The development staff and current plan are a strong match.",
                1,
                1));
        }

        return items;
    }

    private static IReadOnlyList<DevelopmentSetback> BuildSetbacks(NewGmScenarioSnapshot scenario, string personId, DevelopmentCurveType curveType, PotentialVariance variance, DevelopmentCurveContext context, IReadOnlyList<Injury> injuries)
    {
        var items = new List<DevelopmentSetback>();
        if (context.RushedTooEarly)
        {
            items.Add(new DevelopmentSetback(
                $"development-setback:{personId}:{scenario.CurrentDate:yyyyMMdd}:rushed",
                personId,
                scenario.CurrentDate,
                DevelopmentEventType.RushedDevelopmentWarning,
                "Staff worry this player is being pushed into too much too soon.",
                -1,
                12));
        }

        if (variance.PlateauRisk >= 70)
        {
            items.Add(new DevelopmentSetback(
                $"development-setback:{personId}:{scenario.CurrentDate:yyyyMMdd}:plateau",
                personId,
                scenario.CurrentDate,
                DevelopmentEventType.PlateauWarning,
                "The current path carries a meaningful plateau risk.",
                0,
                10));
        }

        if (injuries.Any(injury => injury.Severity is InjurySeverity.Major or InjurySeverity.Severe or InjurySeverity.CareerThreatening))
        {
            items.Add(new DevelopmentSetback(
                $"development-setback:{personId}:{scenario.CurrentDate:yyyyMMdd}:injury",
                personId,
                scenario.CurrentDate,
                DevelopmentEventType.PotentialRevisedDownward,
                "Major injury history is lowering the development probability.",
                -2,
                15));
        }

        return items;
    }

    private static int GrowthFor(PlayerDevelopmentCurve curve, int yearsElapsed, int age)
    {
        var yearly = curve.Pace switch
        {
            DevelopmentPace.Fast => 5,
            DevelopmentPace.Normal => 4,
            DevelopmentPace.Slow => 2,
            DevelopmentPace.VerySlow => 1,
            _ => 3
        };

        if (curve.CurveType == DevelopmentCurveType.LateBloomer && age >= 24)
        {
            yearly += 3;
        }

        if (curve.CurveType == DevelopmentCurveType.SlowBurn && yearsElapsed >= 5)
        {
            yearly += 1;
        }

        return Math.Clamp(yearly * yearsElapsed, 0, 30);
    }

    private static PotentialOutcomeResult DetermineOutcome(
        PlayerDevelopmentCurve curve,
        int projectedOverall,
        int projectedCeiling,
        int visibleLow,
        int visibleHigh,
        int plateauRisk,
        int yearsElapsed,
        int age,
        DevelopmentCurveContext context)
    {
        if (context.MajorInjury && projectedOverall < visibleLow)
        {
            return PotentialOutcomeResult.BelowProjection;
        }

        if (plateauRisk >= 78 && projectedOverall < visibleHigh)
        {
            return PotentialOutcomeResult.Plateaued;
        }

        if (curve.CurveType == DevelopmentCurveType.LateBloomer && age >= 24 && projectedOverall >= visibleHigh - 1)
        {
            return PotentialOutcomeResult.LateBloomer;
        }

        if (projectedCeiling > visibleHigh && projectedOverall > visibleHigh)
        {
            return yearsElapsed <= 3 ? PotentialOutcomeResult.BrokeOut : PotentialOutcomeResult.ExceededProjection;
        }

        if (projectedOverall < visibleLow - 3 && (context.RushedTooEarly || context.BrokenPromise))
        {
            return PotentialOutcomeResult.Bust;
        }

        if (projectedOverall < visibleLow)
        {
            return PotentialOutcomeResult.BelowProjection;
        }

        return PotentialOutcomeResult.MetProjection;
    }

    private static IReadOnlyList<string> ReasonsFor(PlayerDevelopmentCurve curve, DevelopmentCurveContext context, int plateauRisk, int projectedCeiling, int visibleHigh)
    {
        var reasons = new List<string>
        {
            $"Curve type: {Display(curve.CurveType)}.",
            $"Development environment modifier: {context.Score}.",
            $"Plateau risk: {plateauRisk}/100."
        };
        if (projectedCeiling > visibleHigh)
        {
            reasons.Add("Hidden ceiling can exceed the current visible projection in the right environment.");
        }

        if (context.MajorInjury)
        {
            reasons.Add("Major injury context lowers the growth path.");
        }

        if (context.CorrectRole && context.EnoughIceTime)
        {
            reasons.Add("Role and ice time support the development path.");
        }

        return reasons;
    }

    private static string StaffNote(DevelopmentCurveType curveType, PotentialVariance variance, DevelopmentCurveContext context, int? age) =>
        curveType switch
        {
            DevelopmentCurveType.LateBloomer => "Needs patience; staff believe impact may arrive later than the normal prospect window.",
            DevelopmentCurveType.BoomBust => "Wide outcome band; staff see upside but need more evidence before treating the ceiling as bankable.",
            DevelopmentCurveType.InjuryRisk => "Medical history is a real development variable and should shape workload decisions.",
            DevelopmentCurveType.RawToolsyProspect => "Tool base is interesting, but the details need structure and repetition.",
            DevelopmentCurveType.EarlyBloomer => "Already ahead of peers, but staff should avoid assuming the curve will stay linear.",
            DevelopmentCurveType.HighFloor => "Useful floor is visible; ceiling depends on role growth and confidence.",
            DevelopmentCurveType.MentallyMature => "Professional habits give this player a better chance to turn opportunity into growth.",
            DevelopmentCurveType.NeedsPatience => "The current path has friction; rushing this player could damage confidence.",
            _ => variance.BreakoutChance >= 50
                ? "Staff believe this player can beat projection with the right support."
                : "Staff see a normal development path with role and coaching fit still important."
        };

    private static string BestPath(DevelopmentCurveType curveType, RosterPosition position, PlayerDevelopmentPlan? plan, DevelopmentCurveContext context)
    {
        var role = position == RosterPosition.Goalie
            ? "controlled starts and technical repetition"
            : position == RosterPosition.Defense
                ? "stable pair usage and special-teams reps only when earned"
                : "clear line role, practice focus, and measured offensive opportunity";
        var planText = plan is null ? "create a real development plan" : $"continue the {plan.IceTimeRole} plan";
        return curveType switch
        {
            DevelopmentCurveType.LateBloomer or DevelopmentCurveType.SlowBurn or DevelopmentCurveType.NeedsPatience => $"{planText}; prioritize patience, confidence, and proper league placement with {role}.",
            DevelopmentCurveType.BoomBust or DevelopmentCurveType.RawToolsyProspect => $"{planText}; give structured coaching, consistent feedback, and enough ice time to turn tools into habits.",
            DevelopmentCurveType.InjuryRisk => $"{planText}; protect workload, manage recovery, and avoid rushing role increases.",
            _ => $"{planText}; keep role, coaching fit, morale, and ice time aligned with {role}."
        };
    }

    private static int SafeTrait(PlayerDevelopmentProfile? profile, DevelopmentAttribute attribute, int fallback) =>
        profile?.Traits.FirstOrDefault(trait => trait.Attribute == attribute)?.Value ?? fallback;

    private static int SkillSpread(PlayerDevelopmentProfile? profile)
    {
        if (profile is null || profile.Traits.Count == 0)
        {
            return 0;
        }

        var values = profile.Traits.Select(trait => trait.Value).ToArray();
        return values.Max() - values.Min();
    }

    private static string Display(object value) =>
        (value.ToString() ?? string.Empty) switch
        {
            "EarlyBloomer" => "Early Bloomer",
            "SteadyDeveloper" => "Steady Developer",
            "LateBloomer" => "Late Bloomer",
            "SlowBurn" => "Slow Burn",
            "BoomBust" => "Boom/Bust",
            "HighFloor" => "High Floor",
            "RawToolsyProspect" => "Raw Toolsy Prospect",
            "OverAger" => "Over-Ager",
            "InjuryRisk" => "Injury Risk",
            "MentallyMature" => "Mentally Mature",
            "NeedsPatience" => "Needs Patience",
            "VerySlow" => "Very Slow",
            "TrainingBreakthrough" => "Training breakthrough",
            "ConfidenceSurge" => "Confidence surge",
            "RoleDrivenGrowth" => "Role-driven growth",
            "CoachUnlocksPotential" => "Coach unlocks potential",
            "RushedDevelopmentWarning" => "Rushed development warning",
            "PlateauWarning" => "Plateau warning",
            "LateBloomerEmerging" => "Late bloomer emerging",
            "PotentialRevisedUpward" => "Potential revised upward",
            "PotentialRevisedDownward" => "Potential revised downward",
            var text => text
        };

    private static RosterPosition ResolvePosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Position
        ?? RosterPosition.Unknown;

    private static int? PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Age
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Age;

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        FindPerson(scenario, personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProspectName
        ?? personId;

    private static Person? FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId);
}
