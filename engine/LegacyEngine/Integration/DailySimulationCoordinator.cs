using LegacyEngine.Events;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class DailySimulationCoordinator
{
    private static readonly HashSet<LegacyEventType> ImportantInboxEvents =
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
        LegacyEventType.PlayerDrafted,
        LegacyEventType.DraftCompleted,
        LegacyEventType.PlayerAddedToRoster,
        LegacyEventType.PlayerReleased,
        LegacyEventType.PlayerDevelopmentUpdated,
        LegacyEventType.PlayerBreakout,
        LegacyEventType.PlayerRegression,
        LegacyEventType.OwnerGoalSet,
        LegacyEventType.BudgetApproved
    ];

    public AlphaSimulationResult AdvanceOneDay(EngineRegistry registry, AlphaWorldSnapshot snapshot)
    {
        snapshot.Validate();

        var dailyResult = registry.WorldEngine.AdvanceOneDay();
        var updatedSnapshot = snapshot with { WorldState = registry.WorldEngine.State };
        updatedSnapshot.Validate();

        var inboxItems = dailyResult.ProcessedEvents
            .Select(result => registry.EventEngine.History.AllEvents.SingleOrDefault(item => item.EventId == result.EventId))
            .Where(item => item is not null)
            .Cast<LegacyEvent>()
            .Where(IsInboxEvent)
            .Select(ToInboxItem)
            .ToArray();

        return new AlphaSimulationResult(
            CurrentDate: updatedSnapshot.CurrentDate,
            ProcessedEventCount: dailyResult.ProcessedEventCount,
            InboxItems: inboxItems,
            Summary: BuildSummary(updatedSnapshot, dailyResult, inboxItems),
            WorldSnapshot: updatedSnapshot);
    }

    private static bool IsInboxEvent(LegacyEvent legacyEvent) =>
        ImportantInboxEvents.Contains(legacyEvent.EventType)
        || legacyEvent.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical;

    private static AlphaInboxItem ToInboxItem(LegacyEvent legacyEvent) =>
        new(
            InboxItemId: $"inbox:{legacyEvent.EventId}",
            Date: legacyEvent.OccurredAt,
            EventType: legacyEvent.EventType,
            Severity: legacyEvent.Severity,
            Title: legacyEvent.Title,
            Summary: legacyEvent.Description,
            PrimaryPersonId: legacyEvent.Context.PrimaryPersonId);

    private static string BuildSummary(
        AlphaWorldSnapshot snapshot,
        DailySimulationResult dailyResult,
        IReadOnlyList<AlphaInboxItem> inboxItems) =>
        $"Advanced {snapshot.WorldState.WorldName} to {snapshot.CurrentDate:yyyy-MM-dd}; processed {dailyResult.ProcessedEventCount} event(s) and created {inboxItems.Count} inbox item(s).";
}
