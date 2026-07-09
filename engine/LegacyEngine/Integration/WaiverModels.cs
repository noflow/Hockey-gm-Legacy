using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum WaiverStatus
{
    WaiverExempt,
    RequiresWaivers,
    OnWaivers,
    Claimed,
    Cleared,
    Assigned,
    Recalled
}

public enum WaiverTransactionType
{
    Placement,
    Claim,
    Clear,
    Assignment,
    Recall,
    Cancelled
}

public sealed record WaiverEligibility(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    WaiverStatus Status,
    bool WaiversEnabled,
    bool IsWaiverExempt,
    bool RequiresWaivers,
    bool CanAssignToAffiliate,
    bool CanRecallFromAffiliate,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Waiver eligibility requires player identity and explanation.");
        }
    }
}

public sealed record WaiverTransaction(
    string TransactionId,
    WaiverTransactionType TransactionType,
    WaiverStatus Status,
    DateOnly Date,
    DateTimeOffset? ClaimDeadline,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    string OriginOrganizationId,
    string OriginTeamName,
    string? DestinationOrganizationId,
    string? DestinationTeamName,
    string Reason,
    bool IsOpen = true)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TransactionId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(OriginOrganizationId)
            || string.IsNullOrWhiteSpace(OriginTeamName)
            || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Waiver transaction requires identity, team, player, and reason.");
        }
    }
}

public sealed record WaiverClaim(
    string ClaimId,
    string TransactionId,
    string PersonId,
    string PlayerName,
    string ClaimingOrganizationId,
    string ClaimingTeamName,
    int PriorityRank,
    DateOnly ClaimDate,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClaimId)
            || string.IsNullOrWhiteSpace(TransactionId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(ClaimingOrganizationId)
            || string.IsNullOrWhiteSpace(ClaimingTeamName)
            || string.IsNullOrWhiteSpace(Reason)
            || PriorityRank < 1)
        {
            throw new ArgumentException("Waiver claim requires identity, claimant, priority, and reason.");
        }
    }
}

public sealed record WaiverPriority(string OrganizationId, string TeamName, int Rank)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName) || Rank < 1)
        {
            throw new ArgumentException("Waiver priority requires organization, team, and positive rank.");
        }
    }
}

public sealed record WaiverHistoryEntry(
    string HistoryId,
    DateOnly Date,
    string PersonId,
    string PlayerName,
    WaiverStatus Status,
    string OrganizationId,
    string TeamName,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Waiver history entry requires identity, team, player, and summary.");
        }
    }
}

public sealed record WaiverHistory(IReadOnlyList<WaiverHistoryEntry> Entries)
{
    public static WaiverHistory Empty { get; } = new(Array.Empty<WaiverHistoryEntry>());

    public IReadOnlyList<WaiverHistoryEntry> ForPlayer(string personId) =>
        Entries
            .Where(entry => string.Equals(entry.PersonId, personId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.Date)
            .ThenBy(entry => entry.HistoryId, StringComparer.Ordinal)
            .ToArray();

    public WaiverHistory Add(WaiverHistoryEntry entry)
    {
        entry.Validate();
        return new WaiverHistory(Entries
            .Where(existing => existing.HistoryId != entry.HistoryId)
            .Append(entry)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.HistoryId, StringComparer.Ordinal)
            .ToArray());
    }

    public void Validate()
    {
        foreach (var entry in Entries)
        {
            entry.Validate();
        }
    }
}

public sealed record WaiverWire(
    IReadOnlyList<WaiverTransaction> Transactions,
    IReadOnlyList<WaiverClaim> Claims,
    IReadOnlyList<WaiverPriority> Priority)
{
    public static WaiverWire Empty { get; } = new(Array.Empty<WaiverTransaction>(), Array.Empty<WaiverClaim>(), Array.Empty<WaiverPriority>());

    public IReadOnlyList<WaiverTransaction> OpenTransactions =>
        Transactions
            .Where(transaction => transaction.IsOpen && transaction.Status == WaiverStatus.OnWaivers)
            .OrderBy(transaction => transaction.ClaimDeadline)
            .ThenBy(transaction => transaction.PlayerName, StringComparer.Ordinal)
            .ToArray();

    public void Validate()
    {
        foreach (var transaction in Transactions)
        {
            transaction.Validate();
        }

        foreach (var claim in Claims)
        {
            claim.Validate();
        }

        foreach (var priority in Priority)
        {
            priority.Validate();
        }
    }
}

public sealed record WaiverResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    WaiverEligibility? Eligibility,
    WaiverTransaction? Transaction,
    WaiverClaim? Claim,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Eligibility?.Validate();
        Transaction?.Validate();
        Claim?.Validate();
        foreach (var inbox in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(inbox.InboxItemId)
                || string.IsNullOrWhiteSpace(inbox.Title)
                || string.IsNullOrWhiteSpace(inbox.Summary))
            {
                throw new ArgumentException("Waiver inbox item requires id, title, and summary.");
            }
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Waiver result requires a message.");
        }
    }
}
