using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum BuyoutStatus
{
    NotEligible,
    Eligible,
    PendingConfirmation,
    Completed,
    Blocked,
    ExpiredWindow
}

public enum BuyoutDecisionType
{
    Calculate,
    Confirm,
    Cancel
}

public sealed record BuyoutWindow(
    string WindowId,
    DateOnly OpensOn,
    DateOnly ClosesOn,
    bool IsOpen,
    int DaysUntilClose,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WindowId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Buyout window requires identity and summary.");
        }

        if (ClosesOn < OpensOn)
        {
            throw new ArgumentOutOfRangeException(nameof(ClosesOn), "Buyout window cannot close before it opens.");
        }
    }
}

public sealed record BuyoutEligibility(
    string PersonId,
    string PlayerName,
    string ContractId,
    RosterPosition Position,
    int? Age,
    int YearsRemaining,
    BuyoutStatus Status,
    string Reason,
    string Recommendation,
    BuyoutWindow Window)
{
    public bool IsEligible => Status == BuyoutStatus.Eligible || Status == BuyoutStatus.PendingConfirmation;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(ContractId)
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(Recommendation))
        {
            throw new ArgumentException("Buyout eligibility requires player, contract, and explanation.");
        }

        if (YearsRemaining < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(YearsRemaining), "Buyout years remaining cannot be negative.");
        }

        Window.Validate();
    }
}

public sealed record BuyoutPenalty(
    string PenaltyId,
    int SeasonYear,
    decimal Amount,
    string Currency,
    string Description)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PenaltyId)
            || string.IsNullOrWhiteSpace(Currency)
            || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Buyout penalty requires identity, currency, and description.");
        }

        if (SeasonYear < 1 || Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Buyout penalty season and amount must be non-negative.");
        }
    }
}

public sealed record BuyoutCalculation(
    string CalculationId,
    string PersonId,
    string PlayerName,
    string ContractId,
    decimal RemainingSalary,
    decimal BuyoutCost,
    int PenaltySeasons,
    decimal AnnualPenalty,
    decimal CurrentSeasonCapImpact,
    decimal FutureCapImpact,
    decimal OperatingBudgetImpact,
    IReadOnlyList<BuyoutPenalty> Penalties,
    string Explanation,
    string Warning)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CalculationId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(ContractId)
            || string.IsNullOrWhiteSpace(Explanation)
            || string.IsNullOrWhiteSpace(Warning))
        {
            throw new ArgumentException("Buyout calculation requires identity and readable explanation.");
        }

        if (RemainingSalary < 0 || BuyoutCost < 0 || PenaltySeasons < 0 || AnnualPenalty < 0 || CurrentSeasonCapImpact < 0 || FutureCapImpact < 0 || OperatingBudgetImpact < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BuyoutCost), "Buyout calculation values cannot be negative.");
        }

        foreach (var penalty in Penalties)
        {
            penalty.Validate();
        }
    }
}

public sealed record ContractBuyout(
    string BuyoutId,
    string PersonId,
    string PlayerName,
    string OrganizationId,
    string OrganizationName,
    string ContractId,
    BuyoutStatus Status,
    DateOnly CreatedOn,
    DateOnly? ConfirmedOn,
    BuyoutCalculation Calculation,
    string Recommendation,
    string PlayerAgentReaction,
    string OwnerStaffReaction)
{
    public bool IsPending => Status == BuyoutStatus.PendingConfirmation;

    public bool IsCompleted => Status == BuyoutStatus.Completed;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BuyoutId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(ContractId)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(PlayerAgentReaction)
            || string.IsNullOrWhiteSpace(OwnerStaffReaction))
        {
            throw new ArgumentException("Contract buyout requires identity and explanation.");
        }

        Calculation.Validate();
    }
}

public sealed record BuyoutHistoryEntry(
    string HistoryId,
    DateOnly Date,
    string PersonId,
    string PlayerName,
    BuyoutStatus Status,
    BuyoutDecisionType DecisionType,
    string OrganizationId,
    string OrganizationName,
    decimal BuyoutCost,
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
            throw new ArgumentException("Buyout history requires identity, organization, and summary.");
        }

        if (BuyoutCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BuyoutCost), "Buyout cost cannot be negative.");
        }
    }
}

public sealed record BuyoutHistory(IReadOnlyList<BuyoutHistoryEntry> Entries)
{
    public static BuyoutHistory Empty { get; } = new(Array.Empty<BuyoutHistoryEntry>());

    public IReadOnlyList<BuyoutHistoryEntry> ForPlayer(string personId) =>
        Entries
            .Where(entry => entry.PersonId == personId)
            .OrderByDescending(entry => entry.Date)
            .ThenBy(entry => entry.HistoryId, StringComparer.Ordinal)
            .ToArray();

    public BuyoutHistory Add(BuyoutHistoryEntry entry)
    {
        entry.Validate();
        return new BuyoutHistory(Entries
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

public sealed record BuyoutResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    ContractBuyout? Buyout,
    BuyoutEligibility? Eligibility,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueTransactions,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Buyout?.Validate();
        Eligibility?.Validate();
        foreach (var item in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(item.InboxItemId)
                || string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.Summary))
            {
                throw new ArgumentException("Buyout inbox item requires id, title, and summary.");
            }
        }

        foreach (var transaction in LeagueTransactions)
        {
            transaction.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Buyout result requires message.");
        }
    }
}
