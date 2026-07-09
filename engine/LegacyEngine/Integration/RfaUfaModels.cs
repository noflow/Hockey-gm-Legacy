using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum FreeAgentRightsStatus
{
    UnderContract,
    PendingRfa,
    RestrictedFreeAgent,
    PendingUfa,
    UnrestrictedFreeAgent,
    Qualified,
    NotQualified,
    RightsHeld,
    RightsReleased,
    SignedElsewhere
}

public enum ContractRightsStatus
{
    UnderContract,
    PendingRfa,
    RestrictedFreeAgent,
    PendingUfa,
    UnrestrictedFreeAgent,
    Qualified,
    NotQualified,
    RightsHeld,
    RightsReleased,
    SignedElsewhere
}

public enum PlayerRightsDecisionType
{
    Classify,
    Qualify,
    DoNotQualify,
    ReleaseRights,
    NegotiateContract,
    SignedElsewhere
}

public sealed record RightsExpiryRule(string RuleId, string Description, DateOnly Deadline)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RuleId) || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Rights expiry rule requires id and description.");
        }
    }
}

public sealed record QualifyingOffer(
    string QualifyingOfferId,
    string PersonId,
    string PlayerName,
    decimal RequiredSalary,
    string Currency,
    DateOnly Deadline,
    string RightsImpact,
    string CapBudgetImpact,
    string AgentReaction,
    bool IsIssued,
    DateOnly? IssuedOn = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(QualifyingOfferId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Currency)
            || string.IsNullOrWhiteSpace(RightsImpact)
            || string.IsNullOrWhiteSpace(CapBudgetImpact)
            || string.IsNullOrWhiteSpace(AgentReaction))
        {
            throw new ArgumentException("Qualifying offer requires identity and readable context.");
        }

        if (RequiredSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredSalary), "Qualifying offer salary cannot be negative.");
        }
    }
}

public sealed record PlayerRightsDecision(
    string DecisionId,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    int AccruedSeasons,
    string OrganizationId,
    string OrganizationName,
    string? ContractId,
    DateOnly? ContractExpiryDate,
    FreeAgentRightsStatus RightsStatus,
    ContractRightsStatus ContractRightsStatus,
    bool QualifyingOfferRequired,
    QualifyingOffer? QualifyingOffer,
    RightsExpiryRule? ExpiryRule,
    string RightsHolderOrganizationId,
    string RightsHolderTeamName,
    string Recommendation,
    string AgentNote,
    string Reason,
    DateOnly CreatedOn,
    DateOnly LastUpdatedOn)
{
    public bool IsOpenDecision => RightsStatus is FreeAgentRightsStatus.PendingRfa or FreeAgentRightsStatus.PendingUfa or FreeAgentRightsStatus.RestrictedFreeAgent;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DecisionId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(RightsHolderOrganizationId)
            || string.IsNullOrWhiteSpace(RightsHolderTeamName)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(AgentNote)
            || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Player rights decision requires identity, status, and explanation.");
        }

        if (AccruedSeasons < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AccruedSeasons), "Accrued seasons cannot be negative.");
        }

        QualifyingOffer?.Validate();
        ExpiryRule?.Validate();
    }
}

public sealed record RightsHistoryEntry(
    string HistoryId,
    DateOnly Date,
    string PersonId,
    string PlayerName,
    FreeAgentRightsStatus Status,
    PlayerRightsDecisionType DecisionType,
    string OrganizationId,
    string OrganizationName,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Rights history entry requires identity, status, and summary.");
        }
    }
}

public sealed record RightsHistory(IReadOnlyList<RightsHistoryEntry> Entries)
{
    public static RightsHistory Empty { get; } = new(Array.Empty<RightsHistoryEntry>());

    public IReadOnlyList<RightsHistoryEntry> ForPlayer(string personId) =>
        Entries
            .Where(entry => entry.PersonId == personId)
            .OrderByDescending(entry => entry.Date)
            .ThenBy(entry => entry.HistoryId, StringComparer.Ordinal)
            .ToArray();

    public RightsHistory Add(RightsHistoryEntry entry)
    {
        entry.Validate();
        return new RightsHistory(Entries
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

public sealed record RightsDecisionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    PlayerRightsDecision? Decision,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Decision?.Validate();
        foreach (var inbox in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(inbox.InboxItemId)
                || string.IsNullOrWhiteSpace(inbox.Title)
                || string.IsNullOrWhiteSpace(inbox.Summary))
            {
                throw new ArgumentException("Rights inbox item requires id, title, and summary.");
            }
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Rights decision result requires a message.");
        }
    }
}
