using LegacyEngine.Events;
using LegacyEngine.HumanIntelligence;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Relationships;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class RecruitingV2Service
{
    public RecruitingV2Profile BuildProfile(NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var recruit = FindRecruit(scenario, recruitPersonId);
        var person = FindPerson(scenario, recruitPersonId);
        var board = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == recruitPersonId);
        var reports = scenario.CompletedScoutingReports
            .Where(report => report.PlayerId == recruitPersonId)
            .OrderByDescending(report => report.CreatedOn)
            .ToArray();
        var latestReport = reports.FirstOrDefault();
        var relationship = RelationshipWithGm(scenario, recruitPersonId);
        var familyPriorities = BuildFamilyPriorities(recruit);
        var competitors = BuildCompetitors(scenario, recruit).ToArray();
        var topCompetitor = competitors.OrderByDescending(team => team.InterestStrength).First();
        var decisionStyle = DecideStyle(recruit, person, familyPriorities);
        var currentOffers = recruit.Status == RecruitStatus.Offered
            ? new[] { scenario.Organization.Name }
            : Array.Empty<string>();
        var promises = recruit.Promises
            .Where(promise => promise.OrganizationId == scenario.Organization.OrganizationId)
            .OrderBy(promise => promise.Date)
            .Select(promise => $"{DisplayPromise(promise.PromiseType)} ({promise.Strength}/100)")
            .ToArray();

        var profile = new RecruitingV2Profile(
            RecruitPersonId: recruitPersonId,
            Name: person.Identity.DisplayName,
            Position: PositionFor(scenario, recruitPersonId),
            Age: person.CalculateAge(scenario.CurrentDate),
            RegionOrHometown: person.Identity.Birthplace,
            CurrentTeam: CurrentTeamFor(person, scenario),
            Status: recruit.Status,
            InterestLevel: recruit.GetInterest(scenario.Organization.OrganizationId),
            Priorities: recruit.Priorities,
            FamilyPriorities: familyPriorities,
            DecisionStyle: decisionStyle,
            RelationshipWithGm: relationship,
            ScoutingConfidence: latestReport?.Confidence ?? board?.ScoutingConfidence ?? ScoutingConfidenceLevel.Low,
            ProjectionSummary: latestReport?.Opinions.FirstOrDefault() ?? board?.ProjectionText ?? "Projection still forming; staff want more live viewings.",
            RiskSummary: RiskSummary(recruit, person, latestReport),
            CurrentOffers: currentOffers,
            CompetingTeams: competitors,
            WhyTheyAreInterested: WhyInterested(scenario, recruit, relationship),
            WhyTheyMayChooseUs: WhyChooseUs(scenario, recruit, relationship),
            WhyTheyMayRejectUs: WhyRejectUs(recruit, topCompetitor),
            PromisesMade: promises,
            GmNotes: scenario.PlayerDossierNotes.GetValueOrDefault(recruitPersonId, "No GM note yet."));
        profile.Validate();
        return profile;
    }

    public RecruitingV2Result CallRecruit(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        var recruit = FindRecruit(scenario, recruitPersonId);
        var updated = registry.RecruitingEngine.ChangeInterest(recruit, scenario.Organization.OrganizationId, 8, scenario.CurrentDate);
        var scenarioWithRecruit = ReplaceRecruit(scenario, updated);
        scenarioWithRecruit = ChangeGmRelationship(registry, scenarioWithRecruit, recruitPersonId, 4, "Direct recruit call built trust.");
        var profile = BuildProfile(scenarioWithRecruit, recruitPersonId);
        QueueEvent(registry, scenarioWithRecruit, LegacyEventType.RecruitCallResult, recruitPersonId, "Recruit call result", $"{profile.Name} responded well to the GM call.");
        return Result(
            true,
            scenarioWithRecruit,
            profile,
            LegacyEventType.RecruitCallResult,
            "Recruit call result",
            $"{profile.Name} appreciated the direct call. Interest is now {profile.InterestLevel}/100 and trust is {profile.RelationshipWithGm}/100.",
            $"Called {profile.Name}.");
    }

    public RecruitingV2Result CallFamily(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        var recruit = FindRecruit(scenario, recruitPersonId);
        var familyComfort = recruit.Priorities.GetValueOrDefault(RecruitPriority.FamilyComfort);
        var delta = familyComfort >= 70 ? 7 : 4;
        var updated = registry.RecruitingEngine.ChangeInterest(recruit, scenario.Organization.OrganizationId, delta, scenario.CurrentDate);
        var scenarioWithRecruit = ReplaceRecruit(scenario, updated);
        scenarioWithRecruit = ChangeGmRelationship(registry, scenarioWithRecruit, recruitPersonId, 3, "Family call improved comfort with the organization.");
        var profile = BuildProfile(scenarioWithRecruit, recruitPersonId);
        QueueEvent(registry, scenarioWithRecruit, LegacyEventType.RecruitFamilyCallResult, recruitPersonId, "Family call result", $"{profile.Name}'s family received more information.");
        return Result(
            true,
            scenarioWithRecruit,
            profile,
            LegacyEventType.RecruitFamilyCallResult,
            "Family call result",
            $"{profile.Name}'s family focused on {TopFamilyPriority(profile)}. The call improved comfort and current interest is {profile.InterestLevel}/100.",
            $"Called {profile.Name}'s family.");
    }

    public RecruitingV2Result InviteVisit(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        var recruit = FindRecruit(scenario, recruitPersonId);
        var relationship = RelationshipWithGm(scenario, recruitPersonId);
        var acceptanceScore = recruit.GetInterest(scenario.Organization.OrganizationId) + relationship + recruit.Priorities.GetValueOrDefault(RecruitPriority.FamilyComfort) / 2;
        var accepted = acceptanceScore >= 95;
        RecruitProfile updated;
        string inboxSummary;
        if (accepted)
        {
            var visit = new RecruitingVisit(
                VisitId: $"visit:{recruitPersonId}:{scenario.CurrentDate:yyyyMMdd}:{recruit.Visits.Count + 1}",
                OrganizationId: scenario.Organization.OrganizationId,
                Date: scenario.CurrentDate,
                FitScore: Math.Clamp(acceptanceScore / 2, 55, 95),
                Notes: "Visit accepted after the GM explained development, education, and family support.");
            updated = registry.RecruitingEngine.RecordVisit(recruit, visit);
            inboxSummary = "Visit accepted. Staff can use the visit to reinforce fit and family comfort.";
        }
        else
        {
            updated = registry.RecruitingEngine.ChangeInterest(recruit, scenario.Organization.OrganizationId, -2, scenario.CurrentDate);
            inboxSummary = "Visit declined for now. The recruit wants more clarity before traveling.";
        }

        var updatedScenario = ReplaceRecruit(scenario, updated);
        var profile = BuildProfile(updatedScenario, recruitPersonId);
        QueueEvent(registry, updatedScenario, LegacyEventType.RecruitVisitUpdated, recruitPersonId, accepted ? "Recruit visit accepted" : "Recruit visit declined", inboxSummary);
        return Result(
            true,
            updatedScenario,
            profile,
            LegacyEventType.RecruitVisitUpdated,
            accepted ? "Visit accepted" : "Visit declined",
            $"{profile.Name}: {inboxSummary}",
            $"{profile.Name} {(accepted ? "accepted" : "declined")} the visit invitation.");
    }

    public RecruitingV2Result MakePromise(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string recruitPersonId,
        RecruitingPromiseType promiseType)
    {
        var recruit = FindRecruit(scenario, recruitPersonId);
        var promise = new RecruitingPromise(
            PromiseId: $"promise:{recruitPersonId}:{scenario.CurrentDate:yyyyMMdd}:{recruit.Promises.Count + 1}",
            OrganizationId: scenario.Organization.OrganizationId,
            PromiseType: promiseType,
            Strength: PromiseStrength(recruit, promiseType),
            Date: scenario.CurrentDate,
            Description: $"{DisplayPromise(promiseType)} promised during recruiting.",
            TargetDate: scenario.CurrentDate.AddMonths(4));
        var updated = registry.RecruitingEngine.AddPromise(recruit, promise);
        var updatedScenario = ReplaceRecruit(scenario, updated);
        updatedScenario = ChangeGmRelationship(registry, updatedScenario, recruitPersonId, 5, "Recruiting promise increased trust but must be honored later.");
        var profile = BuildProfile(updatedScenario, recruitPersonId);
        QueueEvent(registry, updatedScenario, LegacyEventType.RecruitPromiseMade, recruitPersonId, "Recruiting promise made", $"{DisplayPromise(promiseType)} was promised to {profile.Name}.");
        return Result(
            true,
            updatedScenario,
            profile,
            LegacyEventType.RecruitPromiseMade,
            "Recruiting promise made",
            $"{profile.Name} was promised {DisplayPromise(promiseType)}. This helped interest and trust, but it is now recorded for future accountability.",
            $"Promise recorded for {profile.Name}.");
    }

    public RecruitingV2Result OfferEducationPackage(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        var supportsEducation = scenario.Organization.OrganizationId.Length > 0
            && (registry.Rulebook?.ContractRules?.EducationPackagesEnabled ?? false);
        if (!supportsEducation)
        {
            var profile = BuildProfile(scenario, recruitPersonId);
            return Result(false, scenario, profile, LegacyEventType.RecruitPromiseMade, "Education package unavailable", $"{profile.Name} cannot be offered an education package under the active rulebook.", "Education package unavailable.");
        }

        return MakePromise(registry, scenario, recruitPersonId, RecruitingPromiseType.EducationPackage);
    }

    public RecruitingV2Result WithdrawOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        var recruit = FindRecruit(scenario, recruitPersonId);
        var updated = registry.RecruitingEngine.ChangeInterest(recruit, scenario.Organization.OrganizationId, -18, scenario.CurrentDate).SetStatus(RecruitStatus.Withdrawn);
        var updatedScenario = ReplaceRecruit(scenario, updated);
        var profile = BuildProfile(updatedScenario, recruitPersonId);
        QueueEvent(registry, updatedScenario, LegacyEventType.RecruitOfferWithdrawn, recruitPersonId, "Recruiting offer withdrawn", $"{profile.Name}'s offer was withdrawn.");
        return Result(
            true,
            updatedScenario,
            profile,
            LegacyEventType.RecruitOfferWithdrawn,
            "Recruiting offer withdrawn",
            $"{profile.Name}'s offer was withdrawn. No contract or roster action was taken.",
            $"Offer withdrawn for {profile.Name}.");
    }

    public RecruitingV2Result AskScoutForMoreInformation(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        var profile = BuildProfile(scenario, recruitPersonId);
        QueueEvent(registry, scenario, LegacyEventType.ScoutRecruitingNote, recruitPersonId, "Scout recruiting note requested", $"Staff were asked for more information on {profile.Name}.");
        return Result(
            true,
            scenario,
            profile,
            LegacyEventType.ScoutRecruitingNote,
            $"Scout recruiting note: {profile.Name}",
            $"{profile.Name}: scout note requested. Current projection is '{profile.ProjectionSummary}' with confidence {profile.ScoutingConfidence}.",
            $"Scout asked for more information on {profile.Name}.");
    }

    public RecruitingV2Result AddGmNote(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId, string note)
    {
        var notes = scenario.PlayerDossierNotes
            .Where(item => !string.Equals(item.Key, recruitPersonId, StringComparison.Ordinal))
            .Append(new KeyValuePair<string, string>(recruitPersonId, note))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var updated = scenario with { PlayerDossierNotes = notes };
        updated.Validate();
        var profile = BuildProfile(updated, recruitPersonId);
        return Result(
            true,
            updated,
            profile,
            LegacyEventType.Generic,
            "Recruit GM note updated",
            $"{profile.Name}: GM note updated.",
            $"GM note updated for {profile.Name}.");
    }

    public RecruitingV2Result MakeDecision(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId)
    {
        var recruit = FindRecruit(scenario, recruitPersonId);
        var profile = BuildProfile(scenario, recruitPersonId);
        var humanResult = EvaluateRecruitDecision(profile, recruit, scenario);
        var selectedUs = humanResult.SelectedOption.OptionId == "our-organization";
        var committed = selectedUs && humanResult.RankedOptions[0].Score >= 58;
        var status = committed ? RecruitStatus.Committed : RecruitStatus.Rejected;
        var updatedRecruit = recruit.SetStatus(status);
        var updatedScenario = ReplaceRecruit(scenario, updatedRecruit);
        var explanation = DecisionExplanation(profile, humanResult, committed);
        var eventType = committed ? LegacyEventType.RecruitCommitted : LegacyEventType.RecruitRejected;
        QueueEvent(registry, updatedScenario, eventType, recruitPersonId, committed ? "Recruit committed" : "Recruit rejected", explanation);
        var updatedProfile = BuildProfile(updatedScenario, recruitPersonId);

        return Result(
            true,
            updatedScenario,
            updatedProfile,
            eventType,
            committed ? $"Recruit committed: {profile.Name}" : $"Recruit rejected: {profile.Name}",
            explanation,
            committed ? $"{profile.Name} committed." : $"{profile.Name} rejected the offer.",
            explanation);
    }

    private static HumanDecisionResult EvaluateRecruitDecision(RecruitingV2Profile profile, RecruitProfile recruit, NewGmScenarioSnapshot scenario)
    {
        var person = FindPerson(scenario, profile.RecruitPersonId);
        var topCompetitor = profile.TopCompetitor!;
        var promiseScore = recruit.Promises
            .Where(promise => promise.OrganizationId == scenario.Organization.OrganizationId)
            .Select(promise => promise.Strength)
            .DefaultIfEmpty(35)
            .Average();
        var context = new HumanDecisionContext(
            ContextId: $"recruit-decision:{profile.RecruitPersonId}:{scenario.CurrentDate:yyyyMMdd}",
            ActorPersonId: profile.RecruitPersonId,
            DecisionDate: scenario.CurrentDate,
            Urgency: 45,
            Pressure: 50,
            Risk: profile.RiskSummary.Contains("risk", StringComparison.OrdinalIgnoreCase) ? 62 : 40,
            Reward: Math.Clamp((int)((profile.InterestLevel + promiseScore) / 2), 0, 100),
            Uncertainty: profile.ScoutingConfidence is ScoutingConfidenceLevel.Low or ScoutingConfidenceLevel.Unknown ? 68 : 35,
            OrganizationFit: Math.Clamp((profile.InterestLevel + profile.RelationshipWithGm + (int)promiseScore) / 3, 0, 100),
            Trust: profile.RelationshipWithGm,
            Respect: profile.RelationshipWithGm,
            Confidence: profile.InterestLevel,
            Loyalty: profile.FamilyPriorities.GetValueOrDefault(RecruitingFamilyPriority.Stability));
        var intelligence = new HumanIntelligenceProfile(
            profile.RecruitPersonId,
            person.Personality.Ambition,
            person.Personality.Loyalty,
            person.Personality.Adaptability,
            person.Personality.Temperament,
            person.Personality.Professionalism,
            Math.Clamp((person.Personality.Temperament + person.Personality.Professionalism) / 2, 0, 100));

        var ourOption = new HumanDecisionOption(
            "our-organization",
            scenario.Organization.Name,
            "Commit to the player's current recruiting offer.",
            new[]
            {
                Factor(HumanDecisionFactorType.OrganizationFit, context.OrganizationFit, 3.5m, "Fit with the organization, promised role, and development plan."),
                Factor(HumanDecisionFactorType.RelationshipTrust, profile.RelationshipWithGm, 3m, "Trust in the GM and staff."),
                Factor(HumanDecisionFactorType.Reward, (int)promiseScore, 2m, "Recruiting promises and opportunity."),
                Factor(HumanDecisionFactorType.Uncertainty, 100 - context.Uncertainty, 1m, "Confidence that the situation is clear.")
            });
        var competitorOption = new HumanDecisionOption(
            "top-competitor",
            topCompetitor.TeamName,
            "Choose the strongest competing organization.",
            new[]
            {
                Factor(HumanDecisionFactorType.OrganizationFit, topCompetitor.InterestStrength, 3.5m, topCompetitor.WhyRecruitMayChooseThem),
                Factor(HumanDecisionFactorType.Reward, topCompetitor.HasOffer ? 72 : 55, 2m, "Competing offer/opportunity."),
                Factor(HumanDecisionFactorType.Risk, 100 - topCompetitor.Weaknesses.Count * 10, 1m, "Risk profile of the competitor.")
            });

        return new HumanIntelligenceEngine().Evaluate(context, intelligence, new[] { ourOption, competitorOption });
    }

    private static HumanDecisionFactor Factor(HumanDecisionFactorType type, int score, decimal weight, string description) =>
        new(type, Math.Clamp(score, 0, 100), new HumanDecisionWeight(type, weight), description);

    private static string DecisionExplanation(RecruitingV2Profile profile, HumanDecisionResult decision, bool committed)
    {
        var firstReason = decision.Reasons.OrderByDescending(reason => reason.Contribution).FirstOrDefault()?.Text ?? "the decision factors were balanced.";
        return committed
            ? $"{profile.Name} committed because he values {TopPriority(profile)} and believes your organization offers the clearest fit. {firstReason}"
            : $"{profile.Name} rejected the offer because {profile.TopCompetitor?.TeamName ?? "another team"} presented a stronger fit or reduced a concern. {firstReason}";
    }

    private static NewGmScenarioSnapshot ReplaceRecruit(NewGmScenarioSnapshot scenario, RecruitProfile recruit)
    {
        var recruits = scenario.AlphaSnapshot.Recruits
            .Select(item => item.RecruitPersonId == recruit.RecruitPersonId ? recruit : item)
            .ToArray();
        var alpha = scenario.AlphaSnapshot with { Recruits = recruits };
        var updated = scenario with { AlphaSnapshot = alpha };
        updated.Validate();
        return updated;
    }

    private static NewGmScenarioSnapshot ChangeGmRelationship(EngineRegistry registry, NewGmScenarioSnapshot scenario, string recruitPersonId, int trustDelta, string reason)
    {
        var gmId = scenario.AlphaSnapshot.GeneralManager.PersonId;
        var existing = scenario.AlphaSnapshot.Relationships.FirstOrDefault(relationship => relationship.FromPersonId == gmId && relationship.ToPersonId == recruitPersonId);
        var relationship = existing ?? registry.RelationshipEngine.CreateRelationship(
            $"relationship:gm-recruit:{gmId}:{recruitPersonId}",
            gmId,
            recruitPersonId,
            RelationshipType.Professional,
            scenario.CurrentDate);
        relationship = registry.RelationshipEngine.ChangeRelationship(
            relationship,
            new RelationshipChange(RelationshipDimension.Trust, trustDelta, reason, scenario.CurrentDate));
        relationship = registry.RelationshipEngine.ChangeRelationship(
            relationship,
            new RelationshipChange(RelationshipDimension.Confidence, Math.Max(1, trustDelta / 2), reason, scenario.CurrentDate));
        var relationships = scenario.AlphaSnapshot.Relationships
            .Where(item => !string.Equals(item.RelationshipId, relationship.RelationshipId, StringComparison.Ordinal))
            .Append(relationship)
            .ToArray();
        var alpha = scenario.AlphaSnapshot with { Relationships = relationships };
        var updated = scenario with { AlphaSnapshot = alpha };
        updated.Validate();
        return updated;
    }

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, LegacyEventType eventType, string recruitPersonId, string title, string description)
    {
        var date = scenario.CurrentDate;
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 14, 0, 0, TimeSpan.Zero),
            eventType,
            eventType == LegacyEventType.RecruitRejected ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(recruitPersonId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["system"] = "recruiting_v2" });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static RecruitingV2Result Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        RecruitingV2Profile profile,
        LegacyEventType eventType,
        string inboxTitle,
        string inboxSummary,
        string message,
        string decisionExplanation = "")
    {
        var inbox = new[]
        {
            new AlphaInboxItem(
                InboxItemId: $"inbox:recruiting-v2:{Guid.NewGuid():N}",
                Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 14, 15, 0, TimeSpan.Zero),
                EventType: eventType,
                Severity: eventType == LegacyEventType.RecruitRejected ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
                Title: inboxTitle,
                Summary: inboxSummary,
                PrimaryPersonId: profile.RecruitPersonId)
        };
        var result = new RecruitingV2Result(success, scenario, profile, inbox, message, decisionExplanation);
        result.Validate();
        return result;
    }

    private static RecruitProfile FindRecruit(NewGmScenarioSnapshot scenario, string recruitPersonId) =>
        scenario.AlphaSnapshot.Recruits.SingleOrDefault(recruit => recruit.RecruitPersonId == recruitPersonId)
        ?? throw new ArgumentException("Recruit was not found.", nameof(recruitPersonId));

    private static Person FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)
        ?? throw new ArgumentException("Person was not found.", nameof(personId));

    private static int RelationshipWithGm(NewGmScenarioSnapshot scenario, string recruitPersonId) =>
        scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == scenario.AlphaSnapshot.GeneralManager.PersonId && relationship.ToPersonId == recruitPersonId)
            .Select(relationship => (relationship.Trust + relationship.Confidence + relationship.Respect) / 3)
            .DefaultIfEmpty(50)
            .First();

    private static IReadOnlyDictionary<RecruitingFamilyPriority, int> BuildFamilyPriorities(RecruitProfile recruit) =>
        new Dictionary<RecruitingFamilyPriority, int>
        {
            [RecruitingFamilyPriority.Education] = recruit.Priorities.GetValueOrDefault(RecruitPriority.Education, 50),
            [RecruitingFamilyPriority.Safety] = Math.Clamp((recruit.Priorities.GetValueOrDefault(RecruitPriority.FamilyComfort, 50) + recruit.Priorities.GetValueOrDefault(RecruitPriority.TeamCulture, 55)) / 2, 0, 100),
            [RecruitingFamilyPriority.BilletQuality] = recruit.Priorities.GetValueOrDefault(RecruitPriority.FamilyComfort, 50),
            [RecruitingFamilyPriority.DistanceFromHome] = recruit.Priorities.GetValueOrDefault(RecruitPriority.DistanceFromHome, 50),
            [RecruitingFamilyPriority.Stability] = Math.Clamp((recruit.Priorities.GetValueOrDefault(RecruitPriority.Education, 50) + recruit.Priorities.GetValueOrDefault(RecruitPriority.Winning, 50)) / 2, 0, 100),
            [RecruitingFamilyPriority.TrustInOrganization] = recruit.Priorities.GetValueOrDefault(RecruitPriority.TrustInGm, 50)
        };

    private static IReadOnlyList<RecruitingCompetingTeam> BuildCompetitors(NewGmScenarioSnapshot scenario, RecruitProfile recruit)
    {
        var index = Math.Abs(recruit.RecruitPersonId.GetHashCode());
        var hometown = FindPerson(scenario, recruit.RecruitPersonId).Identity.Birthplace.Split(',')[0].Trim();
        var closeToHome = recruit.Priorities.GetValueOrDefault(RecruitPriority.DistanceFromHome) >= 60;
        return new[]
        {
            new RecruitingCompetingTeam(
                recruit.RecruitPersonId,
                closeToHome ? $"{hometown} Royals" : "Northern Blades",
                Math.Clamp(54 + (index % 18) + (closeToHome ? 10 : 0), 0, 100),
                true,
                new[] { closeToHome ? "Close to home" : "Strong recent development record", "Clear playing opportunity" },
                new[] { "Less education support", "Less direct GM relationship" },
                closeToHome ? "They are close to home and reduce family travel concerns." : "They can sell immediate ice time and a familiar role."),
            new RecruitingCompetingTeam(
                recruit.RecruitPersonId,
                "Capital Valley Wolves",
                Math.Clamp(48 + (index % 14), 0, 100),
                index % 2 == 0,
                new[] { "Winning reputation", "High-end facilities" },
                new[] { "Crowded depth chart", "Less individualized development" },
                "They can offer winning pressure and facilities, but ice time may be harder to earn.")
        };
    }

    private static RecruitingDecisionStyle DecideStyle(
        RecruitProfile recruit,
        Person person,
        IReadOnlyDictionary<RecruitingFamilyPriority, int> familyPriorities)
    {
        if (familyPriorities[RecruitingFamilyPriority.TrustInOrganization] >= 70)
        {
            return RecruitingDecisionStyle.TrustBased;
        }

        if (familyPriorities[RecruitingFamilyPriority.DistanceFromHome] >= 70)
        {
            return RecruitingDecisionStyle.FamilyLed;
        }

        if (recruit.Priorities.GetValueOrDefault(RecruitPriority.IceTime) >= 80 || recruit.Priorities.GetValueOrDefault(RecruitPriority.PlayingRole) >= 75)
        {
            return RecruitingDecisionStyle.OpportunityDriven;
        }

        return person.Personality.Ambition >= 70 ? RecruitingDecisionStyle.Competitive : RecruitingDecisionStyle.DevelopmentFocused;
    }

    private static string PositionFor(NewGmScenarioSnapshot scenario, string personId)
    {
        var rosterPlayer = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        if (rosterPlayer is not null)
        {
            return rosterPlayer.Position.ToString();
        }

        var rank = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Rank ?? 1;
        return rank switch
        {
            3 or 8 => "Goalie",
            2 or 5 or 9 => "Defense",
            _ when rank % 3 == 0 => "Center",
            _ when rank % 3 == 1 => "LeftWing",
            _ => "RightWing"
        };
    }

    private static string CurrentTeamFor(Person person, NewGmScenarioSnapshot scenario)
    {
        var city = person.Identity.Birthplace.Split(',')[0].Trim();
        return string.IsNullOrWhiteSpace(city) ? $"{scenario.Organization.Name} watch list" : $"{city} U18";
    }

    private static string RiskSummary(RecruitProfile recruit, Person person, ScoutingReport? report)
    {
        var risk = recruit.Priorities.GetValueOrDefault(RecruitPriority.DistanceFromHome) >= 70
            ? "distance-from-home risk"
            : recruit.Priorities.GetValueOrDefault(RecruitPriority.IceTime) >= 85
                ? "ice-time expectation risk"
                : "manageable recruiting risk";
        return report is null
            ? $"{risk}; scouting confidence still needs another report."
            : $"{risk}; latest report confidence is {report.Confidence}.";
    }

    private static string WhyInterested(NewGmScenarioSnapshot scenario, RecruitProfile recruit, int relationship) =>
        $"Interest is built around {TopPriority(recruit)} and a GM relationship score of {relationship}/100 with {scenario.Organization.Name}.";

    private static string WhyChooseUs(NewGmScenarioSnapshot scenario, RecruitProfile recruit, int relationship) =>
        $"{scenario.Organization.Name} can sell {TopPriority(recruit)}, development attention, and a clearer trust path with the GM.";

    private static string WhyRejectUs(RecruitProfile recruit, RecruitingCompetingTeam topCompetitor) =>
        $"The recruit may reject us if {topCompetitor.TeamName} wins the {TopPriority(recruit)} argument or family comfort remains unresolved.";

    private static int PromiseStrength(RecruitProfile recruit, RecruitingPromiseType promiseType) =>
        promiseType switch
        {
            RecruitingPromiseType.TopSixRole or RecruitingPromiseType.ImmediateRosterSpot => recruit.Priorities.GetValueOrDefault(RecruitPriority.PlayingRole, recruit.Priorities.GetValueOrDefault(RecruitPriority.IceTime, 75)),
            RecruitingPromiseType.EducationPackage or RecruitingPromiseType.EducationSupport => recruit.Priorities.GetValueOrDefault(RecruitPriority.Education, 75),
            RecruitingPromiseType.DevelopmentFocus or RecruitingPromiseType.DevelopmentPlan => recruit.Priorities.GetValueOrDefault(RecruitPriority.Development, 75),
            RecruitingPromiseType.HigherLeaguePathway or RecruitingPromiseType.PathwaySupport => recruit.Priorities.GetValueOrDefault(RecruitPriority.PathwayToHigherHockey, 75),
            RecruitingPromiseType.CampInvite => 70,
            _ => 78
        };

    private static string TopPriority(RecruitingV2Profile profile) => DisplayRecruitPriority(profile.Priorities.OrderByDescending(item => item.Value).First().Key);

    private static string TopPriority(RecruitProfile recruit) => DisplayRecruitPriority(recruit.Priorities.OrderByDescending(item => item.Value).First().Key);

    private static string TopFamilyPriority(RecruitingV2Profile profile) =>
        profile.FamilyPriorities.OrderByDescending(item => item.Value).First().Key.ToString();

    private static string DisplayRecruitPriority(RecruitPriority priority) =>
        priority switch
        {
            RecruitPriority.IceTime => "ice time",
            RecruitPriority.DistanceFromHome => "distance from home",
            RecruitPriority.PathwayToHigherHockey => "pathway to higher hockey",
            RecruitPriority.FamilyComfort => "family comfort",
            RecruitPriority.TeamCulture => "team culture",
            RecruitPriority.TrustInGm => "trust in the GM",
            RecruitPriority.PlayingRole => "playing role",
            _ => priority.ToString().ToLowerInvariant()
        };

    private static string DisplayPromise(RecruitingPromiseType promise) =>
        promise switch
        {
            RecruitingPromiseType.TopSixRole => "top-six role",
            RecruitingPromiseType.PowerPlayOpportunity => "power play opportunity",
            RecruitingPromiseType.PenaltyKillOpportunity => "penalty kill opportunity",
            RecruitingPromiseType.DevelopmentFocus or RecruitingPromiseType.DevelopmentPlan => "development focus",
            RecruitingPromiseType.EducationPackage or RecruitingPromiseType.EducationSupport => "education package",
            RecruitingPromiseType.LeadershipOpportunity => "leadership opportunity",
            RecruitingPromiseType.HigherLeaguePathway or RecruitingPromiseType.PathwaySupport => "higher league pathway",
            RecruitingPromiseType.ImmediateRosterSpot => "immediate roster spot",
            RecruitingPromiseType.CampInvite => "camp invite",
            _ => promise.ToString()
        };
}
