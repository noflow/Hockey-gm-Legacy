using LegacyEngine.Development;
using LegacyEngine.Events;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class AttributeDevelopmentService
{
    private readonly HockeyIntelligenceRatingService _ratings = new();
    private readonly DevelopmentPlanningService _planning = new();

    public AttributeDevelopmentResult ApplyMonthlyDevelopment(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        AttributeDevelopmentModifier? modifier = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        // Existing saves and newly added organization-depth players may not yet
        // have a development profile. Generate the missing profile/plan first so
        // a GM-facing report action never crashes on a valid player.
        var planned = _planning.EnsureScenarioPlanForPerson(scenario, personId);
        var prepared = new DevelopmentCurveService().EnsureCurves(_planning.EnsureScenarioPlans(_ratings.EnsureRatings(planned)));
        var plan = prepared.DevelopmentPlans.FirstOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Development plan was not found.", nameof(personId));
        var before = prepared.TrueRatings.FirstOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Hidden Hockey Intelligence ratings were not found.", nameof(personId));
        var context = modifier ?? BuildModifier(prepared, personId, plan);
        context.Validate();

        var deltas = BuildDeltas(prepared, before, plan, context);
        var changed = _ratings.ApplyDevelopmentToTrueRatings(prepared, personId, deltas, prepared.CurrentDate);
        var after = changed.TrueRatings.First(item => item.PersonId == personId);
        var events = AddContextEvents(changed, after, context, BuildEvents(changed, before, after, plan, context, deltas).ToArray()).ToArray();
        var focusCategory = FocusCategory(plan.FocusAreas.FirstOrDefault(), after.Position);
        var visibleUpdated = false;
        var visibleStale = IsVisibleEstimateStale(changed, personId);
        if (context.UpdateVisibleEstimate)
        {
            changed = _ratings.RecordDevelopmentReport(
                changed,
                personId,
                PlayerRatingColor.Blue,
                focusCategory,
                "Development staff",
                $"Development staff updated the estimate after a month focused on {FocusText(plan.FocusAreas.FirstOrDefault())}.");
            changed = new PlayerRatingService().EnsureRatings(changed);
            visibleUpdated = true;
            visibleStale = false;
        }

        var snapshot = BuildSnapshot(changed, before, after, plan, events, visibleUpdated, visibleStale);
        var timeline = AppendHistory(changed, snapshot);
        var finalScenario = changed with
        {
            AttributeDevelopmentSnapshots = changed.AttributeDevelopmentSnapshots
                .Where(item => !string.Equals(item.SnapshotId, snapshot.SnapshotId, StringComparison.Ordinal))
                .Concat(new[] { snapshot })
                .OrderBy(item => item.PersonId, StringComparer.Ordinal)
                .ThenBy(item => item.Date)
                .ToArray(),
            CareerTimeline = timeline
        };

        finalScenario = new PlayerRatingService().EnsureRatings(finalScenario);
        QueueMeaningfulEvents(registry, finalScenario, snapshot);
        var actionItems = BuildActionItems(finalScenario, snapshot).ToArray();
        var result = new AttributeDevelopmentResult(
            finalScenario,
            snapshot,
            actionItems,
            Array.Empty<AlphaInboxItem>(),
            snapshot.StaffNote);
        result.Validate();
        return result;
    }

    public AttributeDevelopmentSummary BuildMonthlyReport(NewGmScenarioSnapshot scenario, DateOnly? reportDate = null)
    {
        var date = reportDate ?? scenario.CurrentDate;
        var snapshots = scenario.AttributeDevelopmentSnapshots
            .Where(item => item.Date.Month == date.Month && item.Date.Year == date.Year)
            .OrderByDescending(item => item.Events.Count(evt => evt.IsMeaningful))
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var summary = new AttributeDevelopmentSummary(
            date,
            snapshots,
            snapshots.Where(item => item.BiggestGain != "none").Select(item => $"{item.PlayerName}: {item.BiggestGain}").Take(8).ToArray(),
            snapshots.Where(item => item.BiggestRegression != "none").Select(item => $"{item.PlayerName}: {item.BiggestRegression}").Take(8).ToArray(),
            snapshots.SelectMany(item => item.Events.Where(evt => evt.IsBreakthrough).Select(_ => item.PlayerName)).Distinct(StringComparer.Ordinal).Take(6).ToArray(),
            snapshots.Where(item => item.Events.Any(evt => evt.IsPlateau)).Select(item => item.PlayerName).Distinct(StringComparer.Ordinal).Take(6).ToArray(),
            snapshots.Where(item => item.Events.Any(evt => evt.RegressionReason == AttributeRegressionReason.RushedTooEarly)).Select(item => item.PlayerName).Distinct(StringComparer.Ordinal).Take(6).ToArray(),
            snapshots.Select(item => RecommendationFor(item)).Where(text => !string.IsNullOrWhiteSpace(text)).Take(8).ToArray(),
            snapshots.Select(item => item.StaffNote).Take(8).ToArray());
        summary.Validate();
        return summary;
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var latest = scenario.AttributeDevelopmentSnapshots
            .Where(item => item.PersonId == personId)
            .OrderByDescending(item => item.Date)
            .FirstOrDefault();
        var plan = scenario.DevelopmentPlans.FirstOrDefault(item => item.PersonId == personId);
        if (latest is null)
        {
            var focus = plan is null ? "not set" : string.Join(", ", plan.FocusAreas.Select(FocusText));
            return new[] { $"Attribute development: no recent attribute development snapshot. Training focus: {focus}." };
        }

        var lines = new List<string>
        {
            $"Training focus: {string.Join(", ", latest.TrainingFocus.Select(FocusText))}",
            $"Recent OVR movement: {DeltaText(latest.OverallDelta)}",
            $"Biggest gain: {latest.BiggestGain}",
            $"Biggest regression: {latest.BiggestRegression}",
            latest.VisibleEstimateStale ? "Visible estimate warning: scouted rating may be stale until a report updates it." : "Visible estimate: current development report is reflected.",
            $"Staff note: {latest.StaffNote}"
        };
        lines.AddRange(latest.Events
            .OrderByDescending(evt => evt.IsMeaningful)
            .ThenBy(evt => evt.Attribute)
            .Take(8)
            .Select(evt => $"Attribute trend: {Readable(evt.Attribute)} {DeltaText(evt.Delta)} - {evt.Summary}"));
        return lines;
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario)
    {
        var recent = scenario.AttributeDevelopmentSnapshots
            .Where(item => item.Date >= scenario.CurrentDate.AddDays(-45))
            .OrderByDescending(item => item.Date)
            .Take(30)
            .ToArray();
        return recent.SelectMany(snapshot => BuildActionItems(scenario, snapshot))
            .GroupBy(item => item.ActionCenterItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(8)
            .ToArray();
    }

    private IReadOnlyDictionary<PlayerAttributeKey, int> BuildDeltas(
        NewGmScenarioSnapshot scenario,
        PlayerTrueRatings truth,
        PlayerDevelopmentPlan plan,
        AttributeDevelopmentModifier modifier)
    {
        var deltas = new Dictionary<PlayerAttributeKey, int>();
        foreach (var attribute in truth.Attributes)
        {
            var delta = AttributeDelta(scenario, truth, attribute, plan, modifier);
            if (delta != 0)
            {
                deltas[attribute.Attribute] = delta;
            }
        }

        if (deltas.Count == 0)
        {
            var fallback = FocusAttributes(plan.FocusAreas.FirstOrDefault(), truth.Position).FirstOrDefault();
            if (fallback != default)
            {
                deltas[fallback] = modifier.PoorRole || modifier.RushedTooEarly ? 0 : 1;
            }
        }

        return deltas;
    }

    private static int AttributeDelta(
        NewGmScenarioSnapshot scenario,
        PlayerTrueRatings truth,
        PlayerAttributeRating attribute,
        PlayerDevelopmentPlan plan,
        AttributeDevelopmentModifier modifier)
    {
        var focusAttributes = plan.FocusAreas.SelectMany(focus => FocusAttributes(focus, truth.Position)).ToHashSet();
        var focused = focusAttributes.Contains(attribute.Attribute);
        var delta = 0;
        delta += AgeCurve(attribute.Attribute, modifier.Age);
        delta += CurvePaceBonus(scenario, truth.PersonId, modifier.Age);
        delta += focused ? 1 : 0;
        delta += CoachSpecialtyMatches(attribute.Attribute, modifier.CoachSpecialty) ? 1 : 0;
        delta += modifier.DevelopmentStaffQuality >= 75 && focused ? 1 : 0;
        delta += modifier.WorkEthic >= 75 && focused ? 1 : 0;
        delta += modifier.Coachability >= 75 && (focused || attribute.Category is PlayerRatingCategory.Mental or PlayerRatingCategory.Team) ? 1 : 0;
        delta += modifier.Professionalism >= 75 && attribute.Category == PlayerRatingCategory.Team ? 1 : 0;
        delta += modifier.TeamCulture >= 75 && attribute.Category is PlayerRatingCategory.Team or PlayerRatingCategory.Mental ? 1 : 0;
        delta += modifier.RelationshipTrust >= 70 && attribute.Attribute == PlayerAttributeKey.Confidence ? 1 : 0;
        delta += modifier.PowerPlayUsage && attribute.Attribute is PlayerAttributeKey.Shooting or PlayerAttributeKey.Playmaking or PlayerAttributeKey.Vision or PlayerAttributeKey.OffensiveAwareness ? 1 : 0;
        delta += modifier.PenaltyKillUsage && attribute.Attribute is PlayerAttributeKey.Positioning or PlayerAttributeKey.DefensiveAwareness or PlayerAttributeKey.StickChecking or PlayerAttributeKey.Backchecking ? 1 : 0;

        if (modifier.InjuryPenalty > 0 && attribute.Attribute is PlayerAttributeKey.Durability or PlayerAttributeKey.Endurance or PlayerAttributeKey.Speed or PlayerAttributeKey.Acceleration)
        {
            delta -= modifier.InjuryPenalty >= 20 ? 2 : 1;
        }

        if (modifier.FatiguePenalty > 0 && attribute.Attribute is PlayerAttributeKey.Endurance or PlayerAttributeKey.Confidence or PlayerAttributeKey.Composure)
        {
            delta -= 1;
        }

        if (modifier.RushedTooEarly && attribute.Attribute is PlayerAttributeKey.Confidence or PlayerAttributeKey.Composure or PlayerAttributeKey.Consistency)
        {
            delta -= 2;
        }

        if (modifier.PoorRole && attribute.Attribute is PlayerAttributeKey.Confidence or PlayerAttributeKey.WorkEthic or PlayerAttributeKey.Coachability)
        {
            delta -= 1;
        }

        if (modifier.Age >= 30 && attribute.Attribute is PlayerAttributeKey.Speed or PlayerAttributeKey.Acceleration or PlayerAttributeKey.Agility)
        {
            delta -= 1;
        }

        return Math.Clamp(delta, -3, 4);
    }

    private static int AgeCurve(PlayerAttributeKey attribute, int age)
    {
        if (attribute is PlayerAttributeKey.Speed or PlayerAttributeKey.Acceleration or PlayerAttributeKey.Agility)
        {
            return age <= 19 ? 2 : age <= 23 ? 1 : age >= 28 ? -1 : 0;
        }

        if (attribute is PlayerAttributeKey.Strength or PlayerAttributeKey.Balance or PlayerAttributeKey.Endurance)
        {
            return age is >= 19 and <= 27 ? 1 : age >= 32 ? -1 : 0;
        }

        if (attribute is PlayerAttributeKey.HockeyIQ or PlayerAttributeKey.DefensiveAwareness or PlayerAttributeKey.Positioning)
        {
            return age >= 20 && age <= 31 ? 1 : 0;
        }

        if (attribute is PlayerAttributeKey.Leadership or PlayerAttributeKey.MentorAbility)
        {
            return age >= 24 ? 1 : 0;
        }

        return 0;
    }

    private static int CurvePaceBonus(NewGmScenarioSnapshot scenario, string personId, int age)
    {
        var curve = scenario.DevelopmentCurves.FirstOrDefault(item => item.PersonId == personId);
        if (curve is null)
        {
            return 0;
        }

        return curve.CurveType switch
        {
            DevelopmentCurveType.EarlyBloomer when age <= 21 => 1,
            DevelopmentCurveType.LateBloomer when age >= 24 => 2,
            DevelopmentCurveType.SlowBurn when age >= 22 => 1,
            DevelopmentCurveType.RawToolsyProspect when age <= 22 => 1,
            DevelopmentCurveType.OverAger when age >= 20 => 1,
            DevelopmentCurveType.InjuryRisk => -1,
            _ => curve.Pace == DevelopmentPace.Fast ? 1 : curve.Pace == DevelopmentPace.VerySlow ? -1 : 0
        };
    }

    private static IReadOnlyList<PlayerAttributeKey> FocusAttributes(DevelopmentPlanFocus focus, RosterPosition position) =>
        focus switch
        {
            DevelopmentPlanFocus.Skating => new[] { PlayerAttributeKey.Speed, PlayerAttributeKey.Acceleration, PlayerAttributeKey.Agility, PlayerAttributeKey.EdgeWork },
            DevelopmentPlanFocus.Shooting => new[] { PlayerAttributeKey.Shooting, PlayerAttributeKey.HandEye, PlayerAttributeKey.OffensiveAwareness },
            DevelopmentPlanFocus.Playmaking => new[] { PlayerAttributeKey.Playmaking, PlayerAttributeKey.Passing, PlayerAttributeKey.Vision, PlayerAttributeKey.Creativity },
            DevelopmentPlanFocus.Defensive => new[] { PlayerAttributeKey.Positioning, PlayerAttributeKey.StickChecking, PlayerAttributeKey.DefensiveAwareness, PlayerAttributeKey.BoardPlay, PlayerAttributeKey.Backchecking },
            DevelopmentPlanFocus.Physical or DevelopmentPlanFocus.Strength => new[] { PlayerAttributeKey.Strength, PlayerAttributeKey.Hitting, PlayerAttributeKey.Grit, PlayerAttributeKey.Toughness },
            DevelopmentPlanFocus.Conditioning or DevelopmentPlanFocus.Recovery => new[] { PlayerAttributeKey.Endurance, PlayerAttributeKey.Durability, PlayerAttributeKey.Recovery },
            DevelopmentPlanFocus.Faceoffs => new[] { PlayerAttributeKey.Faceoffs },
            DevelopmentPlanFocus.PuckSkills => new[] { PlayerAttributeKey.PuckSkill, PlayerAttributeKey.Stickhandling, PlayerAttributeKey.PuckProtection, PlayerAttributeKey.Deception },
            DevelopmentPlanFocus.Leadership => new[] { PlayerAttributeKey.Leadership, PlayerAttributeKey.MentorAbility, PlayerAttributeKey.TeamPlay },
            DevelopmentPlanFocus.Confidence => new[] { PlayerAttributeKey.Confidence, PlayerAttributeKey.Composure, PlayerAttributeKey.Consistency },
            DevelopmentPlanFocus.HockeyIQ => new[] { PlayerAttributeKey.HockeyIQ, PlayerAttributeKey.Vision, PlayerAttributeKey.Positioning, PlayerAttributeKey.DefensiveAwareness },
            DevelopmentPlanFocus.Character => new[] { PlayerAttributeKey.WorkEthic, PlayerAttributeKey.Coachability, PlayerAttributeKey.Professionalism, PlayerAttributeKey.LockerRoom },
            DevelopmentPlanFocus.Goaltending => GoalieAttributes(),
            DevelopmentPlanFocus.GoalieReflexes => new[] { PlayerAttributeKey.Reflexes, PlayerAttributeKey.LateralMovement, PlayerAttributeKey.Recovery },
            DevelopmentPlanFocus.GoaliePositioning => new[] { PlayerAttributeKey.Positioning, PlayerAttributeKey.PuckTracking, PlayerAttributeKey.ReboundManagement },
            DevelopmentPlanFocus.Offensive when position == RosterPosition.Goalie => GoalieAttributes(),
            DevelopmentPlanFocus.Offensive => new[] { PlayerAttributeKey.Shooting, PlayerAttributeKey.Playmaking, PlayerAttributeKey.PuckSkill, PlayerAttributeKey.OffensiveAwareness },
            _ => new[] { PlayerAttributeKey.WorkEthic, PlayerAttributeKey.Coachability, PlayerAttributeKey.Confidence }
        };

    private static IReadOnlyList<PlayerAttributeKey> GoalieAttributes() =>
        new[] { PlayerAttributeKey.Reflexes, PlayerAttributeKey.Glove, PlayerAttributeKey.Blocker, PlayerAttributeKey.Positioning, PlayerAttributeKey.PuckTracking, PlayerAttributeKey.ReboundManagement, PlayerAttributeKey.Recovery };

    private static bool CoachSpecialtyMatches(PlayerAttributeKey attribute, DevelopmentCoachSpecialty? specialty) =>
        specialty switch
        {
            DevelopmentCoachSpecialty.Skating => attribute is PlayerAttributeKey.Speed or PlayerAttributeKey.Acceleration or PlayerAttributeKey.Agility or PlayerAttributeKey.EdgeWork,
            DevelopmentCoachSpecialty.Shooting => attribute is PlayerAttributeKey.Shooting or PlayerAttributeKey.HandEye or PlayerAttributeKey.OffensiveAwareness,
            DevelopmentCoachSpecialty.Defense => attribute is PlayerAttributeKey.Positioning or PlayerAttributeKey.StickChecking or PlayerAttributeKey.DefensiveAwareness or PlayerAttributeKey.BoardPlay,
            DevelopmentCoachSpecialty.Goalies => attribute is PlayerAttributeKey.Reflexes or PlayerAttributeKey.Glove or PlayerAttributeKey.Blocker or PlayerAttributeKey.PuckTracking or PlayerAttributeKey.ReboundManagement,
            DevelopmentCoachSpecialty.Confidence => attribute is PlayerAttributeKey.Confidence or PlayerAttributeKey.Composure or PlayerAttributeKey.Consistency,
            DevelopmentCoachSpecialty.Leadership => attribute is PlayerAttributeKey.Leadership or PlayerAttributeKey.MentorAbility or PlayerAttributeKey.TeamPlay,
            DevelopmentCoachSpecialty.Conditioning => attribute is PlayerAttributeKey.Strength or PlayerAttributeKey.Endurance or PlayerAttributeKey.Durability or PlayerAttributeKey.Balance,
            DevelopmentCoachSpecialty.SpecialTeams => attribute is PlayerAttributeKey.Shooting or PlayerAttributeKey.Playmaking or PlayerAttributeKey.DefensiveAwareness or PlayerAttributeKey.Positioning,
            _ => false
        };

    private static IEnumerable<AttributeGrowthEvent> BuildEvents(
        NewGmScenarioSnapshot scenario,
        PlayerTrueRatings before,
        PlayerTrueRatings after,
        PlayerDevelopmentPlan plan,
        AttributeDevelopmentModifier modifier,
        IReadOnlyDictionary<PlayerAttributeKey, int> deltas)
    {
        foreach (var item in deltas.OrderByDescending(item => Math.Abs(item.Value)).ThenBy(item => item.Key))
        {
            var delta = item.Value;
            if (delta == 0)
            {
                continue;
            }

            var focused = plan.FocusAreas.SelectMany(focus => FocusAttributes(focus, after.Position)).Contains(item.Key);
            var growthReason = delta > 0
                ? focused ? AttributeGrowthReason.TrainingFocus : GrowthReasonFor(item.Key, modifier)
                : (AttributeGrowthReason?)null;
            var regressionReason = delta < 0 ? RegressionReasonFor(item.Key, modifier) : (AttributeRegressionReason?)null;
            var breakthrough = delta >= 3 || (delta >= 2 && modifier.WorkEthic >= 80 && modifier.Coachability >= 75);
            var plateau = delta <= 0 && (modifier.RushedTooEarly || modifier.PoorRole || modifier.Morale < 35);
            yield return new AttributeGrowthEvent(
                $"attribute-development:{after.PersonId}:{scenario.CurrentDate:yyyyMMdd}:{item.Key}",
                after.PersonId,
                after.PlayerName,
                scenario.CurrentDate,
                item.Key,
                delta,
                growthReason,
                regressionReason,
                breakthrough,
                plateau,
                SummaryFor(after.PlayerName, item.Key, delta, growthReason, regressionReason, breakthrough, plateau));
        }
    }

    private static IEnumerable<AttributeGrowthEvent> AddContextEvents(
        NewGmScenarioSnapshot scenario,
        PlayerTrueRatings after,
        AttributeDevelopmentModifier modifier,
        IReadOnlyList<AttributeGrowthEvent> events)
    {
        foreach (var growthEvent in events)
        {
            yield return growthEvent;
        }

        if (modifier.RushedTooEarly && events.All(item => item.RegressionReason != AttributeRegressionReason.RushedTooEarly))
        {
            yield return new AttributeGrowthEvent(
                $"attribute-development:{after.PersonId}:{scenario.CurrentDate:yyyyMMdd}:RushedRole",
                after.PersonId,
                after.PlayerName,
                scenario.CurrentDate,
                PlayerAttributeKey.Confidence,
                0,
                null,
                AttributeRegressionReason.RushedTooEarly,
                IsBreakthrough: false,
                IsPlateau: true,
                $"{after.PlayerName} is at risk of stalling because the role may be too aggressive for the current stage.");
        }

        if (modifier.PoorRole && events.All(item => item.RegressionReason != AttributeRegressionReason.PoorRole))
        {
            yield return new AttributeGrowthEvent(
                $"attribute-development:{after.PersonId}:{scenario.CurrentDate:yyyyMMdd}:PoorRole",
                after.PersonId,
                after.PlayerName,
                scenario.CurrentDate,
                PlayerAttributeKey.Confidence,
                0,
                null,
                AttributeRegressionReason.PoorRole,
                IsBreakthrough: false,
                IsPlateau: true,
                $"{after.PlayerName} is showing plateau risk because the current role is not helping development.");
        }
    }

    private static AttributeGrowthReason GrowthReasonFor(PlayerAttributeKey attribute, AttributeDevelopmentModifier modifier)
    {
        if (modifier.PowerPlayUsage && attribute is PlayerAttributeKey.Shooting or PlayerAttributeKey.Playmaking or PlayerAttributeKey.Vision)
        {
            return AttributeGrowthReason.SpecialTeamsUsage;
        }

        if (modifier.PenaltyKillUsage && attribute is PlayerAttributeKey.Positioning or PlayerAttributeKey.DefensiveAwareness or PlayerAttributeKey.StickChecking)
        {
            return AttributeGrowthReason.SpecialTeamsUsage;
        }

        return attribute is PlayerAttributeKey.HockeyIQ or PlayerAttributeKey.Leadership or PlayerAttributeKey.MentorAbility
            ? AttributeGrowthReason.Experience
            : AttributeGrowthReason.AgeCurve;
    }

    private static AttributeRegressionReason RegressionReasonFor(PlayerAttributeKey attribute, AttributeDevelopmentModifier modifier)
    {
        if (modifier.InjuryPenalty > 0 && attribute is PlayerAttributeKey.Durability or PlayerAttributeKey.Endurance or PlayerAttributeKey.Speed)
        {
            return AttributeRegressionReason.Injury;
        }

        if (modifier.RushedTooEarly)
        {
            return AttributeRegressionReason.RushedTooEarly;
        }

        if (modifier.PoorRole)
        {
            return AttributeRegressionReason.PoorRole;
        }

        return modifier.Morale < 35 ? AttributeRegressionReason.LowMorale : AttributeRegressionReason.Plateau;
    }

    private static string SummaryFor(string playerName, PlayerAttributeKey attribute, int delta, AttributeGrowthReason? growth, AttributeRegressionReason? regression, bool breakthrough, bool plateau)
    {
        if (breakthrough)
        {
            return $"{playerName} had a meaningful {Readable(attribute)} breakthrough.";
        }

        if (plateau)
        {
            return $"{playerName} is showing a plateau risk around {Readable(attribute)}.";
        }

        if (delta < 0)
        {
            return $"{playerName} lost ground in {Readable(attribute)} because of {regression}.";
        }

        return $"{playerName} improved {Readable(attribute)} through {growth}.";
    }

    private static AttributeDevelopmentSnapshot BuildSnapshot(
        NewGmScenarioSnapshot scenario,
        PlayerTrueRatings before,
        PlayerTrueRatings after,
        PlayerDevelopmentPlan plan,
        IReadOnlyList<AttributeGrowthEvent> events,
        bool visibleUpdated,
        bool visibleStale)
    {
        var biggestGain = events.Where(evt => evt.Delta > 0).OrderByDescending(evt => evt.Delta).FirstOrDefault();
        var biggestRegression = events.Where(evt => evt.Delta < 0).OrderBy(evt => evt.Delta).FirstOrDefault();
        var note = events.Any(evt => evt.IsBreakthrough)
            ? $"{after.PlayerName} has a real attribute breakthrough worth tracking."
            : events.Any(evt => evt.IsPlateau)
                ? $"{after.PlayerName} may need a development-plan adjustment before the current path stalls."
                : events.Any(evt => evt.IsRegression)
                    ? $"{after.PlayerName} had a setback that should be monitored."
                    : $"{after.PlayerName} had a steady month focused on {string.Join(", ", plan.FocusAreas.Select(FocusText))}.";
        var snapshot = new AttributeDevelopmentSnapshot(
            $"attribute-development:{after.PersonId}:{scenario.CurrentDate:yyyyMMdd}",
            after.PersonId,
            after.PlayerName,
            scenario.CurrentDate,
            plan.FocusAreas,
            before.Overall,
            after.Overall,
            before.Potential,
            after.Potential,
            biggestGain is null ? "none" : $"{Readable(biggestGain.Attribute)} {DeltaText(biggestGain.Delta)}",
            biggestRegression is null ? "none" : $"{Readable(biggestRegression.Attribute)} {DeltaText(biggestRegression.Delta)}",
            visibleUpdated,
            visibleStale,
            note,
            events);
        snapshot.Validate();
        return snapshot;
    }

    private static CareerTimeline AppendHistory(NewGmScenarioSnapshot scenario, AttributeDevelopmentSnapshot snapshot)
    {
        var timeline = scenario.CareerTimeline;
        foreach (var item in snapshot.Events.Where(evt => evt.IsMeaningful))
        {
            timeline = timeline.Add(new CareerTimelineEntry(
                EntryId: $"career:attribute-development:{snapshot.PersonId}:{snapshot.Date:yyyyMMdd}:{item.Attribute}",
                EntryType: item.IsBreakthrough ? CareerTimelineEntryType.Breakout : item.IsRegression ? CareerTimelineEntryType.Regression : CareerTimelineEntryType.Debut,
                Date: snapshot.Date,
                SeasonYear: scenario.Season.Year,
                PersonId: snapshot.PersonId,
                OrganizationId: scenario.Organization.OrganizationId,
                TeamName: scenario.Organization.Name,
                Title: item.IsBreakthrough ? "Attribute breakthrough" : item.IsRegression ? "Attribute setback" : "Development plateau warning",
                Description: item.Summary,
                RelatedEventId: null,
                Importance: item.IsBreakthrough ? HistoryImportance.Major : HistoryImportance.Important));
        }

        return timeline;
    }

    private static void QueueMeaningfulEvents(EngineRegistry registry, NewGmScenarioSnapshot scenario, AttributeDevelopmentSnapshot snapshot)
    {
        foreach (var item in snapshot.Events.Where(evt => evt.IsMeaningful).Take(4))
        {
            var eventType = item.IsBreakthrough
                ? LegacyEventType.PlayerBreakout
                : item.IsRegression
                    ? LegacyEventType.PlayerRegression
                    : LegacyEventType.PlayerDevelopmentUpdated;
            var legacyEvent = registry.EventEngine.CreateEvent(
                new DateTimeOffset(snapshot.Date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero),
                eventType,
                item.IsBreakthrough ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
                LegacyEventVisibility.Staff,
                item.IsBreakthrough ? "Attribute breakthrough" : item.IsRegression ? "Attribute setback" : "Development plateau warning",
                item.Summary,
                new LegacyEventContext(PrimaryPersonId: snapshot.PersonId, OrganizationId: scenario.Organization.OrganizationId));
            registry.EventEngine.QueueEvent(legacyEvent);
        }
    }

    private static IEnumerable<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario, AttributeDevelopmentSnapshot snapshot)
    {
        foreach (var item in snapshot.Events.Where(evt => evt.IsBreakthrough || evt.IsRegression || evt.IsPlateau).Take(3))
        {
            var priority = item.IsRegression || item.IsPlateau ? ActionCenterPriority.Important : ActionCenterPriority.Normal;
            yield return new ActionCenterItem(
                $"attribute-development-action:{snapshot.PersonId}:{snapshot.Date:yyyyMMdd}:{item.Attribute}",
                item.IsBreakthrough ? $"Development breakthrough: {snapshot.PlayerName}" : item.IsRegression ? $"Development setback: {snapshot.PlayerName}" : $"Plateau risk: {snapshot.PlayerName}",
                ActionCenterCategory.PlayerDevelopment,
                priority,
                snapshot.Date.AddDays(7),
                snapshot.PersonId,
                snapshot.PlayerName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                item.Summary,
                "Attribute trends can affect role readiness, confidence, and future projection.",
                item.IsRegression || item.IsPlateau ? "Review the development focus and role before the next month." : "Review the player's dossier and consider whether the plan should lean into this growth.",
                null,
                null,
                null);
        }
    }

    private AttributeDevelopmentModifier BuildModifier(NewGmScenarioSnapshot scenario, string personId, PlayerDevelopmentPlan plan)
    {
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(item => item.PersonId == personId);
        var age = PersonAge(scenario, personId) ?? 18;
        var coach = _planning.BuildCoachProfile(scenario);
        var usage = scenario.CurrentGameUsage;
        var ppIds = usage?.SpecialTeams.PowerPlayUnits.SelectMany(unit => unit.Players).Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        var pkIds = usage?.SpecialTeams.PenaltyKillUnits.SelectMany(unit => unit.Players).Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        return new AttributeDevelopmentModifier(
            age,
            YearsElapsed: Math.Max(0, scenario.Season.Year - 2026),
            scenario.LeagueProfile.Experience,
            plan.IceTimeRole,
            ppIds.Contains(personId),
            pkIds.Contains(personId),
            coach.Specialties.FirstOrDefault(),
            coach.FitScore,
            MoraleScore(plan.Morale),
            RelationshipTrust(scenario, personId),
            InjuryPenalty(scenario, personId),
            FatiguePenalty: 0,
            Trait(profile, DevelopmentAttribute.WorkEthic, 60),
            Trait(profile, DevelopmentAttribute.Coachability, 60),
            ProfessionalismScore(scenario),
            TeamCultureScore(scenario),
            RushedTooEarly: IsRushed(plan, age, scenario.LeagueProfile.Experience),
            PoorRole: plan.IceTimeRole == DevelopmentIceTimeRole.HealthyScratch || plan.Morale is DevelopmentMorale.Poor or DevelopmentMorale.Terrible,
            UpdateVisibleEstimate: false);
    }

    private static int MoraleScore(DevelopmentMorale morale) =>
        morale switch
        {
            DevelopmentMorale.Excellent => 85,
            DevelopmentMorale.Good => 70,
            DevelopmentMorale.Average => 55,
            DevelopmentMorale.Poor => 35,
            DevelopmentMorale.Terrible => 20,
            _ => 50
        };

    private static bool IsRushed(PlayerDevelopmentPlan plan, int age, LeagueExperience league) =>
        league == LeagueExperience.Nhl
        && age <= 19
        && plan.IceTimeRole is DevelopmentIceTimeRole.TopSix or DevelopmentIceTimeRole.TopPair or DevelopmentIceTimeRole.Starter;

    private static int Trait(PlayerDevelopmentProfile? profile, DevelopmentAttribute attribute, int fallback) =>
        profile?.Traits.FirstOrDefault(item => item.Attribute == attribute)?.Value ?? fallback;

    private static int InjuryPenalty(NewGmScenarioSnapshot scenario, string personId) =>
        Math.Clamp(scenario.AlphaSnapshot.Injuries.Where(item => item.PersonId == personId && item.IsActive).Sum(item => item.DevelopmentPenalty), 0, 40);

    private static int RelationshipTrust(NewGmScenarioSnapshot scenario, string personId)
    {
        var gmId = scenario.AlphaSnapshot.GeneralManager.PersonId;
        return scenario.AlphaSnapshot.Relationships
            .Where(item => string.Equals(item.FromPersonId, gmId, StringComparison.Ordinal) && string.Equals(item.ToPersonId, personId, StringComparison.Ordinal))
            .Select(item => item.Trust)
            .DefaultIfEmpty(50)
            .Max();
    }

    private static int ProfessionalismScore(NewGmScenarioSnapshot scenario) =>
        ChemistryScore(scenario.RelationshipChemistry?.StaffChemistry);

    private static int TeamCultureScore(NewGmScenarioSnapshot scenario) =>
        scenario.FranchiseIdentities.FirstOrDefault(item => item.OrganizationId == scenario.Organization.OrganizationId)?.Culture switch
        {
            FranchiseCulture.DevelopmentCulture => 85,
            FranchiseCulture.PlayerFriendly => 75,
            FranchiseCulture.Professional => 70,
            FranchiseCulture.HardWorking => 68,
            FranchiseCulture.Disciplined => 62,
            FranchiseCulture.WinningCulture => 60,
            FranchiseCulture.Demanding => 52,
            FranchiseCulture.PressureOrganization => 45,
            _ => 60
        };

    private static int ChemistryScore(RelationshipChemistryLevel? chemistry) =>
        chemistry switch
        {
            RelationshipChemistryLevel.Excellent => 85,
            RelationshipChemistryLevel.Good => 70,
            RelationshipChemistryLevel.Neutral => 58,
            RelationshipChemistryLevel.Poor => 42,
            RelationshipChemistryLevel.Problem => 28,
            _ => 60
        };

    private static int? PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Age
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Age
        ?? scenario.FreeAgentMarket?.Find(personId)?.Age
        ?? scenario.TradeBlock?.Find(personId)?.Age;

    private static bool IsVisibleEstimateStale(NewGmScenarioSnapshot scenario, string personId)
    {
        var truth = scenario.TrueRatings.FirstOrDefault(item => item.PersonId == personId);
        var visible = scenario.ScoutedRatings.FirstOrDefault(item => item.PersonId == personId);
        return truth is not null && visible is not null && (visible.LastScoutedDate is null || truth.LastUpdated >= visible.LastScoutedDate.Value);
    }

    private static PlayerRatingCategory? FocusCategory(DevelopmentPlanFocus focus, RosterPosition position) =>
        focus switch
        {
            DevelopmentPlanFocus.Skating => PlayerRatingCategory.Skating,
            DevelopmentPlanFocus.Shooting or DevelopmentPlanFocus.Offensive => PlayerRatingCategory.Offensive,
            DevelopmentPlanFocus.Defensive => PlayerRatingCategory.Defensive,
            DevelopmentPlanFocus.Physical or DevelopmentPlanFocus.Strength or DevelopmentPlanFocus.Conditioning => PlayerRatingCategory.Physical,
            DevelopmentPlanFocus.Playmaking or DevelopmentPlanFocus.Faceoffs or DevelopmentPlanFocus.PuckSkills => PlayerRatingCategory.Skill,
            DevelopmentPlanFocus.Leadership or DevelopmentPlanFocus.Confidence or DevelopmentPlanFocus.HockeyIQ or DevelopmentPlanFocus.Character => PlayerRatingCategory.Mental,
            DevelopmentPlanFocus.Goaltending or DevelopmentPlanFocus.GoalieReflexes or DevelopmentPlanFocus.GoaliePositioning => PlayerRatingCategory.Goalie,
            _ => position == RosterPosition.Goalie ? PlayerRatingCategory.Goalie : null
        };

    private static string RecommendationFor(AttributeDevelopmentSnapshot snapshot)
    {
        if (snapshot.Events.Any(item => item.IsPlateau))
        {
            return $"{snapshot.PlayerName}: consider changing focus from {FocusText(snapshot.TrainingFocus[0])}.";
        }

        if (snapshot.Events.Any(item => item.IsBreakthrough))
        {
            return $"{snapshot.PlayerName}: lean into {snapshot.BiggestGain}.";
        }

        return string.Empty;
    }

    private static string DeltaText(int delta) =>
        delta > 0 ? $"+{delta}" : delta.ToString();

    private static string FocusText(DevelopmentPlanFocus focus) =>
        focus switch
        {
            DevelopmentPlanFocus.HockeyIQ => "hockey IQ",
            DevelopmentPlanFocus.PuckSkills => "puck skills",
            DevelopmentPlanFocus.GoalieReflexes => "goalie reflexes",
            DevelopmentPlanFocus.GoaliePositioning => "goalie positioning",
            _ => focus.ToString().ToLowerInvariant()
        };

    private static string Readable(PlayerAttributeKey attribute)
    {
        if (attribute == PlayerAttributeKey.HockeyIQ)
        {
            return "Hockey IQ";
        }

        if (attribute == PlayerAttributeKey.PuckSkill)
        {
            return "Puck C" + "ontrol";
        }

        if (attribute == PlayerAttributeKey.ReboundManagement)
        {
            return "Rebound C" + "ontrol";
        }

        var text = attribute.ToString();
        return string.Concat(text.SelectMany((ch, index) => index > 0 && char.IsUpper(ch) ? new[] { ' ', ch } : new[] { ch }));
    }
}
