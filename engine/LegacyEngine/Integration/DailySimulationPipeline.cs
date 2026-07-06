using LegacyEngine.Development;
using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Relationships;
using LegacyEngine.Seasons;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class DailySimulationPipeline
{
    private static readonly HashSet<LegacyEventType> ImportantCommunicationEvents =
    [
        LegacyEventType.PlayerInjured,
        LegacyEventType.PlayerRecovered,
        LegacyEventType.InjuryReAggravated,
        LegacyEventType.InjuryCareerThreatening,
        LegacyEventType.RecruitingOfferSubmitted,
        LegacyEventType.RecruitCommitted,
        LegacyEventType.RecruitRejected,
        LegacyEventType.ContractOffered,
        LegacyEventType.ContractSigned,
        LegacyEventType.ContractRejected,
        LegacyEventType.ContractTerminated,
        LegacyEventType.PlayerDrafted,
        LegacyEventType.DraftCompleted,
        LegacyEventType.DraftRecapCreated,
        LegacyEventType.DraftBoardChanged,
        LegacyEventType.ScoutRecommendationUpdated,
        LegacyEventType.OwnerDraftReaction,
        LegacyEventType.PendingGmActionCreated,
        LegacyEventType.ProspectDecisionMade,
        LegacyEventType.ProspectContractOffered,
        LegacyEventType.ProspectSigned,
        LegacyEventType.ProspectInvitedToCamp,
        LegacyEventType.ProspectReturned,
        LegacyEventType.ProspectAssignedToAffiliate,
        LegacyEventType.ProspectRightsReleased,
        LegacyEventType.OpeningRosterValidated,
        LegacyEventType.OpeningRosterRejected,
        LegacyEventType.SeasonReady,
        LegacyEventType.OwnerOffseasonReview,
        LegacyEventType.CoachRosterReview,
        LegacyEventType.ScoutOffseasonReview,
        LegacyEventType.FrontOfficeReadinessReportCreated,
        LegacyEventType.EndOfSeasonExecutiveReviewCreated,
        LegacyEventType.ScoutAssignedToRegion,
        LegacyEventType.ScoutAssignedToPlayer,
        LegacyEventType.ScoutAssignmentCompleted,
        LegacyEventType.ScoutingReportUpdated,
        LegacyEventType.StaffRoleChanged,
        LegacyEventType.StaffReleased,
        LegacyEventType.StaffConflictWarning,
        LegacyEventType.TrainingCampOpened,
        LegacyEventType.TrainingCampEvaluationCreated,
        LegacyEventType.TrainingCampPlayerKept,
        LegacyEventType.TrainingCampPlayerCut,
        LegacyEventType.TrainingCampPlayerAssigned,
        LegacyEventType.TrainingCampCompleted,
        LegacyEventType.PlayerAddedToRoster,
        LegacyEventType.PlayerReleased,
        LegacyEventType.OwnerGoalSet,
        LegacyEventType.BudgetApproved,
        LegacyEventType.ScoutAssigned,
        LegacyEventType.GamePlayed
    ];

    public AlphaSimulationResult RunOneDay(EngineRegistry registry, AlphaWorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();

        var log = new List<DailySimulationLogEntry>();

        var previousDate = snapshot.CurrentDate;
        var dailyResult = registry.WorldEngine.AdvanceOneDay();
        var currentDate = registry.WorldEngine.State.CurrentDate.Value;
        var season = snapshot.Season is null
            ? null
            : registry.SeasonEngine.AdvanceTo(snapshot.Season, currentDate).Season;
        if (season is not null)
        {
            registry.WorldEngine.SetPhase(ToWorldPhase(season.CurrentPhase));
        }

        log.Add(Log(
            DailySimulationStep.AdvanceWorldClock,
            $"Advanced world clock from {previousDate:yyyy-MM-dd} to {currentDate:yyyy-MM-dd}.",
            new Dictionary<string, object?>
            {
                ["previous_date"] = previousDate,
                ["current_date"] = currentDate,
                ["season_phase"] = season?.CurrentPhase.ToString()
            }));

        var processedEvents = ResolveProcessedEvents(registry, dailyResult);
        log.Add(Log(
            DailySimulationStep.ProcessQueuedEvents,
            $"Processed {dailyResult.ProcessedEventCount} queued event(s).",
            new Dictionary<string, object?> { ["processed_event_count"] = dailyResult.ProcessedEventCount }));

        var relationships = ApplyRelationshipDecay(snapshot.Relationships, currentDate);
        var relationshipChanges = relationships
            .Zip(snapshot.Relationships, (updated, original) => updated.History.Count - original.History.Count)
            .Sum();
        log.Add(Log(
            DailySimulationStep.ApplyRelationshipDecay,
            $"Applied relationship decay to {relationships.Count} relationship(s).",
            new Dictionary<string, object?>
            {
                ["relationship_count"] = relationships.Count,
                ["history_entries_added"] = relationshipChanges
            }));

        var development = ApplyDevelopmentUpdates(registry, snapshot, currentDate, season?.CurrentPhase);
        log.Add(Log(
            DailySimulationStep.ApplyPlayerDevelopmentUpdates,
            development.Summary,
            new Dictionary<string, object?>
            {
                ["development_profile_count"] = development.Profiles.Count,
                ["profiles_evaluated"] = development.ProfilesEvaluated,
                ["development_inbox_count"] = development.InboxItems.Count,
                ["skipped"] = development.WasSkipped
            }));

        var injuries = ApplyInjuryRecoveryUpdates(registry, snapshot.Injuries, currentDate);
        log.Add(Log(
            DailySimulationStep.ApplyInjuryRecoveryUpdates,
            $"Updated {injuries.Count} injury record(s).",
            new Dictionary<string, object?> { ["injury_count"] = injuries.Count }));

        log.Add(Log(
            DailySimulationStep.CheckContractStatuses,
            "Contract status checks skipped; Alpha snapshot does not track contracts yet.",
            new Dictionary<string, object?> { ["contracts_tracked"] = 0 }));

        var recruits = ProgressRecruiting(registry, snapshot, currentDate);
        log.Add(Log(
            DailySimulationStep.ProgressRecruiting,
            $"Progressed {recruits.Count} recruit profile(s).",
            new Dictionary<string, object?> { ["recruit_count"] = recruits.Count }));

        var messages = GenerateCommunicationMessages(processedEvents);
        log.Add(Log(
            DailySimulationStep.GenerateCommunicationMessages,
            $"Generated {messages.Count} communication message(s).",
            new Dictionary<string, object?> { ["message_count"] = messages.Count }));

        var inboxItems = messages.Select(ToInboxItem).Concat(development.InboxItems).ToArray();
        log.Add(Log(
            DailySimulationStep.ConvertInboxItems,
            $"Converted {inboxItems.Length} message(s) into inbox item(s).",
            new Dictionary<string, object?> { ["inbox_item_count"] = inboxItems.Length }));

        var updatedSnapshot = snapshot with
        {
            WorldState = registry.WorldEngine.State,
            Season = season,
            Relationships = relationships,
            DevelopmentProfiles = development.Profiles,
            Injuries = injuries,
            Recruits = recruits
        };
        updatedSnapshot.Validate();

        log.Add(Log(
            DailySimulationStep.ReturnSimulationResult,
            "Returned alpha daily simulation result.",
            new Dictionary<string, object?> { ["current_date"] = updatedSnapshot.CurrentDate }));

        return new AlphaSimulationResult(
            CurrentDate: updatedSnapshot.CurrentDate,
            ProcessedEventCount: dailyResult.ProcessedEventCount,
            InboxItems: inboxItems,
            CommunicationMessages: messages,
            LogEntries: log,
            Summary: BuildSummary(updatedSnapshot, dailyResult, inboxItems, log),
            WorldSnapshot: updatedSnapshot);
    }

    public IReadOnlyList<AlphaSimulationResult> RunDays(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Daily simulation cannot advance a negative number of days.");
        }

        var results = new List<AlphaSimulationResult>();
        var currentSnapshot = snapshot;
        for (var day = 0; day < days; day++)
        {
            var result = RunOneDay(registry, currentSnapshot);
            results.Add(result);
            currentSnapshot = result.WorldSnapshot;
        }

        return results;
    }

    private static IReadOnlyList<Relationship> ApplyRelationshipDecay(
        IReadOnlyList<Relationship> relationships,
        DateOnly currentDate) =>
        relationships
            .Select(relationship => relationship.ApplyDecay(currentDate, daysBeforeDecay: 1, amountPerPeriod: 1))
            .ToArray();

    private static DevelopmentPipelineResult ApplyDevelopmentUpdates(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        DateOnly currentDate,
        SeasonPhase? seasonPhase)
    {
        if (!IsDevelopmentActive(snapshot, seasonPhase))
        {
            return new DevelopmentPipelineResult(
                snapshot.DevelopmentProfiles,
                Array.Empty<AlphaInboxItem>(),
                ProfilesEvaluated: 0,
                WasSkipped: true,
                Summary: "Player development skipped; offseason/draft/free-agency periods do not run daily development.");
        }

        var injuryPenaltyByPerson = snapshot.Injuries
            .Where(injury => injury.IsActive)
            .GroupBy(injury => injury.PersonId)
            .ToDictionary(
                group => group.Key,
                group => Math.Clamp(group.Max(injury => injury.DevelopmentPenalty), 0, 100),
                StringComparer.Ordinal);

        var meaningfulUpdates = new List<(DevelopmentResult Result, DevelopmentFactor Factor, Person? Person, string Reason)>();
        var profiles = snapshot.DevelopmentProfiles
            .Select(profile =>
            {
                var person = snapshot.People.SingleOrDefault(item => item.PersonId == profile.PersonId);
                var age = person?.CalculateAge(currentDate) ?? 18;
                var injuryPenalty = injuryPenaltyByPerson.GetValueOrDefault(profile.PersonId, 0);
                var factor = new DevelopmentFactor(
                    Age: age,
                    UpdateDate: currentDate,
                    IceTimeScore: snapshot.Roster.HasActivePlayer(profile.PersonId) ? 65 : 35,
                    FacilityBonus: 20,
                    CoachingBonus: 20,
                    InjuryPenalty: injuryPenalty,
                    RandomModifier: 0);
                var result = registry.DevelopmentEngine.ApplyMonthlyUpdate(profile, factor);

                var reason = MeaningfulDevelopmentReason(result, factor, profile);
                if (reason is not null)
                {
                    meaningfulUpdates.Add((result, factor, person, reason));
                }

                return result.UpdatedProfile;
            })
            .ToArray();

        var inboxItems = meaningfulUpdates
            .OrderByDescending(item => item.Result.IsBreakout || item.Result.IsRegression)
            .ThenByDescending(item => Math.Abs(item.Result.CurrentAbilityChange))
            .ThenBy(item => item.Person?.Identity.DisplayName ?? item.Result.PersonId, StringComparer.Ordinal)
            .Take(3)
            .Select(item => ToDevelopmentInboxItem(snapshot, item.Result, item.Factor, item.Person, item.Reason))
            .ToArray();

        return new DevelopmentPipelineResult(
            profiles,
            inboxItems,
            ProfilesEvaluated: profiles.Length,
            WasSkipped: false,
            Summary: inboxItems.Length == 0
                ? $"Updated {profiles.Length} development profile(s); routine changes stayed in the internal simulation log."
                : $"Updated {profiles.Length} development profile(s); created {inboxItems.Length} meaningful development inbox item(s).");
    }

    private static bool IsDevelopmentActive(AlphaWorldSnapshot snapshot, SeasonPhase? seasonPhase)
    {
        if (seasonPhase is not null)
        {
            return seasonPhase is SeasonPhase.Preseason or SeasonPhase.RegularSeason or SeasonPhase.TradeDeadline;
        }

        return snapshot.WorldState.CurrentPhase is WorldPhase.Preseason or WorldPhase.RegularSeason;
    }

    private static string? MeaningfulDevelopmentReason(
        DevelopmentResult result,
        DevelopmentFactor factor,
        PlayerDevelopmentProfile originalProfile)
    {
        if (result.IsBreakout)
        {
            return "breakout";
        }

        if (result.IsRegression)
        {
            return "regression";
        }

        if (factor.InjuryPenalty.GetValueOrDefault() >= 10 && result.CurrentAbilityChange <= 0)
        {
            return "injury-related slowdown";
        }

        var confidenceChange = result.Updates
            .FirstOrDefault(update => update.Attribute == DevelopmentAttribute.Confidence);
        if (confidenceChange is not null && Math.Abs(confidenceChange.NewValue - confidenceChange.PreviousValue) >= 2)
        {
            return "major confidence change";
        }

        if (originalProfile.Potential >= 70 && result.CurrentAbilityChange >= 2)
        {
            return "top prospect development update";
        }

        return null;
    }

    private static AlphaInboxItem ToDevelopmentInboxItem(
        AlphaWorldSnapshot snapshot,
        DevelopmentResult result,
        DevelopmentFactor factor,
        Person? person,
        string reason)
    {
        var playerName = person?.Identity.DisplayName ?? result.PersonId;
        var strongestUpdate = result.Updates
            .OrderByDescending(update => Math.Abs(update.NewValue - update.PreviousValue))
            .FirstOrDefault();
        var theme = strongestUpdate?.Attribute.ToString() ?? "overall development";
        var direction = result.CurrentAbilityChange switch
        {
            >= 4 => "breakout",
            > 0 => "improvement",
            0 => "holding steady",
            <= -3 => "regression",
            _ => "minor slowdown"
        };
        var coachNote = CoachNoteFor(reason, factor);
        var whyItMatters = reason switch
        {
            "breakout" => "This matters because a real jump can change how aggressively the staff plans the player's role.",
            "regression" => "This matters because the staff may need to adjust workload, confidence support, or development expectations.",
            "injury-related slowdown" => "This matters because health is now affecting the player's development path.",
            "major confidence change" => "This matters because confidence can alter practice habits and game readiness.",
            _ => "This matters because the player is part of the club's priority development group."
        };

        return new AlphaInboxItem(
            InboxItemId: $"inbox:development:{Guid.NewGuid():N}",
            Date: new DateTimeOffset(result.UpdateDate.Year, result.UpdateDate.Month, result.UpdateDate.Day, 13, 30, 0, TimeSpan.Zero),
            EventType: result.IsBreakout
                ? LegacyEventType.PlayerBreakout
                : result.IsRegression
                    ? LegacyEventType.PlayerRegression
                    : LegacyEventType.PlayerDevelopmentUpdated,
            Severity: result.IsBreakout || result.IsRegression ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            Title: $"Player Development Update: {playerName}",
            Summary: $"{playerName}, age {factor.Age}, is showing {direction} around {theme}. {coachNote} {whyItMatters}",
            PrimaryPersonId: result.PersonId);
    }

    private static string CoachNoteFor(string reason, DevelopmentFactor factor) =>
        reason switch
        {
            "injury-related slowdown" => "Staff believe the injury load is limiting practice quality.",
            "major confidence change" => "Coaches flagged confidence as the most important trend to monitor.",
            "breakout" => "Staff believe ice time, habits, and coaching support are starting to connect.",
            "regression" => "Coaches want to review workload and confidence before this becomes a pattern.",
            _ when factor.IceTimeScore.GetValueOrDefault() >= 60 => "Coaches point to useful ice time and steady practice habits.",
            _ => "Coaches want a little more evidence before changing the player's plan."
        };

    private static IReadOnlyList<Injury> ApplyInjuryRecoveryUpdates(
        EngineRegistry registry,
        IReadOnlyList<Injury> injuries,
        DateOnly currentDate) =>
        injuries
            .Select(injury =>
            {
                if (!injury.IsActive || injury.Status == InjuryStatus.CareerThreatening)
                {
                    return injury;
                }

                var result = registry.InjuryEngine.ApplyRecoveryUpdate(
                    injury,
                    new InjuryRecoveryUpdate(
                        Date: currentDate,
                        RecoveryProgressDelta: 5,
                        GamesMissedIncrease: 1,
                        Notes: "Daily alpha recovery progress."));

                return result.Injury;
            })
            .ToArray();

    private static IReadOnlyList<RecruitProfile> ProgressRecruiting(
        EngineRegistry registry,
        AlphaWorldSnapshot snapshot,
        DateOnly currentDate) =>
        snapshot.Recruits
            .Select(recruit => recruit.Status is RecruitStatus.Available or RecruitStatus.Interested
                ? registry.RecruitingEngine.ChangeInterest(recruit, snapshot.OrganizationId, 1, currentDate)
                : recruit)
            .ToArray();

    private static IReadOnlyList<LegacyEvent> ResolveProcessedEvents(
        EngineRegistry registry,
        DailySimulationResult dailyResult) =>
        dailyResult.ProcessedEvents
            .Select(result => registry.EventEngine.History.AllEvents.SingleOrDefault(item => item.EventId == result.EventId))
            .Where(item => item is not null)
            .Cast<LegacyEvent>()
            .ToArray();

    private static IReadOnlyList<AlphaCommunicationMessage> GenerateCommunicationMessages(
        IReadOnlyList<LegacyEvent> processedEvents) =>
        processedEvents
            .Where(IsImportantEvent)
            .Select(ToCommunicationMessage)
            .ToArray();

    private static bool IsImportantEvent(LegacyEvent legacyEvent) =>
        ImportantCommunicationEvents.Contains(legacyEvent.EventType)
        || legacyEvent.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical;

    private static AlphaCommunicationMessage ToCommunicationMessage(LegacyEvent legacyEvent) =>
        new(
            MessageId: $"message:{legacyEvent.EventId}",
            Date: legacyEvent.OccurredAt,
            SourceEventType: legacyEvent.EventType,
            Severity: legacyEvent.Severity,
            Subject: legacyEvent.Title,
            Body: legacyEvent.Description,
            PrimaryPersonId: legacyEvent.Context.PrimaryPersonId);

    private static AlphaInboxItem ToInboxItem(AlphaCommunicationMessage message) =>
        new(
            InboxItemId: $"inbox:{message.MessageId}",
            Date: message.Date,
            EventType: message.SourceEventType,
            Severity: message.Severity,
            Title: message.Subject,
            Summary: message.Body,
            PrimaryPersonId: message.PrimaryPersonId);

    private static DailySimulationLogEntry Log(
        DailySimulationStep step,
        string message,
        IReadOnlyDictionary<string, object?> details) =>
        new(step, true, message, details);

    private static string BuildSummary(
        AlphaWorldSnapshot snapshot,
        DailySimulationResult dailyResult,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        IReadOnlyList<DailySimulationLogEntry> logEntries) =>
        $"Advanced {snapshot.WorldState.WorldName} to {snapshot.CurrentDate:yyyy-MM-dd}; processed {dailyResult.ProcessedEventCount} event(s), created {inboxItems.Count} inbox item(s), and ran {logEntries.Count} pipeline step(s).";

    private static WorldPhase ToWorldPhase(SeasonPhase seasonPhase) =>
        seasonPhase switch
        {
            SeasonPhase.Preseason => WorldPhase.Preseason,
            SeasonPhase.RegularSeason or SeasonPhase.TradeDeadline => WorldPhase.RegularSeason,
            SeasonPhase.Playoffs or SeasonPhase.Championship => WorldPhase.Playoffs,
            SeasonPhase.Draft => WorldPhase.Draft,
            SeasonPhase.FreeAgency => WorldPhase.FreeAgency,
            _ => WorldPhase.Offseason
        };
}
