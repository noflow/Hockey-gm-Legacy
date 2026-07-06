using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed record InboxMessage(
    AlphaInboxItem Item,
    InboxCategory Category,
    InboxMessageStatus Status,
    bool IsPinned,
    bool ReplyAvailable = false,
    InboxPriority Priority = InboxPriority.Normal)
{
    public string InboxItemId => Item.InboxItemId;

    public bool IsUnread => Status == InboxMessageStatus.Unread;

    public bool IsArchived => Status == InboxMessageStatus.Archived;

    public bool IsDeleted => Status == InboxMessageStatus.Deleted;

    public bool IsImportant =>
        IsPinned
        || Priority is InboxPriority.Important or InboxPriority.Urgent
        || Item.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InboxItemId))
        {
            throw new ArgumentException("Inbox item id is required.", nameof(Item));
        }

        if (string.IsNullOrWhiteSpace(Item.Title))
        {
            throw new ArgumentException("Inbox title is required.", nameof(Item));
        }
    }
}
