namespace LegacyEngine.Integration;

public sealed record InboxFilter(
    InboxCategory Category = InboxCategory.All,
    bool UnreadOnly = false,
    bool ImportantOnly = false,
    bool IncludeArchived = false,
    bool IncludeDeleted = false);
