using LegacyEngine.Contracts;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum ContractNegotiationStatus
{
    NotStarted,
    InitialInterest,
    OfferSubmitted,
    AgentReviewing,
    Countered,
    AcceptedInPrinciple,
    Rejected,
    Stalled,
    Withdrawn,
    Signed,
    RightsDecisionRequired,
    ArbitrationPending,
    EnteredFreeAgency
}

public enum ContractOfferResponse
{
    Accepted,
    Rejected,
    Countered,
    Waiting
}

public enum ContractMarketStatus
{
    UnderContract,
    Expiring,
    ProjectedRfa,
    ProjectedUfa,
    Rfa,
    Ufa,
    FreeAgent,
    Negotiating,
    Signed
}

public enum ContractDecisionDeadlineType
{
    Extension,
    QualifyingOffer,
    ArbitrationFiling,
    Hearing,
    OfferExpiration,
    FreeAgency
}

public sealed record ContractDemand(
    string PersonId,
    string PersonName,
    decimal AnnualSalary,
    int TermYears,
    string DesiredRole,
    string Priorities,
    string AgentComment,
    DateOnly RequestedOn)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(DesiredRole)
            || string.IsNullOrWhiteSpace(Priorities)
            || string.IsNullOrWhiteSpace(AgentComment)
            || AnnualSalary < 0
            || TermYears <= 0)
        {
            throw new ArgumentException("Contract demand requires player, role, priorities, salary, and term.");
        }
    }
}

public sealed record ContractDecisionDeadline(
    string DeadlineId,
    string PersonId,
    string PersonName,
    ContractDecisionDeadlineType Type,
    DateOnly DueOn,
    string Consequence,
    bool IsActionable)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DeadlineId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(Consequence))
        {
            throw new ArgumentException("Contract deadline requires identity and consequence.");
        }
    }
}

public sealed record ContractComparable(
    string ComparableId,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    decimal AnnualSalary,
    int TermYears,
    string Role,
    string Source,
    string Context)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ComparableId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Role)
            || string.IsNullOrWhiteSpace(Source)
            || string.IsNullOrWhiteSpace(Context)
            || AnnualSalary < 0
            || TermYears <= 0)
        {
            throw new ArgumentException("Contract comparable requires player, role, source, and valid terms.");
        }
    }
}

public sealed record ContractNegotiationHistoryEntry(
    string HistoryId,
    DateOnly Date,
    string PersonId,
    string PersonName,
    ContractNegotiationStatus Status,
    ContractOfferResponse? Response,
    int Round,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(Summary)
            || Round < 0)
        {
            throw new ArgumentException("Contract negotiation history requires identity, status, round, and summary.");
        }
    }
}

public sealed record ContractNegotiationHistory(IReadOnlyList<ContractNegotiationHistoryEntry> Entries)
{
    public static ContractNegotiationHistory Empty { get; } = new(Array.Empty<ContractNegotiationHistoryEntry>());

    public IReadOnlyList<ContractNegotiationHistoryEntry> ForPlayer(string personId) =>
        Entries.Where(entry => entry.PersonId == personId)
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.HistoryId, StringComparer.Ordinal)
            .ToArray();

    public ContractNegotiationHistory Add(ContractNegotiationHistoryEntry entry)
    {
        entry.Validate();
        return new ContractNegotiationHistory(Entries
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

public sealed record ContractNegotiation(
    string NegotiationId,
    string PersonId,
    string PersonName,
    string OrganizationId,
    ContractAskType AskType,
    ContractNegotiationStatus Status,
    ContractMarketStatus MarketStatus,
    ContractDemand Demand,
    ContractOffer? CurrentOffer,
    ContractOfferEvaluation? LastEvaluation,
    DateOnly StartedOn,
    DateOnly LastUpdatedOn,
    DateOnly? DecisionDeadline,
    int Round,
    string LastResponse,
    string NextAction)
{
    public bool IsOpen => Status is ContractNegotiationStatus.InitialInterest
        or ContractNegotiationStatus.OfferSubmitted
        or ContractNegotiationStatus.AgentReviewing
        or ContractNegotiationStatus.Countered
        or ContractNegotiationStatus.AcceptedInPrinciple
        or ContractNegotiationStatus.RightsDecisionRequired
        or ContractNegotiationStatus.ArbitrationPending;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(NegotiationId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(LastResponse)
            || string.IsNullOrWhiteSpace(NextAction)
            || Round < 0)
        {
            throw new ArgumentException("Contract negotiation requires identity, status, response, and next action.");
        }

        Demand.Validate();
        CurrentOffer?.Validate();
        LastEvaluation?.Validate();
    }
}

public sealed record ContractMarketSummary(
    IReadOnlyList<ContractNegotiation> Negotiations,
    IReadOnlyList<Contract> ExpiringContracts,
    IReadOnlyList<PlayerRightsDecision> RightsDecisions,
    IReadOnlyList<FreeAgent> FreeAgents,
    IReadOnlyList<ContractDecisionDeadline> Deadlines,
    OrganizationPlan? Planning,
    string Summary)
{
    public int OpenNegotiations => Negotiations.Count(item => item.IsOpen);

    public void Validate()
    {
        foreach (var negotiation in Negotiations)
        {
            negotiation.Validate();
        }

        foreach (var contract in ExpiringContracts)
        {
            contract.Validate();
        }

        foreach (var decision in RightsDecisions)
        {
            decision.Validate();
        }

        foreach (var agent in FreeAgents)
        {
            agent.Validate();
        }

        foreach (var deadline in Deadlines)
        {
            deadline.Validate();
        }

        Planning?.Validate();
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Contract market summary requires readable summary text.");
        }
    }
}

public sealed record ContractMarketResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    ContractNegotiation? Negotiation,
    ContractOfferEvaluation? Evaluation,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Negotiation?.Validate();
        Evaluation?.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Contract market result requires a message.");
        }
    }
}
