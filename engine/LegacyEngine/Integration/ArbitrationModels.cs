using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum ArbitrationEligibilityStatus
{
    NotEligible,
    Eligible
}

public enum ArbitrationCaseStatus
{
    NotEligible,
    Eligible,
    PlayerFiled,
    TeamFiled,
    HearingScheduled,
    SettledBeforeHearing,
    AwardIssued,
    Accepted,
    WalkedAway,
    Resolved
}

public enum ArbitrationFilingType
{
    PlayerElected,
    TeamElected
}

public enum ArbitrationDecisionType
{
    FileTeamElected,
    PlayerFiled,
    NegotiateSettlement,
    AcceptAward,
    WalkAway
}

public sealed record ArbitrationEligibility(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    int AccruedSeasons,
    ArbitrationEligibilityStatus Status,
    DateOnly? FilingDeadline,
    bool QualifyingOfferIssued,
    string RightsHolderOrganizationId,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(RightsHolderOrganizationId)
            || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Arbitration eligibility requires player, rights holder, and reason.");
        }

        if (AccruedSeasons < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AccruedSeasons), "Accrued seasons cannot be negative.");
        }
    }
}

public sealed record ArbitrationFiling(
    string FilingId,
    string CaseId,
    ArbitrationFilingType FilingType,
    DateOnly FiledOn,
    string FiledBy,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FilingId)
            || string.IsNullOrWhiteSpace(CaseId)
            || string.IsNullOrWhiteSpace(FiledBy)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Arbitration filing requires id, case, filer, and summary.");
        }
    }
}

public sealed record ArbitrationAward(
    string AwardId,
    string PersonId,
    string PlayerName,
    decimal PlayerAsk,
    decimal TeamOffer,
    decimal ProjectedAwardLow,
    decimal ProjectedAwardHigh,
    decimal FinalAward,
    string Currency,
    string Explanation,
    string CapImpact,
    string AgentComment)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AwardId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Currency)
            || string.IsNullOrWhiteSpace(Explanation)
            || string.IsNullOrWhiteSpace(CapImpact)
            || string.IsNullOrWhiteSpace(AgentComment))
        {
            throw new ArgumentException("Arbitration award requires identity, amounts, and explanation.");
        }

        if (PlayerAsk < 0 || TeamOffer < 0 || ProjectedAwardLow < 0 || ProjectedAwardHigh < 0 || FinalAward < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FinalAward), "Arbitration award amounts cannot be negative.");
        }
    }
}

public sealed record ArbitrationCase(
    string CaseId,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    string OrganizationId,
    string OrganizationName,
    ArbitrationCaseStatus Status,
    DateOnly CreatedOn,
    DateOnly? FilingDeadline,
    DateOnly? HearingDate,
    ArbitrationFiling? Filing,
    ArbitrationAward? Award,
    string Recommendation,
    string AgentComment)
{
    public bool IsOpen => Status is ArbitrationCaseStatus.Eligible
        or ArbitrationCaseStatus.PlayerFiled
        or ArbitrationCaseStatus.TeamFiled
        or ArbitrationCaseStatus.HearingScheduled
        or ArbitrationCaseStatus.AwardIssued;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CaseId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(AgentComment))
        {
            throw new ArgumentException("Arbitration case requires identity, status, and explanation.");
        }

        Filing?.Validate();
        Award?.Validate();
    }
}

public sealed record ArbitrationHistoryEntry(
    string HistoryId,
    DateOnly Date,
    string PersonId,
    string PlayerName,
    ArbitrationCaseStatus Status,
    ArbitrationDecisionType DecisionType,
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
            throw new ArgumentException("Arbitration history requires identity, status, and summary.");
        }
    }
}

public sealed record ArbitrationHistory(IReadOnlyList<ArbitrationHistoryEntry> Entries)
{
    public static ArbitrationHistory Empty { get; } = new(Array.Empty<ArbitrationHistoryEntry>());

    public IReadOnlyList<ArbitrationHistoryEntry> ForPlayer(string personId) =>
        Entries
            .Where(entry => entry.PersonId == personId)
            .OrderByDescending(entry => entry.Date)
            .ThenBy(entry => entry.HistoryId, StringComparer.Ordinal)
            .ToArray();

    public ArbitrationHistory Add(ArbitrationHistoryEntry entry)
    {
        entry.Validate();
        return new ArbitrationHistory(Entries
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

public sealed record ArbitrationResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    ArbitrationCase? Case,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Case?.Validate();
        foreach (var item in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(item.InboxItemId)
                || string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.Summary))
            {
                throw new ArgumentException("Arbitration inbox item requires id, title, and summary.");
            }
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Arbitration result requires message.");
        }
    }
}
