using LegacyEngine.Development;
using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class TrainingCampService
{
    public TrainingCampCalendarInfo GetCalendarInfo(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);

        var rulebook = ResolveRulebook(registry);
        var calendar = CalendarForCurrentCamp(scenario);
        var opens = MilestoneDate(calendar, SeasonMilestoneType.TrainingCampOpens);
        var closes = MilestoneDate(calendar, SeasonMilestoneType.SeasonBegins);
        var validation = registry.RosterEngine.ValidateRoster(
            scenario.AlphaSnapshot.Roster,
            rulebook.RosterRules is null ? null : new RosterRuleValidator(rulebook));
        var required = rulebook.RosterRules?.ActiveRoster ?? 0;
        var currentCount = scenario.TrainingCamp?.Players.Count(player => player.Status is not TrainingCampStatus.Cut
            and not TrainingCampStatus.Released
            and not TrainingCampStatus.ReturnedToJuniorTeam
            and not TrainingCampStatus.AssignedToAffiliate
            and not TrainingCampStatus.ReturnedToParent) ?? scenario.AlphaSnapshot.Roster.CurrentPlayers.Count;

        return new TrainingCampCalendarInfo(
            OpensOn: opens,
            ClosesOn: closes,
            DaysUntilRosterDeadline: closes.DayNumber - scenario.CurrentDate.DayNumber,
            CurrentCampRosterCount: currentCount,
            RequiredOpeningRosterSize: required,
            PlayersOverLimit: Math.Max(0, scenario.AlphaSnapshot.Roster.ActivePlayers.Count - required),
            RosterValidationResult: validation);
    }

    public TrainingCampCalendarResult AdvanceCalendar(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var info = GetCalendarInfo(registry, scenario);
        var inbox = new List<AlphaInboxItem>();
        var current = scenario;
        var summaries = new List<string>();

        if (current.TrainingCamp is null && current.CurrentDate >= info.OpensOn && current.CurrentDate < info.ClosesOn)
        {
            var opened = OpenCampForCalendar(registry, current);
            current = opened.ScenarioSnapshot;
            inbox.AddRange(opened.InboxItems);
            summaries.Add(opened.Summary);
        }

        if (current.TrainingCamp is { IsCompleted: false } camp && current.CurrentDate >= info.OpensOn && current.CurrentDate < info.ClosesOn)
        {
            var shouldRefresh = camp.Evaluations.Count == 0
                || camp.Evaluations.Max(evaluation => evaluation.CreatedOn).AddDays(7) <= current.CurrentDate;
            if (shouldRefresh)
            {
                var evaluated = RefreshEvaluations(registry, current, inboxLimit: 2);
                current = evaluated.ScenarioSnapshot;
                inbox.AddRange(evaluated.InboxItems);
                summaries.Add(evaluated.Summary);
            }
        }

        var latestInfo = GetCalendarInfo(registry, current);
        if (current.TrainingCamp is { IsCompleted: false } && !latestInfo.IsRosterCompliant)
        {
            var urgent = CreateRosterDeadlinePendingActions(registry, current, latestInfo);
            current = urgent.ScenarioSnapshot;
            inbox.AddRange(urgent.InboxItems);
            summaries.Add(urgent.Summary);
        }

        if (current.TrainingCamp is { IsCompleted: false } && current.CurrentDate >= latestInfo.ClosesOn)
        {
            var completed = CompleteCamp(registry, current);
            current = completed.ScenarioSnapshot;
            inbox.AddRange(completed.InboxItems);
            summaries.Add(completed.Summary);
        }

        return new TrainingCampCalendarResult(
            current,
            inbox,
            summaries.Count == 0 ? "Training camp calendar check made no changes." : string.Join(" ", summaries));
    }

    public bool CanOpenCamp(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);

        var rulebook = ResolveRulebook(registry);
        if (DraftUiPolicy.IsDraftUiEnabled(rulebook))
        {
            return scenario.DraftExperience?.Status == DraftExperienceStatus.Completed;
        }

        return scenario.CurrentDate >= scenario.DraftDate
            || scenario.DraftExperience?.Status == DraftExperienceStatus.Disabled;
    }

    public TrainingCampResult OpenCamp(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (scenario.TrainingCamp is { } existing)
        {
            return BuildResult(scenario, existing, Array.Empty<AlphaInboxItem>(), "Training camp is already open.");
        }

        if (!CanOpenCamp(registry, scenario))
        {
            throw new InvalidOperationException("Training camp cannot open until draft/offseason setup is complete.");
        }

        return OpenCampCore(registry, scenario, "Training camp opened");
    }

    private TrainingCampResult OpenCampForCalendar(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        if (scenario.TrainingCamp is { } existing)
        {
            return BuildResult(scenario, existing, Array.Empty<AlphaInboxItem>(), "Training camp is already open.");
        }

        return OpenCampCore(registry, scenario, "Training camp opened automatically from the season calendar");
    }

    private TrainingCampResult OpenCampCore(EngineRegistry registry, NewGmScenarioSnapshot scenario, string eventTitle)
    {
        var date = scenario.CurrentDate;
        var players = new List<TrainingCampPlayer>();
        foreach (var rosterPlayer in scenario.AlphaSnapshot.Roster.CurrentPlayers)
        {
            players.Add(CreatePlayer(
                scenario,
                rosterPlayer.PersonId,
                rosterPlayer.Position,
                TrainingCampInviteType.ReturningRosterPlayer,
                date,
                rosterPlayer.AcquisitionSource));
        }

        if (scenario.DraftExperience is { Status: DraftExperienceStatus.Completed })
        {
            foreach (var prospect in scenario.ProspectRights.Where(prospect => prospect.Status == ProspectStatus.InvitedToCamp))
            {
                AddIfMissing(players, CreatePlayer(
                    scenario,
                    prospect.ProspectPersonId,
                    prospect.Position,
                    TrainingCampInviteType.DraftedProspect,
                    date,
                    PlayerAcquisitionSource.Unknown));
            }
        }

        foreach (var recruit in scenario.AlphaSnapshot.Recruits.Take(4))
        {
            AddIfMissing(players, CreatePlayer(
                scenario,
                recruit.RecruitPersonId,
                GuessPosition(scenario, recruit.RecruitPersonId),
                TrainingCampInviteType.Recruit,
                date,
                PlayerAcquisitionSource.FreeAgentSigning));
        }

        var camp = new TrainingCamp(
            CampId: $"camp:{scenario.Organization.OrganizationId}:{date:yyyyMMdd}",
            OrganizationId: scenario.Organization.OrganizationId,
            OpenedOn: date,
            Players: players
                .OrderBy(player => player.PlayerName, StringComparer.Ordinal)
                .ToArray(),
            Evaluations: Array.Empty<TrainingCampEvaluation>());
        camp.Validate();

        QueueCampEvent(
            registry,
            scenario,
            LegacyEventType.TrainingCampOpened,
            LegacyEventSeverity.Notice,
            eventTitle,
            $"Training camp opened for {scenario.Organization.Name}.",
            date);

        var updatedScenario = scenario with { TrainingCamp = camp };
        var inbox = new[]
        {
            CreateInboxItem(
                date,
                LegacyEventType.TrainingCampOpened,
                LegacyEventSeverity.Notice,
                "Training Camp Opened",
                $"{scenario.Organization.Name} opened camp with {camp.Players.Count} player(s) invited.",
                null)
        };

        return BuildResult(updatedScenario, camp, inbox, $"Training camp opened with {camp.Players.Count} invited player(s).");
    }

    public TrainingCampResult InvitePlayer(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        RosterPosition position,
        TrainingCampInviteType inviteType,
        PlayerAcquisitionSource acquisitionSource = PlayerAcquisitionSource.Unknown,
        string? sourceOrganizationId = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var camp = RequireCamp(scenario);

        var player = CreatePlayer(
            scenario,
            personId,
            position,
            inviteType,
            scenario.CurrentDate,
            acquisitionSource,
            sourceOrganizationId);

        if (camp.FindPlayer(personId) is not null)
        {
            return BuildResult(scenario, camp, Array.Empty<AlphaInboxItem>(), $"{player.PlayerName} is already in camp.");
        }

        var updatedCamp = camp with { Players = camp.Players.Append(player).ToArray() };
        updatedCamp.Validate();
        var updatedScenario = scenario with { TrainingCamp = updatedCamp };

        return BuildResult(updatedScenario, updatedCamp, Array.Empty<AlphaInboxItem>(), $"{player.PlayerName} was invited to camp.");
    }

    public TrainingCampResult EvaluateCamp(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        return RefreshEvaluations(registry, scenario, inboxLimit: 3);
    }

    public TrainingCampResult RefreshEvaluations(EngineRegistry registry, NewGmScenarioSnapshot scenario, int inboxLimit = 2)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var camp = RequireCamp(scenario);

        var date = scenario.CurrentDate;
        var evaluations = camp.Players
            .Where(player => player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp or TrainingCampStatus.Kept)
            .Select(player => CreateEvaluation(scenario, player, date))
            .ToArray();

        var updatedCamp = camp with
        {
            Players = camp.Players
                .Select(player => player.Status == TrainingCampStatus.Invited
                    ? player with { Status = TrainingCampStatus.InCamp }
                    : player)
                .ToArray(),
            Evaluations = camp.Evaluations
                .Where(existing => evaluations.All(next => next.PersonId != existing.PersonId))
                .Concat(evaluations)
                .ToArray()
        };
        updatedCamp.Validate();

        foreach (var evaluation in evaluations)
        {
            QueueCampEvent(
                registry,
                scenario,
                LegacyEventType.TrainingCampEvaluationCreated,
                LegacyEventSeverity.Notice,
                $"Camp evaluation: {evaluation.PlayerName}",
                $"{evaluation.PlayerName}: {evaluation.Recommendation}. {evaluation.CoachNote}",
                date,
                evaluation.PersonId);
        }

        var updatedScenario = scenario with { TrainingCamp = updatedCamp };
        var inbox = evaluations
            .OrderByDescending(evaluation => evaluation.CampScore)
            .Take(inboxLimit)
            .Select(evaluation => CreateInboxItem(
                date,
                LegacyEventType.TrainingCampEvaluationCreated,
                LegacyEventSeverity.Notice,
                $"Camp Evaluation: {evaluation.PlayerName}",
                $"{evaluation.PlayerName} scored {evaluation.CampScore}/100. {evaluation.Recommendation} {evaluation.CoachNote}",
                evaluation.PersonId))
            .ToArray();

        return BuildResult(updatedScenario, updatedCamp, inbox, $"Refreshed {evaluations.Length} camp evaluation(s).");
    }

    public TrainingCampDecisionResult ApplyDecision(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        TrainingCampDecision decision)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        decision.Validate();

        var camp = RequireCamp(scenario);
        var player = camp.FindPlayer(decision.PersonId)
            ?? throw new ArgumentException("Player is not in training camp.", nameof(decision));

        var rulebook = ResolveRulebook(registry);
        var validationMessage = ValidateDecision(scenario, rulebook, player, decision.DecisionType);
        if (validationMessage is not null)
        {
            return new TrainingCampDecisionResult(
                Success: false,
                ScenarioSnapshot: scenario,
                Camp: camp,
                Decision: decision,
                InboxItems: Array.Empty<AlphaInboxItem>(),
                Message: validationMessage);
        }

        var status = ToStatus(decision.DecisionType);
        var updatedPlayer = player with { Status = status };
        var updatedCamp = camp with
        {
            Players = camp.Players
                .Select(item => item.PersonId == player.PersonId ? updatedPlayer : item)
                .ToArray()
        };
        updatedCamp.Validate();

        var eventType = ToEventType(decision.DecisionType);
        QueueCampEvent(
            registry,
            scenario,
            eventType,
            decision.DecisionType is TrainingCampDecisionType.MarkInjured ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            $"Camp decision: {player.PlayerName}",
            $"{player.PlayerName} was marked {status}.",
            decision.DecisionDate,
            player.PersonId);

        var updatedScenario = scenario with { TrainingCamp = updatedCamp };
        var inbox = CreateInboxForDecision(decision, player, status);

        return new TrainingCampDecisionResult(
            Success: true,
            ScenarioSnapshot: updatedScenario,
            Camp: updatedCamp,
            Decision: decision,
            InboxItems: inbox,
            Message: $"{player.PlayerName} is now {status}.");
    }

    public TrainingCampResult CompleteCamp(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var camp = RequireCamp(scenario);

        var rulebook = ResolveRulebook(registry);
        var validation = registry.RosterEngine.ValidateRoster(
            scenario.AlphaSnapshot.Roster,
            rulebook.RosterRules is null ? null : new RosterRuleValidator(rulebook));

        var kept = camp.Players.Count(player => player.Status == TrainingCampStatus.Kept);
        var cutOrReleased = camp.Players.Count(player => player.Status is TrainingCampStatus.Cut or TrainingCampStatus.Released);
        var assignedOrReturned = camp.Players.Count(player => player.Status is TrainingCampStatus.AssignedToAffiliate
            or TrainingCampStatus.ReturnedToParent
            or TrainingCampStatus.ReturnedToJuniorTeam);
        var injuryConcerns = camp.Players.Count(player => player.Status == TrainingCampStatus.Injured)
            + scenario.AlphaSnapshot.Injuries.Count(injury => injury.IsActive);

        var summary = new TrainingCampSummary(
            PlayersInvited: camp.Players.Count,
            PlayersKept: kept,
            PlayersCutOrReleased: cutOrReleased,
            PlayersAssignedOrReturned: assignedOrReturned,
            InjuryConcerns: injuryConcerns,
            RosterValidationResult: validation,
            StaffSummary: validation.IsValid
                ? $"Staff believe the opening roster is compliant. {kept} player(s) have been kept from camp."
                : $"Staff flagged opening roster issue: {validation.Message}");

        var completed = camp with
        {
            Summary = summary,
            CompletedOn = scenario.CurrentDate
        };
        completed.Validate();

        QueueCampEvent(
            registry,
            scenario,
            LegacyEventType.TrainingCampCompleted,
            validation.IsValid ? LegacyEventSeverity.Notice : LegacyEventSeverity.Warning,
            "Training camp completed",
            summary.StaffSummary,
            scenario.CurrentDate);

        var updatedScenario = scenario with { TrainingCamp = completed };
        var inbox = new[]
        {
            CreateInboxItem(
                scenario.CurrentDate,
                LegacyEventType.TrainingCampCompleted,
                validation.IsValid ? LegacyEventSeverity.Notice : LegacyEventSeverity.Warning,
                "Training Camp Summary",
                summary.StaffSummary,
                null)
        };

        return BuildResult(updatedScenario, completed, inbox, summary.StaffSummary);
    }

    private static TrainingCampPlayer CreatePlayer(
        NewGmScenarioSnapshot scenario,
        string personId,
        RosterPosition position,
        TrainingCampInviteType inviteType,
        DateOnly date,
        PlayerAcquisitionSource acquisitionSource,
        string? sourceOrganizationId = null) =>
        new(
            PersonId: personId,
            PlayerName: ResolvePlayerName(scenario, personId),
            Position: position,
            InviteType: inviteType,
            Status: TrainingCampStatus.Invited,
            InvitedOn: date,
            AcquisitionSource: acquisitionSource,
            SourceOrganizationId: sourceOrganizationId);

    private static TrainingCampEvaluation CreateEvaluation(
        NewGmScenarioSnapshot scenario,
        TrainingCampPlayer player,
        DateOnly date)
    {
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.SingleOrDefault(item => item.PersonId == player.PersonId);
        var injury = scenario.AlphaSnapshot.Injuries.FirstOrDefault(item => item.PersonId == player.PersonId && item.IsActive);
        var score = CalculateCampScore(profile, injury, player.InviteType);
        var readiness = score >= 72 ? "Opening roster ready" : score >= 58 ? "Bubble player" : "Needs development time";
        var upside = profile is null
            ? "Unknown upside; staff need more viewings."
            : profile.Potential - profile.CurrentAbility >= 18 ? "High development upside." : "Moderate development upside.";
        var coachNote = score >= 72
            ? "Coach sees a player who can handle structure now."
            : score >= 58 ? "Coach wants another look before final cuts." : "Coach recommends patience and targeted development.";
        var scoutNote = player.InviteType == TrainingCampInviteType.DraftedProspect
            ? "Scout note: draft profile suggests patience before heavy responsibility."
            : "Scout note: camp view matches current projection.";
        var riskNote = injury is null
            ? "Risk note: no active injury concern."
            : $"Risk note: active {injury.Severity} injury may affect readiness.";
        var recommendation = score >= 72
            ? "Recommendation: keep."
            : score >= 58 ? "Recommendation: bubble; compare against roster needs." : "Recommendation: cut, return, or develop outside the opening roster.";

        return new TrainingCampEvaluation(
            EvaluationId: $"camp-eval:{player.PersonId}:{date:yyyyMMdd}",
            PersonId: player.PersonId,
            PlayerName: player.PlayerName,
            Position: player.Position,
            CampScore: score,
            Readiness: readiness,
            DevelopmentUpside: upside,
            CoachNote: coachNote,
            ScoutNote: scoutNote,
            RiskNote: riskNote,
            Recommendation: recommendation,
            CreatedOn: date);
    }

    private static int CalculateCampScore(
        PlayerDevelopmentProfile? profile,
        Injury? injury,
        TrainingCampInviteType inviteType)
    {
        var baseScore = profile is null ? 52 : (profile.CurrentAbility + profile.Potential) / 2;
        var sourceAdjustment = inviteType switch
        {
            TrainingCampInviteType.ReturningRosterPlayer => 8,
            TrainingCampInviteType.DraftedProspect => -4,
            TrainingCampInviteType.Tryout => -6,
            TrainingCampInviteType.AssignedFromParentClub or TrainingCampInviteType.TwoWayContract => 4,
            _ => 0
        };
        var injuryPenalty = injury?.DevelopmentPenalty / 2 ?? 0;

        return Math.Clamp(baseScore + sourceAdjustment - injuryPenalty, 0, 100);
    }

    private static string? ValidateDecision(
        NewGmScenarioSnapshot scenario,
        Rulebook rulebook,
        TrainingCampPlayer player,
        TrainingCampDecisionType decisionType)
    {
        if (decisionType == TrainingCampDecisionType.ReturnToJuniorTeam && !SupportsJuniorReturn(rulebook, player))
        {
            return "Return to junior/youth team is unavailable for this player/rulebook.";
        }

        if (decisionType == TrainingCampDecisionType.PlaceOnWaivers && !IsNhlStyle(rulebook))
        {
            return "Waiver placement is only available for NHL-style rulebooks in v1.";
        }

        if (decisionType == TrainingCampDecisionType.AssignToAffiliate && IsNhlStyle(rulebook) && !IsWaiverExempt(player))
        {
            return "This player requires waivers before AHL assignment.";
        }

        if (decisionType == TrainingCampDecisionType.AssignToAffiliate && !SupportsAffiliateAssignment(scenario, rulebook))
        {
            return "Assign to affiliate is unavailable for this organization/rulebook.";
        }

        if (decisionType == TrainingCampDecisionType.ReturnToParent)
        {
            if (!SupportsParentReturn(scenario, rulebook, player))
            {
                return "Return to parent is unavailable for this player/source.";
            }
        }

        return null;
    }

    private static bool SupportsAffiliateAssignment(NewGmScenarioSnapshot scenario, Rulebook rulebook) =>
        (rulebook.AffiliateRules is { AffiliateEnabled: true } || IsNhlStyle(rulebook))
        && !string.IsNullOrWhiteSpace(scenario.Organization.AffiliateOrganizationId);

    private static bool SupportsParentReturn(
        NewGmScenarioSnapshot scenario,
        Rulebook rulebook,
        TrainingCampPlayer player) =>
        rulebook.AffiliateRules is { AffiliateEnabled: true }
        && !string.IsNullOrWhiteSpace(scenario.Organization.ParentOrganizationId)
        && player.InviteType is TrainingCampInviteType.AssignedFromParentClub
            or TrainingCampInviteType.LoanedFromParentClub
            or TrainingCampInviteType.TwoWayContract;

    private static TrainingCampStatus ToStatus(TrainingCampDecisionType decisionType) =>
        decisionType switch
        {
            TrainingCampDecisionType.Keep => TrainingCampStatus.Kept,
            TrainingCampDecisionType.Cut => TrainingCampStatus.Cut,
            TrainingCampDecisionType.Release => TrainingCampStatus.Released,
            TrainingCampDecisionType.ReturnToJuniorTeam => TrainingCampStatus.ReturnedToJuniorTeam,
            TrainingCampDecisionType.AssignToAffiliate => TrainingCampStatus.AssignedToAffiliate,
            TrainingCampDecisionType.ReturnToParent => TrainingCampStatus.ReturnedToParent,
            TrainingCampDecisionType.PlaceOnWaivers => TrainingCampStatus.PlacedOnWaivers,
            TrainingCampDecisionType.MarkInjured => TrainingCampStatus.Injured,
            _ => throw new ArgumentOutOfRangeException(nameof(decisionType), decisionType, "Unknown training camp decision.")
        };

    private static LegacyEventType ToEventType(TrainingCampDecisionType decisionType) =>
        decisionType switch
        {
            TrainingCampDecisionType.Keep => LegacyEventType.TrainingCampPlayerKept,
            TrainingCampDecisionType.AssignToAffiliate or TrainingCampDecisionType.ReturnToParent or TrainingCampDecisionType.ReturnToJuniorTeam or TrainingCampDecisionType.PlaceOnWaivers => LegacyEventType.TrainingCampPlayerAssigned,
            _ => LegacyEventType.TrainingCampPlayerCut
        };

    private TrainingCampCalendarResult CreateRosterDeadlinePendingActions(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        TrainingCampCalendarInfo info)
    {
        var pendingService = new PendingGmActionService();
        var current = scenario;
        var inbox = new List<AlphaInboxItem>();
        var needed = Math.Max(1, info.PlayersOverLimit);
        var candidates = current.TrainingCamp?.Players
            .Where(player => player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp or TrainingCampStatus.Kept)
            .OrderBy(player => current.TrainingCamp!.FindEvaluation(player.PersonId)?.CampScore ?? 100)
            .ThenBy(player => player.PlayerName, StringComparer.Ordinal)
            .Take(needed)
            .ToArray() ?? Array.Empty<TrainingCampPlayer>();

        foreach (var player in candidates)
        {
            if (current.PendingActions.Any(action => action.IsOpen && action.PersonId == player.PersonId && action.ActionType is PendingGmActionType.CutPlayer
                    or PendingGmActionType.ReleasePlayer
                    or PendingGmActionType.AssignToAffiliate
                    or PendingGmActionType.ReturnToParent
                    or PendingGmActionType.ReturnToJuniorTeam
                    or PendingGmActionType.PlaceOnWaivers))
            {
                continue;
            }

            var pendingType = SuggestedPendingActionType(ResolveRulebook(registry), player);
            var result = pendingService.CreatePendingAction(
                registry,
                current,
                pendingType,
                player.PersonId,
                $"Roster deadline is approaching and the club is over the rulebook limit: {info.RosterValidationResult.Message}",
                "GM must approve a roster cutdown decision before opening night.",
                player.Position,
                player.AcquisitionSource);
            current = result.ScenarioSnapshot;
            inbox.AddRange(result.InboxItems);
        }

        return new TrainingCampCalendarResult(
            current,
            inbox,
            inbox.Count == 0
                ? "Roster remains over limit; existing pending GM cutdown actions are still open."
                : $"Created {inbox.Count} urgent pending GM roster cutdown action(s).");
    }

    private static PendingGmActionType SuggestedPendingActionType(Rulebook rulebook, TrainingCampPlayer player)
    {
        if (rulebook.LeagueType.Contains("ahl", StringComparison.OrdinalIgnoreCase)
            && player.InviteType is TrainingCampInviteType.AssignedFromParentClub or TrainingCampInviteType.LoanedFromParentClub or TrainingCampInviteType.TwoWayContract)
        {
            return PendingGmActionType.ReturnToParent;
        }

        if (IsNhlStyle(rulebook))
        {
            return IsWaiverExempt(player) ? PendingGmActionType.AssignToAffiliate : PendingGmActionType.PlaceOnWaivers;
        }

        return player.InviteType is TrainingCampInviteType.Tryout
            ? PendingGmActionType.ReleasePlayer
            : PendingGmActionType.ReturnToJuniorTeam;
    }

    private static bool SupportsJuniorReturn(Rulebook rulebook, TrainingCampPlayer player) =>
        rulebook.LeagueType.Contains("junior", StringComparison.OrdinalIgnoreCase)
        || (IsNhlStyle(rulebook) && player.InviteType is TrainingCampInviteType.DraftedProspect or TrainingCampInviteType.Recruit);

    private static bool IsWaiverExempt(TrainingCampPlayer player) =>
        player.InviteType is TrainingCampInviteType.DraftedProspect or TrainingCampInviteType.Recruit or TrainingCampInviteType.Tryout;

    private static bool IsNhlStyle(Rulebook rulebook) =>
        rulebook.LeagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase);

    private static SeasonCalendar CalendarForCurrentCamp(NewGmScenarioSnapshot scenario) =>
        SeasonCalendar.Build(scenario.CurrentDate.Year, scenario.Season.Settings);

    private static DateOnly MilestoneDate(SeasonCalendar calendar, SeasonMilestoneType type) =>
        calendar.Milestones.Single(milestone => milestone.Type == type).Date.Value;

    private static IReadOnlyList<AlphaInboxItem> CreateInboxForDecision(
        TrainingCampDecision decision,
        TrainingCampPlayer player,
        TrainingCampStatus status)
    {
        if (decision.DecisionType is TrainingCampDecisionType.Cut or TrainingCampDecisionType.Release)
        {
            return Array.Empty<AlphaInboxItem>();
        }

        return new[]
        {
            CreateInboxItem(
                decision.DecisionDate,
                ToEventType(decision.DecisionType),
                decision.DecisionType == TrainingCampDecisionType.MarkInjured ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
                $"Camp Decision: {player.PlayerName}",
                $"{player.PlayerName} was marked {status}.",
                player.PersonId)
        };
    }

    private static void QueueCampEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        LegacyEventSeverity severity,
        string title,
        string description,
        DateOnly date,
        string? personId = null)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            ToDateTimeOffset(date),
            eventType,
            severity,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: personId,
                OrganizationId: scenario.Organization.OrganizationId,
                SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["organization_id"] = scenario.Organization.OrganizationId,
                ["scenario"] = "new_gm_training_camp"
            });

        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem CreateInboxItem(
        DateOnly date,
        LegacyEventType eventType,
        LegacyEventSeverity severity,
        string title,
        string summary,
        string? personId) =>
        new(
            InboxItemId: $"inbox:camp:{eventType}:{date:yyyyMMdd}:{Guid.NewGuid():N}",
            Date: ToDateTimeOffset(date),
            EventType: eventType,
            Severity: severity,
            Title: title,
            Summary: summary,
            PrimaryPersonId: personId);

    private static TrainingCampResult BuildResult(
        NewGmScenarioSnapshot scenario,
        TrainingCamp camp,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        string summary)
    {
        var result = new TrainingCampResult(scenario, camp, inboxItems, summary);
        result.Validate();
        return result;
    }

    private static TrainingCamp RequireCamp(NewGmScenarioSnapshot scenario) =>
        scenario.TrainingCamp ?? throw new InvalidOperationException("Training camp has not been opened.");

    private static Rulebook ResolveRulebook(EngineRegistry registry) =>
        registry.Rulebook ?? RulebookPresets.CreateJuniorMajor();

    private static RosterPosition GuessPosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.AlphaSnapshot.Roster.Players
            .OrderBy(player => player.PersonId, StringComparer.Ordinal)
            .FirstOrDefault()?.Position
        ?? RosterPosition.Unknown;

    private static string ResolvePlayerName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private static void AddIfMissing(List<TrainingCampPlayer> players, TrainingCampPlayer player)
    {
        if (!players.Any(existing => existing.PersonId == player.PersonId))
        {
            players.Add(player);
        }
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero);
}
