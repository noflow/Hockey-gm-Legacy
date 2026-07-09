using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum OfferSheetStatus
{
    NotEligible,
    Eligible,
    Submitted,
    AcceptedByPlayer,
    MatchedByTeam,
    DeclinedByPlayer,
    CompensationRequired,
    Completed,
    Expired,
    Blocked
}

public enum OfferSheetDecision
{
    BuildOffer,
    SubmitOffer,
    Withdraw,
    PlayerAccepted,
    MatchOffer,
    DeclineAndTakeCompensation
}

public sealed record OfferSheetEligibility(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int? Age,
    string RightsHolderOrganizationId,
    string RightsHolderTeamName,
    string OfferingOrganizationId,
    string OfferingTeamName,
    OfferSheetStatus Status,
    string Reason,
    string Recommendation,
    string AgentInterest)
{
    public bool IsEligible => Status == OfferSheetStatus.Eligible;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(RightsHolderOrganizationId)
            || string.IsNullOrWhiteSpace(RightsHolderTeamName)
            || string.IsNullOrWhiteSpace(OfferingOrganizationId)
            || string.IsNullOrWhiteSpace(OfferingTeamName)
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(AgentInterest))
        {
            throw new ArgumentException("Offer sheet eligibility requires identity and readable context.");
        }
    }
}

public sealed record OfferSheetCompensation(
    decimal Aav,
    int TermYears,
    IReadOnlyList<int> RequiredRounds,
    IReadOnlyList<TradeAsset> RequiredPicks,
    IReadOnlyList<int> MissingRounds,
    bool HasRequiredPicks,
    decimal CapImpact,
    string Summary)
{
    public void Validate()
    {
        if (Aav < 0 || TermYears <= 0 || CapImpact < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Aav), "Offer sheet compensation values must be non-negative and term must be positive.");
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Offer sheet compensation requires summary.");
        }

        foreach (var pick in RequiredPicks)
        {
            pick.Validate();
        }
    }
}

public sealed record OfferSheet(
    string OfferSheetId,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    string RightsHolderOrganizationId,
    string RightsHolderTeamName,
    string OfferingOrganizationId,
    string OfferingTeamName,
    OfferSheetStatus Status,
    DateOnly SubmittedOn,
    DateOnly ResponseDeadline,
    decimal AnnualSalary,
    int TermYears,
    string Currency,
    OfferSheetCompensation Compensation,
    string AgentComment,
    string RightsHolderRecommendation,
    DateOnly? ResolvedOn = null)
{
    public bool IsActive => Status is OfferSheetStatus.Submitted or OfferSheetStatus.AcceptedByPlayer or OfferSheetStatus.CompensationRequired;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OfferSheetId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(RightsHolderOrganizationId)
            || string.IsNullOrWhiteSpace(RightsHolderTeamName)
            || string.IsNullOrWhiteSpace(OfferingOrganizationId)
            || string.IsNullOrWhiteSpace(OfferingTeamName)
            || string.IsNullOrWhiteSpace(Currency)
            || string.IsNullOrWhiteSpace(AgentComment)
            || string.IsNullOrWhiteSpace(RightsHolderRecommendation))
        {
            throw new ArgumentException("Offer sheet requires identity, money, and explanation.");
        }

        if (AnnualSalary < 0 || TermYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AnnualSalary), "Offer sheet money and term must be valid.");
        }

        Compensation.Validate();
    }
}

public sealed record OfferSheetHistoryEntry(
    string HistoryId,
    DateOnly Date,
    string PersonId,
    string PlayerName,
    OfferSheetStatus Status,
    OfferSheetDecision Decision,
    string RightsHolderOrganizationId,
    string RightsHolderTeamName,
    string OfferingOrganizationId,
    string OfferingTeamName,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(RightsHolderOrganizationId)
            || string.IsNullOrWhiteSpace(RightsHolderTeamName)
            || string.IsNullOrWhiteSpace(OfferingOrganizationId)
            || string.IsNullOrWhiteSpace(OfferingTeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Offer sheet history requires identity, teams, and summary.");
        }
    }
}

public sealed record OfferSheetHistory(IReadOnlyList<OfferSheetHistoryEntry> Entries)
{
    public static OfferSheetHistory Empty { get; } = new(Array.Empty<OfferSheetHistoryEntry>());

    public IReadOnlyList<OfferSheetHistoryEntry> ForPlayer(string personId) =>
        Entries
            .Where(entry => entry.PersonId == personId)
            .OrderByDescending(entry => entry.Date)
            .ThenBy(entry => entry.HistoryId, StringComparer.Ordinal)
            .ToArray();

    public OfferSheetHistory Add(OfferSheetHistoryEntry entry)
    {
        entry.Validate();
        return new OfferSheetHistory(Entries
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

public sealed record OfferSheetResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    OfferSheet? OfferSheet,
    OfferSheetEligibility? Eligibility,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        OfferSheet?.Validate();
        Eligibility?.Validate();

        foreach (var item in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(item.InboxItemId)
                || string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.Summary))
            {
                throw new ArgumentException("Offer sheet inbox item requires id, title, and summary.");
            }
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Offer sheet result requires message.");
        }
    }
}
