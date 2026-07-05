using LegacyEngine.Events;
using LegacyEngine.Integration;

internal sealed class InboxManagerTests
{
    public void MessageCategorizedCorrectly()
    {
        var manager = new InboxManager();
        var owner = manager.Add(Item("owner", LegacyEventType.OwnerGoalSet));
        var scouting = manager.Add(Item("scouting", LegacyEventType.ScoutAssigned));
        var medical = manager.Add(Item("medical", LegacyEventType.PlayerInjured));
        var draft = manager.Add(Item("draft", LegacyEventType.DraftStarted));

        Assert.Equal(InboxCategory.Owner, owner.Category);
        Assert.Equal(InboxCategory.Scouting, scouting.Category);
        Assert.Equal(InboxCategory.Medical, medical.Category);
        Assert.Equal(InboxCategory.Draft, draft.Category);
    }

    public void ReadUnreadWorks()
    {
        var manager = Seed();
        var message = manager.Query(new InboxFilter()).First();

        manager.ApplyAction(message.InboxItemId, InboxMessageAction.MarkRead);
        Assert.Equal(InboxMessageStatus.Read, manager.Query(new InboxFilter()).First(item => item.InboxItemId == message.InboxItemId).Status);

        manager.ApplyAction(message.InboxItemId, InboxMessageAction.MarkUnread);
        Assert.Equal(InboxMessageStatus.Unread, manager.Query(new InboxFilter()).First(item => item.InboxItemId == message.InboxItemId).Status);
    }

    public void ArchiveWorks()
    {
        var manager = Seed();
        var message = manager.Query(new InboxFilter()).First();

        manager.ApplyAction(message.InboxItemId, InboxMessageAction.Archive);

        Assert.False(manager.Query(new InboxFilter()).Any(item => item.InboxItemId == message.InboxItemId), "Archived message should leave the default inbox.");
        Assert.True(manager.Query(new InboxFilter(IncludeArchived: true)).Any(item => item.InboxItemId == message.InboxItemId), "Archived message should remain queryable.");
    }

    public void DeleteHidesMessageFromInbox()
    {
        var manager = Seed();
        var message = manager.Query(new InboxFilter()).First();

        manager.ApplyAction(message.InboxItemId, InboxMessageAction.Delete);

        Assert.False(manager.Query(new InboxFilter()).Any(item => item.InboxItemId == message.InboxItemId), "Deleted message should leave the default inbox.");
        Assert.True(manager.Query(new InboxFilter(IncludeDeleted: true)).Any(item => item.InboxItemId == message.InboxItemId), "Deleted message should remain in inbox manager history.");
    }

    public void PinWorks()
    {
        var manager = Seed();
        var oldest = manager.Query(new InboxFilter()).Last();

        manager.ApplyAction(oldest.InboxItemId, InboxMessageAction.Pin);

        Assert.Equal(oldest.InboxItemId, manager.Query(new InboxFilter()).First().InboxItemId);
        Assert.True(manager.Query(new InboxFilter()).First().IsPinned, "Pinned message should be marked pinned.");
    }

    public void FiltersWork()
    {
        var manager = Seed();
        var warning = manager.Add(Item("warning", LegacyEventType.PlayerInjured, LegacyEventSeverity.Warning));
        manager.ApplyAction(manager.Query(new InboxFilter()).First(item => item.InboxItemId != warning.InboxItemId).InboxItemId, InboxMessageAction.MarkRead);

        Assert.True(manager.Query(new InboxFilter(UnreadOnly: true)).All(item => item.Status == InboxMessageStatus.Unread), "Unread filter should return only unread messages.");
        Assert.True(manager.Query(new InboxFilter(ImportantOnly: true)).All(item => item.IsImportant), "Important filter should return only important messages.");
        Assert.True(manager.Query(new InboxFilter(InboxCategory.Medical)).All(item => item.Category == InboxCategory.Medical), "Category filter should return only that category.");
    }

    public void EventHistoryIsPreserved()
    {
        var eventEngine = new EventEngine();
        var legacyEvent = eventEngine.QueueEvent(eventEngine.CreateEvent(
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
            LegacyEventType.OwnerGoalSet,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            "Owner goal",
            "Owner set expectations."));
        eventEngine.ProcessQueuedEvents(new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero));

        var manager = new InboxManager();
        var message = manager.Add(new AlphaInboxItem(
            $"inbox:{legacyEvent.EventId}",
            legacyEvent.OccurredAt,
            legacyEvent.EventType,
            legacyEvent.Severity,
            legacyEvent.Title,
            legacyEvent.Description,
            legacyEvent.Context.PrimaryPersonId));
        manager.ApplyAction(message.InboxItemId, InboxMessageAction.Delete);

        Assert.Equal(1, eventEngine.History.Count);
    }

    public void AlphaDesktopCanDisplayCategories()
    {
        var manager = Seed();

        foreach (var category in Enum.GetValues<InboxCategory>())
        {
            _ = manager.Query(new InboxFilter(category));
        }

        Assert.True(manager.Count > 0, "Desktop can query category views from the inbox manager.");
    }

    private static InboxManager Seed()
    {
        var manager = new InboxManager();
        manager.Add(Item("owner", LegacyEventType.OwnerGoalSet));
        manager.Add(Item("staff", LegacyEventType.StaffAssigned));
        manager.Add(Item("scouting", LegacyEventType.ScoutAssigned));
        manager.Add(Item("recruiting", LegacyEventType.RecruitingOfferSubmitted));
        return manager;
    }

    private static AlphaInboxItem Item(
        string id,
        LegacyEventType eventType,
        LegacyEventSeverity severity = LegacyEventSeverity.Notice) =>
        new(
            InboxItemId: $"inbox-test-{id}",
            Date: new DateTimeOffset(2026, 6, 15, 9, Math.Abs(id.GetHashCode()) % 59, 0, TimeSpan.Zero),
            EventType: eventType,
            Severity: severity,
            Title: $"{id} title",
            Summary: $"{id} summary",
            PrimaryPersonId: null);
}
