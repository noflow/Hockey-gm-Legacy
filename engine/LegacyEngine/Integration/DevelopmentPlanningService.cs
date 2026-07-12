using LegacyEngine.Development;
using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class DevelopmentPlanningService
{
    public IReadOnlyList<PlayerDevelopmentPlan> EnsurePlans(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var existing = scenario.DevelopmentPlans
            .GroupBy(plan => plan.PersonId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var coach = FindDevelopmentCoach(scenario);

        return scenario.AlphaSnapshot.DevelopmentProfiles
            .Select((profile, index) => existing.TryGetValue(profile.PersonId, out var plan)
                ? plan
                : CreateDefaultPlan(scenario, profile, coach, index))
            .ToArray();
    }

    public DevelopmentCoachProfile BuildCoachProfile(NewGmScenarioSnapshot scenario)
    {
        var coach = FindDevelopmentCoach(scenario);
        var coachName = PersonName(scenario, coach.PersonId);
        var development = coach.Attributes.CoachingScore(StaffCoachingAttribute.Development);
        var specialties = BuildCoachSpecialties(coach).ToArray();
        var summary = development >= 75
            ? $"{coachName} is a strong development voice with clear player-growth habits."
            : development >= 55
                ? $"{coachName} gives the department steady development coverage."
                : $"{coachName} can help, but the club may need more specialized development support.";
        var profile = new DevelopmentCoachProfile(coach.PersonId, coachName, specialties, Math.Clamp(development, 0, 100), summary);
        profile.Validate();
        return profile;
    }

    public NewGmScenarioSnapshot EnsureScenarioPlans(NewGmScenarioSnapshot scenario)
    {
        var plans = EnsurePlans(scenario);
        return scenario with { DevelopmentPlans = plans };
    }

    public DevelopmentV2Result SetPlan(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        IReadOnlyList<DevelopmentPlanFocus> focusAreas,
        DevelopmentIceTimeRole role,
        string gmComment)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);

        var scenarioWithTargetProfile = EnsureScenarioPlanForPerson(scenario, personId);
        var plans = EnsurePlans(scenarioWithTargetProfile).ToList();
        var existing = plans.FirstOrDefault(plan => plan.PersonId == personId)
            ?? throw new ArgumentException("Player development plan was not found.", nameof(personId));
        var updatedPlan = existing with
        {
            FocusAreas = NormalizeFocusAreas(focusAreas),
            IceTimeRole = role,
            LastReviewed = scenarioWithTargetProfile.CurrentDate,
            GmComment = string.IsNullOrWhiteSpace(gmComment) ? $"GM updated plan to {role}." : gmComment.Trim(),
            CoachComment = BuildCoachComment(focusAreas, role)
        };
        updatedPlan.Validate();

        var index = plans.FindIndex(plan => plan.PersonId == personId);
        plans[index] = updatedPlan;
        var updatedScenario = scenarioWithTargetProfile with { DevelopmentPlans = plans };
        var progress = BuildProgress(updatedScenario, updatedPlan, DevelopmentOutcomeType.Progress, 0, Array.Empty<DevelopmentUpdate>());
        var recommendation = BuildRecommendation(updatedScenario, updatedPlan, progress);
        var inbox = CreateInboxItem(updatedScenario, progress, recommendation, "Development plan updated");
        var legacyEvent = CreateDevelopmentEvent(
            registry,
            updatedScenario,
            updatedPlan.PersonId,
            LegacyEventType.PlayerDevelopmentUpdated,
            "Development plan updated",
            $"{PersonName(updatedScenario, personId)} now has a {role} plan focused on {string.Join(", ", updatedPlan.FocusAreas)}.");
        var finalScenario = updatedScenario with
        {
            DevelopmentRecommendations = ReplaceRecommendation(updatedScenario.DevelopmentRecommendations, recommendation),
            CareerTimeline = updatedScenario.CareerTimeline.Add(new CareerTimelineEntry(
                EntryId: $"career:development-plan:{personId}:{updatedScenario.CurrentDate:yyyyMMdd}",
                EntryType: CareerTimelineEntryType.Breakout,
                Date: updatedScenario.CurrentDate,
                SeasonYear: updatedScenario.Season.Year,
                PersonId: personId,
                OrganizationId: updatedScenario.Organization.OrganizationId,
                TeamName: updatedScenario.Organization.Name,
                Title: "Development plan updated",
                Description: $"Plan focused on {string.Join(", ", updatedPlan.FocusAreas)} with {role} opportunity.",
                RelatedEventId: null,
                Importance: HistoryImportance.Normal))
        };

        var result = new DevelopmentV2Result(
            finalScenario,
            updatedPlan,
            progress,
            new[] { recommendation },
            new[] { inbox },
            $"Development plan updated for {PersonName(updatedScenario, personId)}. Event queued: {legacyEvent.EventId}.");
        result.Validate();
        return result;
    }

    public DevelopmentV2Result ApplyMonthlyProgress(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        int randomModifier = 0)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);

        var scenarioWithPlans = EnsureScenarioPlanForPerson(scenario, personId);
        var plan = scenarioWithPlans.DevelopmentPlans.FirstOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Development plan was not found.", nameof(personId));
        var profile = scenarioWithPlans.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Development profile was not found.", nameof(personId));
        var age = FindPerson(scenarioWithPlans, personId)?.CalculateAge(scenarioWithPlans.CurrentDate) ?? 18;
        var coachProfile = BuildCoachProfile(scenarioWithPlans);
        var factors = new DevelopmentFactor(
            Age: age,
            UpdateDate: scenarioWithPlans.CurrentDate,
            IceTimeScore: IceTimeScore(plan.IceTimeRole),
            FacilityBonus: 20,
            CoachingBonus: CoachFitBonus(plan, coachProfile),
            InjuryPenalty: InjuryPenalty(scenarioWithPlans, personId),
            RandomModifier: randomModifier);
        var development = registry.DevelopmentEngine.ApplyMonthlyUpdate(profile, factors);
        var outcome = DetermineOutcome(development, plan, factors);
        var progress = BuildProgress(scenarioWithPlans, plan, outcome, ConfidenceDelta(plan, development, factors), development.Updates);
        var updatedPlan = plan with
        {
            Confidence = Math.Clamp(plan.Confidence + progress.ConfidenceChange, 0, 100),
            Morale = UpdateMorale(plan.Morale, plan.IceTimeRole, progress.ConfidenceChange, development.IsRegression),
            LastReviewed = scenarioWithPlans.CurrentDate,
            CoachComment = progress.CoachComment
        };
        updatedPlan.Validate();

        var updatedProfiles = scenarioWithPlans.AlphaSnapshot.DevelopmentProfiles
            .Select(item => item.PersonId == personId ? development.UpdatedProfile : item)
            .ToArray();
        var plans = scenarioWithPlans.DevelopmentPlans
            .Select(item => item.PersonId == personId ? updatedPlan : item)
            .ToArray();
        var recommendation = BuildRecommendation(scenarioWithPlans, updatedPlan, progress);
        var inbox = ShouldCreateInbox(progress, recommendation)
            ? new[] { CreateInboxItem(scenarioWithPlans, progress, recommendation, "Development progress") }
            : Array.Empty<AlphaInboxItem>();
        var eventType = outcome == DevelopmentOutcomeType.Breakout
            ? LegacyEventType.PlayerBreakout
            : outcome == DevelopmentOutcomeType.Regression
                ? LegacyEventType.PlayerRegression
                : LegacyEventType.PlayerDevelopmentUpdated;
        CreateDevelopmentEvent(registry, scenarioWithPlans, personId, eventType, $"Development {outcome}", progress.Summary);

        var careerTimeline = scenarioWithPlans.CareerTimeline;
        if (outcome is DevelopmentOutcomeType.Breakout or DevelopmentOutcomeType.Regression)
        {
            careerTimeline = careerTimeline.Add(new CareerTimelineEntry(
                EntryId: $"career:development:{outcome}:{personId}:{scenarioWithPlans.CurrentDate:yyyyMMdd}",
                EntryType: outcome == DevelopmentOutcomeType.Breakout ? CareerTimelineEntryType.Breakout : CareerTimelineEntryType.Regression,
                Date: scenarioWithPlans.CurrentDate,
                SeasonYear: scenarioWithPlans.Season.Year,
                PersonId: personId,
                OrganizationId: scenarioWithPlans.Organization.OrganizationId,
                TeamName: scenarioWithPlans.Organization.Name,
                Title: outcome == DevelopmentOutcomeType.Breakout ? "Breakout development month" : "Development regression",
                Description: progress.Summary,
                RelatedEventId: null,
                Importance: outcome == DevelopmentOutcomeType.Breakout ? HistoryImportance.Major : HistoryImportance.Important));
        }

        var finalScenario = scenarioWithPlans with
        {
            AlphaSnapshot = scenarioWithPlans.AlphaSnapshot with { DevelopmentProfiles = updatedProfiles },
            DevelopmentPlans = plans,
            DevelopmentRecommendations = ReplaceRecommendation(scenarioWithPlans.DevelopmentRecommendations, recommendation),
            CareerTimeline = careerTimeline
        };
        var result = new DevelopmentV2Result(
            finalScenario,
            updatedPlan,
            progress,
            new[] { recommendation },
            inbox,
            progress.Summary);
        result.Validate();
        return result;
    }

    public DevelopmentReview GenerateYearlyReview(NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var scenarioWithPlans = EnsureScenarioPlanForPerson(scenario, personId);
        var person = FindPerson(scenarioWithPlans, personId)
            ?? throw new ArgumentException("Player was not found for development review.", nameof(personId));
        var plan = scenarioWithPlans.DevelopmentPlans.First(plan => plan.PersonId == personId);
        var profile = scenarioWithPlans.AlphaSnapshot.DevelopmentProfiles.First(profile => profile.PersonId == personId);
        var strengths = PublicTraitThemes(profile, descending: true).Take(3).ToArray();
        var weaknesses = PublicTraitThemes(profile, descending: false).Take(2).ToArray();
        var improved = plan.FocusAreas.Select(FocusText).Take(3).ToArray();
        var regressions = plan.Morale is DevelopmentMorale.Poor or DevelopmentMorale.Terrible
            ? new[] { "confidence and daily engagement need attention" }
            : Array.Empty<string>();
        var review = new DevelopmentReview(
            ReviewId: $"development-review:{personId}:{scenarioWithPlans.Season.Year}",
            PersonId: personId,
            PlayerName: person.Identity.DisplayName,
            SeasonYear: scenarioWithPlans.Season.Year,
            ReviewDate: scenarioWithPlans.CurrentDate,
            ImprovedThemes: improved,
            RegressionThemes: regressions,
            Strengths: strengths,
            Weaknesses: weaknesses,
            CoachComment: plan.CoachComment,
            ScoutComment: ScoutComment(scenarioWithPlans, personId),
            GmComment: string.IsNullOrWhiteSpace(plan.GmComment) ? "No GM comment yet." : plan.GmComment,
            FutureProjection: FutureProjection(plan, strengths));
        review.Validate();
        return review;
    }

    public NewGmScenarioSnapshot StoreYearlyReview(NewGmScenarioSnapshot scenario, DevelopmentReview review)
    {
        review.Validate();
        return scenario with
        {
            DevelopmentReviews = scenario.DevelopmentReviews
                .Where(item => item.ReviewId != review.ReviewId)
                .Append(review)
                .OrderByDescending(item => item.ReviewDate)
                .ToArray()
        };
    }

    public string BuildDossierSummary(NewGmScenarioSnapshot scenario, string personId)
    {
        var scenarioWithPlans = EnsureScenarioPlans(scenario);
        var plan = scenarioWithPlans.DevelopmentPlans.FirstOrDefault(plan => plan.PersonId == personId);
        if (plan is null)
        {
            return "No development plan is currently tracked.";
        }

        var latestReview = scenarioWithPlans.DevelopmentReviews
            .Where(review => review.PersonId == personId)
            .OrderByDescending(review => review.ReviewDate)
            .FirstOrDefault();
        var lines = new List<string>
        {
            $"Development plan: {string.Join(", ", plan.FocusAreas)}",
            $"Current focus: {FocusText(plan.FocusAreas[0])}",
            $"Ice-time role: {RoleText(plan.IceTimeRole)}",
            $"Confidence: {ConfidenceLabel(plan.Confidence)}",
            $"Morale: {plan.Morale}",
            $"Coach comments: {plan.CoachComment}",
            "Progress graph: monthly tracking placeholder; year-over-year growth will be summarized in reviews.",
            $"Development history: last reviewed {plan.LastReviewed:yyyy-MM-dd}."
        };

        if (latestReview is not null)
        {
            lines.Add($"Year-over-year growth: {string.Join(", ", latestReview.ImprovedThemes)}.");
            lines.Add($"Future projection: {latestReview.FutureProjection}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public IReadOnlyList<DevelopmentRecommendation> BuildMonthlyRecommendations(NewGmScenarioSnapshot scenario)
    {
        var scenarioWithPlans = EnsureScenarioPlans(scenario);
        return scenarioWithPlans.DevelopmentPlans
            .Select(plan => BuildRecommendation(scenarioWithPlans, plan, BuildProgress(scenarioWithPlans, plan, DevelopmentOutcomeType.Progress, 0, Array.Empty<DevelopmentUpdate>())))
            .Where(recommendation => recommendation.RecommendationType != DevelopmentRecommendationType.StayCourse)
            .Take(5)
            .ToArray();
    }

    private static PlayerDevelopmentPlan CreateDefaultPlan(
        NewGmScenarioSnapshot scenario,
        PlayerDevelopmentProfile profile,
        StaffMember coach,
        int index)
    {
        var focus = DefaultFocus(profile, scenario, index);
        var role = DefaultRole(scenario, profile.PersonId);
        var plan = new PlayerDevelopmentPlan(
            PersonId: profile.PersonId,
            FocusAreas: focus,
            IceTimeRole: role,
            Confidence: Math.Clamp(profile.TraitValue(DevelopmentAttribute.Confidence), 0, 100),
            Morale: DevelopmentMorale.Good,
            CoachPersonId: coach.PersonId,
            LastReviewed: scenario.CurrentDate,
            CoachComment: BuildCoachComment(focus, role),
            GmComment: string.Empty);
        plan.Validate();
        return plan;
    }

    /// <summary>
    /// Ensures a known player can be safely addressed by any development-facing
    /// workflow, including scenarios created before their profile was generated.
    /// </summary>
    public NewGmScenarioSnapshot EnsureScenarioPlanForPerson(NewGmScenarioSnapshot scenario, string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        var profileExists = scenario.AlphaSnapshot.DevelopmentProfiles.Any(profile => profile.PersonId == personId);
        if (profileExists)
        {
            return EnsureScenarioPlans(scenario);
        }

        var person = FindPerson(scenario, personId)
            ?? throw new ArgumentException("Player was not found for development planning.", nameof(personId));
        var age = person.CalculateAge(scenario.CurrentDate);
        var seed = StableHash(personId);
        var profile = CreateGeneratedProfile(personId, age, seed, scenario.CurrentDate);
        var alpha = scenario.AlphaSnapshot with
        {
            DevelopmentProfiles = scenario.AlphaSnapshot.DevelopmentProfiles
                .Append(profile)
                .ToArray()
        };
        return EnsureScenarioPlans(scenario with { AlphaSnapshot = alpha });
    }

    private static PlayerDevelopmentProfile CreateGeneratedProfile(string personId, int age, int seed, DateOnly currentDate)
    {
        var stage = age switch
        {
            <= 17 => DevelopmentStage.Prospect,
            <= 20 => DevelopmentStage.Junior,
            <= 24 => DevelopmentStage.YoungPro,
            <= 28 => DevelopmentStage.Prime,
            <= 32 => DevelopmentStage.Veteran,
            _ => DevelopmentStage.Declining
        };
        var currentAbility = stage switch
        {
            DevelopmentStage.Prospect => 34 + seed % 10,
            DevelopmentStage.Junior => 38 + seed % 12,
            DevelopmentStage.YoungPro => 44 + seed % 14,
            DevelopmentStage.Prime => 52 + seed % 16,
            DevelopmentStage.Veteran => 50 + seed % 15,
            _ => 44 + seed % 12
        };
        var potential = Math.Clamp(currentAbility + 12 + seed % 18, currentAbility, 100);
        var profile = new PlayerDevelopmentProfile(
            personId,
            currentAbility,
            potential,
            stage,
            GeneratedTraits(seed),
            currentDate);
        profile.Validate();
        return profile;
    }

    private static IReadOnlyList<DevelopmentTrait> GeneratedTraits(int seed) =>
        Enum.GetValues<DevelopmentAttribute>()
            .Select((attribute, index) =>
            {
                var value = attribute switch
                {
                    DevelopmentAttribute.WorkEthic => 56 + ((seed + index * 7) % 18),
                    DevelopmentAttribute.Coachability => 54 + ((seed + index * 5) % 18),
                    DevelopmentAttribute.Confidence => 50 + ((seed + index * 3) % 16),
                    _ => 47 + ((seed + index * 11) % 22)
                };
                return new DevelopmentTrait(attribute, Math.Clamp(value, 0, 100));
            })
            .ToArray();

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in value)
            {
                hash = hash * 31 + character;
            }

            return hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
        }
    }

    private static IReadOnlyList<DevelopmentPlanFocus> DefaultFocus(PlayerDevelopmentProfile profile, NewGmScenarioSnapshot scenario, int index)
    {
        var position = scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == profile.PersonId)?.Position
            ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == profile.PersonId)?.Bio?.Position
            ?? RosterPosition.Unknown;
        var primary = position switch
        {
            RosterPosition.Goalie => DevelopmentPlanFocus.Goaltending,
            RosterPosition.Defense => DevelopmentPlanFocus.Defensive,
            RosterPosition.Center => DevelopmentPlanFocus.Playmaking,
            RosterPosition.LeftWing or RosterPosition.RightWing => DevelopmentPlanFocus.Shooting,
            _ => index % 2 == 0 ? DevelopmentPlanFocus.Skating : DevelopmentPlanFocus.Balanced
        };
        return new[] { primary, DevelopmentPlanFocus.Confidence };
    }

    private static DevelopmentIceTimeRole DefaultRole(NewGmScenarioSnapshot scenario, string personId)
    {
        var roster = scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId);
        if (roster is null)
        {
            return DevelopmentIceTimeRole.JuniorReturn;
        }

        return roster.Position switch
        {
            RosterPosition.Goalie => DevelopmentIceTimeRole.Backup,
            RosterPosition.Defense => DevelopmentIceTimeRole.TopPair,
            _ => DevelopmentIceTimeRole.MiddleSix
        };
    }

    private static IReadOnlyList<DevelopmentPlanFocus> NormalizeFocusAreas(IReadOnlyList<DevelopmentPlanFocus> focusAreas)
    {
        if (focusAreas.Count == 0)
        {
            return new[] { DevelopmentPlanFocus.Balanced };
        }

        return focusAreas.Distinct().Take(3).ToArray();
    }

    private static StaffMember FindDevelopmentCoach(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers.FirstOrDefault(member =>
            member.EmploymentStatus == StaffEmploymentStatus.Employed
            && member.CurrentRole is StaffRole.DevelopmentCoach or StaffRole.SkillsCoach or StaffRole.AssistantCoach or StaffRole.HeadCoach)
        ?? scenario.StaffMembers.First(member => member.EmploymentStatus == StaffEmploymentStatus.Employed);

    private static IReadOnlyList<DevelopmentCoachSpecialty> BuildCoachSpecialties(StaffMember coach)
    {
        var development = coach.Attributes.CoachingScore(StaffCoachingAttribute.Development);
        var teaching = coach.Attributes.CoachingScore(StaffCoachingAttribute.Teaching);
        var motivation = coach.Attributes.CoachingScore(StaffCoachingAttribute.Motivation);
        var leadership = coach.Attributes.CoachingScore(StaffCoachingAttribute.Leadership);
        var tactics = coach.Attributes.CoachingScore(StaffCoachingAttribute.Tactics);
        var adaptability = coach.Attributes.CoachingScore(StaffCoachingAttribute.Adaptability);
        var specialties = new List<(DevelopmentCoachSpecialty Specialty, int Score)>
        {
            (DevelopmentCoachSpecialty.Skating, teaching + adaptability / 2),
            (DevelopmentCoachSpecialty.Shooting, development + teaching / 2),
            (DevelopmentCoachSpecialty.Defense, tactics + development / 2),
            (DevelopmentCoachSpecialty.Goalies, coach.CurrentRole is StaffRole.GoalieCoach or StaffRole.GoaltendingCoach ? 90 : development / 2),
            (DevelopmentCoachSpecialty.Confidence, motivation + communicationProxy(coach) / 2),
            (DevelopmentCoachSpecialty.Leadership, leadership + motivation / 2),
            (DevelopmentCoachSpecialty.Conditioning, disciplineProxy(coach) + development / 2),
            (DevelopmentCoachSpecialty.SpecialTeams, tactics + adaptability / 2)
        };

        return specialties
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Specialty)
            .Take(3)
            .Select(item => item.Specialty)
            .ToArray();

        static int communicationProxy(StaffMember member) => member.Attributes.CoachingScore(StaffCoachingAttribute.Communication);

        static int disciplineProxy(StaffMember member) => member.Attributes.CoachingScore(StaffCoachingAttribute.Discipline);
    }

    private static DevelopmentProgressSnapshot BuildProgress(
        NewGmScenarioSnapshot scenario,
        PlayerDevelopmentPlan plan,
        DevelopmentOutcomeType outcome,
        int confidenceChange,
        IReadOnlyList<DevelopmentUpdate> updates)
    {
        var playerName = PersonName(scenario, plan.PersonId);
        var improved = updates
            .Where(update => update.Change > 0)
            .OrderByDescending(update => update.Change)
            .Select(update => ThemeText(update.Attribute))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .DefaultIfEmpty(FocusText(plan.FocusAreas[0]))
            .ToArray();
        var regressed = updates
            .Where(update => update.Change < 0)
            .OrderBy(update => update.Change)
            .Select(update => ThemeText(update.Attribute))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var coachComment = outcome switch
        {
            DevelopmentOutcomeType.Breakout => $"{playerName} looks ready for a bigger challenge if the role is available.",
            DevelopmentOutcomeType.Regression => $"{playerName} needs a reset in workload, confidence, or recovery support.",
            DevelopmentOutcomeType.Plateau => $"{playerName} is not slipping, but the plan may need a sharper focus.",
            _ => BuildCoachComment(plan.FocusAreas, plan.IceTimeRole)
        };
        var scoutComment = ScoutComment(scenario, plan.PersonId);
        var summary = outcome switch
        {
            DevelopmentOutcomeType.Breakout => $"{playerName} has a breakout development trend around {string.Join(", ", improved)}.",
            DevelopmentOutcomeType.Regression => $"{playerName} is showing regression signals around {string.Join(", ", regressed.DefaultIfEmpty("confidence"))}.",
            DevelopmentOutcomeType.Plateau => $"{playerName} has plateaued; staff should revisit opportunity and focus.",
            _ => $"{playerName} is progressing through a {string.Join(", ", plan.FocusAreas)} plan with {RoleText(plan.IceTimeRole)} opportunity."
        };
        var progress = new DevelopmentProgressSnapshot(
            plan.PersonId,
            scenario.CurrentDate,
            outcome,
            improved,
            regressed,
            confidenceChange,
            plan.Morale,
            summary,
            coachComment,
            scoutComment);
        progress.Validate();
        return progress;
    }

    private static DevelopmentRecommendation BuildRecommendation(
        NewGmScenarioSnapshot scenario,
        PlayerDevelopmentPlan plan,
        DevelopmentProgressSnapshot progress)
    {
        var type = progress.Outcome switch
        {
            DevelopmentOutcomeType.Breakout => DevelopmentRecommendationType.ReadyForNextRole,
            DevelopmentOutcomeType.Regression when plan.FocusAreas.Contains(DevelopmentPlanFocus.Recovery) => DevelopmentRecommendationType.RecoveryPlan,
            DevelopmentOutcomeType.Regression => DevelopmentRecommendationType.NeedsConfidence,
            DevelopmentOutcomeType.Plateau when plan.IceTimeRole is DevelopmentIceTimeRole.HealthyScratch or DevelopmentIceTimeRole.FourthLine => DevelopmentRecommendationType.IncreaseIceTime,
            _ when plan.Morale is DevelopmentMorale.Poor or DevelopmentMorale.Terrible => DevelopmentRecommendationType.NeedsConfidence,
            _ when plan.IceTimeRole == DevelopmentIceTimeRole.JuniorReturn => DevelopmentRecommendationType.ReturnToJunior,
            _ => DevelopmentRecommendationType.StayCourse
        };
        var playerName = PersonName(scenario, plan.PersonId);
        var action = type switch
        {
            DevelopmentRecommendationType.IncreaseIceTime => "Consider a larger role or more special-teams reps.",
            DevelopmentRecommendationType.NeedsStrength => "Keep the player on a strength and conditioning focus.",
            DevelopmentRecommendationType.NeedsConfidence => "Give the player a confidence-support plan and clearer feedback.",
            DevelopmentRecommendationType.ReadyForNextRole => "Review promotion or a harder assignment before the next month.",
            DevelopmentRecommendationType.ReturnToJunior => "Keep junior/youth return available if sitting hurts development.",
            DevelopmentRecommendationType.AssignToAffiliate => "Consider affiliate minutes if the rulebook and organization allow it.",
            DevelopmentRecommendationType.RecoveryPlan => "Prioritize recovery and avoid overloading practice minutes.",
            _ => "Stay with the current plan and review next month."
        };
        var recommendation = new DevelopmentRecommendation(
            RecommendationId: $"development-recommendation:{plan.PersonId}:{scenario.CurrentDate:yyyyMMdd}:{type}",
            PersonId: plan.PersonId,
            PlayerName: playerName,
            RecommendationType: type,
            CreatedOn: scenario.CurrentDate,
            Reason: progress.Summary,
            RecommendedAction: action,
            IsActive: type != DevelopmentRecommendationType.StayCourse);
        recommendation.Validate();
        return recommendation;
    }

    private static IReadOnlyList<DevelopmentRecommendation> ReplaceRecommendation(
        IReadOnlyList<DevelopmentRecommendation> existing,
        DevelopmentRecommendation recommendation) =>
        existing
            .Where(item => item.RecommendationId != recommendation.RecommendationId)
            .Append(recommendation)
            .OrderByDescending(item => item.CreatedOn)
            .ToArray();

    private static AlphaInboxItem CreateInboxItem(
        NewGmScenarioSnapshot scenario,
        DevelopmentProgressSnapshot progress,
        DevelopmentRecommendation recommendation,
        string titlePrefix) =>
        new(
            InboxItemId: $"inbox:development-v2:{progress.PersonId}:{progress.Date:yyyyMMdd}:{Guid.NewGuid():N}",
            Date: new DateTimeOffset(progress.Date.Year, progress.Date.Month, progress.Date.Day, 11, 30, 0, TimeSpan.Zero),
            EventType: progress.Outcome == DevelopmentOutcomeType.Breakout
                ? LegacyEventType.PlayerBreakout
                : progress.Outcome == DevelopmentOutcomeType.Regression
                    ? LegacyEventType.PlayerRegression
                    : LegacyEventType.PlayerDevelopmentUpdated,
            Severity: recommendation.RecommendationType == DevelopmentRecommendationType.StayCourse ? LegacyEventSeverity.Notice : LegacyEventSeverity.Warning,
            Title: $"{titlePrefix}: {recommendation.PlayerName}",
            Summary: $"{progress.Summary} Coach: {progress.CoachComment} Recommended action: {recommendation.RecommendedAction}",
            PrimaryPersonId: progress.PersonId);

    private static LegacyEvent CreateDevelopmentEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        LegacyEventType type,
        string title,
        string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 11, 0, 0, TimeSpan.Zero),
            type,
            type is LegacyEventType.PlayerBreakout or LegacyEventType.PlayerRegression ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Internal,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: scenario.Organization.OrganizationId),
            new Dictionary<string, object?> { ["development_v2"] = true });
        return registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static DevelopmentOutcomeType DetermineOutcome(
        DevelopmentResult result,
        PlayerDevelopmentPlan plan,
        DevelopmentFactor factors)
    {
        if (result.IsBreakout || (result.CurrentAbilityChange >= 3 && plan.Confidence >= 70 && factors.IceTimeScore >= 65))
        {
            return DevelopmentOutcomeType.Breakout;
        }

        if (result.IsRegression || factors.InjuryPenalty.GetValueOrDefault() >= 40 || plan.Morale is DevelopmentMorale.Poor or DevelopmentMorale.Terrible)
        {
            return DevelopmentOutcomeType.Regression;
        }

        if (result.CurrentAbilityChange == 0 || factors.IceTimeScore <= 35)
        {
            return DevelopmentOutcomeType.Plateau;
        }

        return DevelopmentOutcomeType.Progress;
    }

    private static int ConfidenceDelta(PlayerDevelopmentPlan plan, DevelopmentResult result, DevelopmentFactor factors)
    {
        var delta = result.CurrentAbilityChange switch
        {
            >= 4 => 8,
            > 0 => 3,
            0 => -1,
            <= -3 => -8,
            _ => -3
        };
        if (factors.IceTimeScore <= 35)
        {
            delta -= 3;
        }

        if (plan.FocusAreas.Contains(DevelopmentPlanFocus.Confidence))
        {
            delta += 2;
        }

        if (factors.InjuryPenalty.GetValueOrDefault() >= 20)
        {
            delta -= 2;
        }

        return Math.Clamp(delta, -12, 12);
    }

    private static DevelopmentMorale UpdateMorale(DevelopmentMorale morale, DevelopmentIceTimeRole role, int confidenceChange, bool regression)
    {
        var score = (int)morale + (confidenceChange >= 4 ? 1 : confidenceChange <= -4 ? -1 : 0);
        if (role == DevelopmentIceTimeRole.HealthyScratch)
        {
            score--;
        }

        if (regression)
        {
            score--;
        }

        return (DevelopmentMorale)Math.Clamp(score, (int)DevelopmentMorale.Terrible, (int)DevelopmentMorale.Excellent);
    }

    private static int IceTimeScore(DevelopmentIceTimeRole role) =>
        role switch
        {
            DevelopmentIceTimeRole.HealthyScratch => 20,
            DevelopmentIceTimeRole.FourthLine => 40,
            DevelopmentIceTimeRole.Depth => 45,
            DevelopmentIceTimeRole.MiddleSix => 60,
            DevelopmentIceTimeRole.TopSix => 75,
            DevelopmentIceTimeRole.TopPair => 75,
            DevelopmentIceTimeRole.Starter => 78,
            DevelopmentIceTimeRole.Backup => 55,
            DevelopmentIceTimeRole.JuniorReturn => 65,
            DevelopmentIceTimeRole.AhlAssignment => 68,
            _ => 50
        };

    private static int CoachFitBonus(PlayerDevelopmentPlan plan, DevelopmentCoachProfile coach)
    {
        var fit = (coach.FitScore - 50) / 3;
        var specialtyBonus = plan.FocusAreas.Any(focus => coach.Specialties.Any(specialty => SpecialtyMatches(specialty, focus))) ? 10 : 0;
        return Math.Clamp(fit + specialtyBonus, 0, 35);
    }

    private static bool SpecialtyMatches(DevelopmentCoachSpecialty specialty, DevelopmentPlanFocus focus) =>
        (specialty, focus) switch
        {
            (DevelopmentCoachSpecialty.Skating, DevelopmentPlanFocus.Skating) => true,
            (DevelopmentCoachSpecialty.Shooting, DevelopmentPlanFocus.Shooting or DevelopmentPlanFocus.Offensive) => true,
            (DevelopmentCoachSpecialty.Defense, DevelopmentPlanFocus.Defensive) => true,
            (DevelopmentCoachSpecialty.Goalies, DevelopmentPlanFocus.Goaltending or DevelopmentPlanFocus.GoalieReflexes or DevelopmentPlanFocus.GoaliePositioning) => true,
            (DevelopmentCoachSpecialty.Confidence, DevelopmentPlanFocus.Confidence or DevelopmentPlanFocus.Character) => true,
            (DevelopmentCoachSpecialty.Leadership, DevelopmentPlanFocus.Leadership or DevelopmentPlanFocus.Character) => true,
            (DevelopmentCoachSpecialty.Conditioning, DevelopmentPlanFocus.Physical or DevelopmentPlanFocus.Strength or DevelopmentPlanFocus.Conditioning or DevelopmentPlanFocus.Recovery) => true,
            (DevelopmentCoachSpecialty.SpecialTeams, DevelopmentPlanFocus.Offensive or DevelopmentPlanFocus.Defensive or DevelopmentPlanFocus.HockeyIQ) => true,
            _ => false
        };

    private static int InjuryPenalty(NewGmScenarioSnapshot scenario, string personId) =>
        Math.Clamp(scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.PersonId == personId && injury.IsActive)
            .Select(injury => injury.DevelopmentPenalty)
            .DefaultIfEmpty(0)
            .Max(), 0, 100);

    private static bool ShouldCreateInbox(DevelopmentProgressSnapshot progress, DevelopmentRecommendation recommendation) =>
        progress.Outcome is DevelopmentOutcomeType.Breakout or DevelopmentOutcomeType.Regression
        || recommendation.RecommendationType is DevelopmentRecommendationType.IncreaseIceTime or DevelopmentRecommendationType.ReadyForNextRole or DevelopmentRecommendationType.NeedsConfidence or DevelopmentRecommendationType.RecoveryPlan;

    private static string BuildCoachComment(IReadOnlyList<DevelopmentPlanFocus> focusAreas, DevelopmentIceTimeRole role) =>
        $"Staff will focus on {string.Join(", ", focusAreas.Select(FocusText))} while monitoring whether {RoleText(role)} gives enough growth opportunity.";

    private static IEnumerable<string> PublicTraitThemes(PlayerDevelopmentProfile profile, bool descending)
    {
        var traits = descending
            ? profile.Traits.OrderByDescending(trait => trait.Value)
            : profile.Traits.OrderBy(trait => trait.Value);
        return traits.Select(trait => ThemeText(trait.Attribute));
    }

    private static string ScoutComment(NewGmScenarioSnapshot scenario, string personId)
    {
        var report = scenario.CompletedScoutingReports
            .Where(item => item.PlayerId == personId)
            .OrderByDescending(item => item.CreatedOn)
            .FirstOrDefault();
        if (report is not null)
        {
            return $"{PersonName(scenario, report.ScoutId)}: {report.Opinions.FirstOrDefault() ?? report.Recommendation.ToString()}";
        }

        var board = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == personId);
        return board is null
            ? "Scouting staff want more viewings before changing the projection."
            : $"Scouting staff: {board.ProjectionText}";
    }

    private static string FutureProjection(PlayerDevelopmentPlan plan, IReadOnlyList<string> strengths) =>
        plan.Confidence >= 70
            ? $"Positive path if {string.Join(", ", plan.FocusAreas.Select(FocusText))} continues; strongest visible themes are {string.Join(", ", strengths)}."
            : $"Projection remains cautious until confidence and role clarity improve around {FocusText(plan.FocusAreas[0])}.";

    private static string ThemeText(DevelopmentAttribute attribute) =>
        attribute switch
        {
            DevelopmentAttribute.HockeyIQ => "hockey IQ",
            DevelopmentAttribute.WorkEthic => "work ethic",
            _ => attribute.ToString().ToLowerInvariant()
        };

    private static string FocusText(DevelopmentPlanFocus focus) =>
        focus switch
        {
            DevelopmentPlanFocus.HockeyIQ => "hockey IQ",
            DevelopmentPlanFocus.PuckSkills => "puck skills",
            DevelopmentPlanFocus.GoalieReflexes => "goalie reflexes",
            DevelopmentPlanFocus.GoaliePositioning => "goalie positioning",
            _ => focus.ToString().ToLowerInvariant()
        };

    private static string RoleText(DevelopmentIceTimeRole role) =>
        role switch
        {
            DevelopmentIceTimeRole.HealthyScratch => "healthy scratch",
            DevelopmentIceTimeRole.FourthLine => "fourth-line minutes",
            DevelopmentIceTimeRole.MiddleSix => "middle-six minutes",
            DevelopmentIceTimeRole.TopSix => "top-six minutes",
            DevelopmentIceTimeRole.TopPair => "top-pair minutes",
            DevelopmentIceTimeRole.JuniorReturn => "junior return minutes",
            DevelopmentIceTimeRole.AhlAssignment => "AHL assignment minutes",
            _ => role.ToString().ToLowerInvariant()
        };

    private static string ConfidenceLabel(int confidence) =>
        confidence >= 80 ? "Excellent" :
        confidence >= 65 ? "Good" :
        confidence >= 45 ? "Average" :
        confidence >= 30 ? "Poor" :
        "Terrible";

    private static Person? FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId);

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        FindPerson(scenario, personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)?.PersonId
        ?? personId;
}
