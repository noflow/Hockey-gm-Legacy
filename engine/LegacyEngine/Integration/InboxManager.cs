using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed class InboxManager
{
    private readonly List<InboxMessage> _messages = [];

    public IReadOnlyList<InboxMessage> AllMessages => Ordered(_messages, includeDeleted: true);

    public int Count => Query(new InboxFilter()).Count;

    public InboxMessage Add(AlphaInboxItem item)
    {
        var message = new InboxMessage(
            Item: item,
            Category: Categorize(item),
            Status: InboxMessageStatus.Unread,
            IsPinned: item.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical,
            ReplyAvailable: CanReply(item));
        message.Validate();

        var existingIndex = _messages.FindIndex(existing => existing.InboxItemId == message.InboxItemId);
        if (existingIndex >= 0)
        {
            _messages[existingIndex] = message;
        }
        else
        {
            _messages.Add(message);
        }

        return message;
    }

    public IReadOnlyList<InboxMessage> AddRange(IEnumerable<AlphaInboxItem> items) =>
        items.Select(Add).ToArray();

    public InboxMessage ApplyAction(string inboxItemId, InboxMessageAction action)
    {
        if (string.IsNullOrWhiteSpace(inboxItemId))
        {
            throw new ArgumentException("Inbox item id is required.", nameof(inboxItemId));
        }

        if (action == InboxMessageAction.Reply)
        {
            throw new NotSupportedException("Reply actions are reserved for a future conversation system.");
        }

        var index = _messages.FindIndex(message => message.InboxItemId == inboxItemId);
        if (index < 0)
        {
            throw new ArgumentException("Inbox message was not found.", nameof(inboxItemId));
        }

        var current = _messages[index];
        var updated = action switch
        {
            InboxMessageAction.MarkRead => current with { Status = InboxMessageStatus.Read },
            InboxMessageAction.MarkUnread => current with { Status = InboxMessageStatus.Unread },
            InboxMessageAction.Archive => current with { Status = InboxMessageStatus.Archived },
            InboxMessageAction.Delete => current with { Status = InboxMessageStatus.Deleted },
            InboxMessageAction.Pin => current with { IsPinned = true },
            InboxMessageAction.Unpin => current with { IsPinned = false },
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported inbox action.")
        };
        updated.Validate();
        _messages[index] = updated;
        return updated;
    }

    public IReadOnlyList<InboxMessage> Query(InboxFilter filter)
    {
        var query = _messages.AsEnumerable();

        if (!filter.IncludeArchived)
        {
            query = query.Where(message => !message.IsArchived);
        }

        if (!filter.IncludeDeleted)
        {
            query = query.Where(message => !message.IsDeleted);
        }

        if (filter.Category != InboxCategory.All)
        {
            query = query.Where(message => message.Category == filter.Category);
        }

        if (filter.UnreadOnly)
        {
            query = query.Where(message => message.IsUnread);
        }

        if (filter.ImportantOnly)
        {
            query = query.Where(message => message.IsImportant);
        }

        return Ordered(query, includeDeleted: filter.IncludeDeleted);
    }

    public IReadOnlyDictionary<InboxCategory, int> CountsByCategory() =>
        Enum.GetValues<InboxCategory>()
            .ToDictionary(
                category => category,
                category => Query(new InboxFilter(category)).Count);

    public static InboxCategory Categorize(AlphaInboxItem item) =>
        item.EventType switch
        {
            LegacyEventType.OwnerGoalSet or LegacyEventType.BudgetApproved => InboxCategory.Owner,
            LegacyEventType.StaffHired or LegacyEventType.StaffAssigned or LegacyEventType.StaffReassigned or LegacyEventType.StaffReleased or LegacyEventType.StaffEvaluated => InboxCategory.Staff,
            LegacyEventType.ScoutAssigned or LegacyEventType.ScoutingReportCreated => InboxCategory.Scouting,
            LegacyEventType.RecruitingOfferSubmitted or LegacyEventType.RecruitCommitted or LegacyEventType.RecruitRejected or LegacyEventType.RecruitingOpened or LegacyEventType.RecruitingClosed => InboxCategory.Recruiting,
            LegacyEventType.PlayerDevelopmentUpdated or LegacyEventType.PlayerBreakout or LegacyEventType.PlayerRegression => InboxCategory.PlayerDevelopment,
            LegacyEventType.PlayerInjured or LegacyEventType.PlayerRecovered or LegacyEventType.InjuryReAggravated or LegacyEventType.InjuryCareerThreatening or LegacyEventType.PlayerMovedToInjuredReserve => InboxCategory.Medical,
            LegacyEventType.ContractOffered or LegacyEventType.ContractSigned or LegacyEventType.ContractRejected or LegacyEventType.ContractTerminated => InboxCategory.Contracts,
            LegacyEventType.DraftStarted or LegacyEventType.PlayerDrafted or LegacyEventType.DraftCompleted or LegacyEventType.DraftOpened or LegacyEventType.DraftClosed => InboxCategory.Draft,
            LegacyEventType.SeasonCreated or LegacyEventType.PhaseChanged or LegacyEventType.MilestoneReached or LegacyEventType.FreeAgencyOpened or LegacyEventType.FreeAgencyClosed or LegacyEventType.SeasonStarted or LegacyEventType.SeasonEnded => InboxCategory.League,
            _ => CategorizeByText(item)
        };

    private static InboxCategory CategorizeByText(AlphaInboxItem item)
    {
        var text = $"{item.Title} {item.Summary}".ToLowerInvariant();
        if (text.Contains("owner", StringComparison.Ordinal))
        {
            return InboxCategory.Owner;
        }

        if (text.Contains("coach", StringComparison.Ordinal) || text.Contains("staff", StringComparison.Ordinal))
        {
            return InboxCategory.Staff;
        }

        if (text.Contains("scout", StringComparison.Ordinal))
        {
            return InboxCategory.Scouting;
        }

        if (text.Contains("recruit", StringComparison.Ordinal))
        {
            return InboxCategory.Recruiting;
        }

        if (text.Contains("draft", StringComparison.Ordinal))
        {
            return InboxCategory.Draft;
        }

        if (text.Contains("injury", StringComparison.Ordinal) || text.Contains("medical", StringComparison.Ordinal))
        {
            return InboxCategory.Medical;
        }

        return InboxCategory.System;
    }

    private static bool CanReply(AlphaInboxItem item) =>
        item.EventType is LegacyEventType.OwnerGoalSet
            or LegacyEventType.ScoutAssigned
            or LegacyEventType.RecruitingOfferSubmitted
            or LegacyEventType.ContractOffered
            or LegacyEventType.Generic;

    private static IReadOnlyList<InboxMessage> Ordered(IEnumerable<InboxMessage> messages, bool includeDeleted) =>
        messages
            .Where(message => includeDeleted || !message.IsDeleted)
            .OrderByDescending(message => message.IsPinned)
            .ThenByDescending(message => message.Item.Date)
            .ThenBy(message => message.InboxItemId, StringComparer.Ordinal)
            .ToArray();
}
