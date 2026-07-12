using LegacyEngine.Contracts;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

/// <summary>Mutually exclusive places a player can occupy within one organization.</summary>
public enum OrganizationRosterGroup
{
    NhlActiveRoster,
    AhlAffiliateRoster,
    OtherContracted,
    UnsignedProspectRights,
    SignedJuniorReturn,
    InjuredOrUnavailable
}

public enum EntryLevelSlideStatus
{
    NotEligible,
    Eligible,
    Tracking,
    SlidePreserved,
    ContractYearActivated,
    SlideUsed,
    SlideLimitReached
}

public sealed record AffiliateAssignment(
    string PersonId,
    string AffiliateOrganizationId,
    string AffiliateTeamName,
    DateOnly AssignedOn,
    bool RequiresWaivers,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(AffiliateOrganizationId)
            || string.IsNullOrWhiteSpace(AffiliateTeamName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Affiliate assignment requires player, affiliate, and summary.");
        }
    }
}

public sealed record EntryLevelSlideEligibility(
    string PersonId,
    bool IsEligible,
    bool IsJuniorReturn,
    int Age,
    int NhlGames,
    int NhlGameThreshold,
    int SlidesUsed,
    int MaximumSlides,
    bool IsContractCountExempt,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || Age < 0 || NhlGames < 0 || NhlGameThreshold < 0
            || SlidesUsed < 0 || MaximumSlides < 0 || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Entry-level slide eligibility is invalid.");
        }
    }
}

public sealed record ContractYearActivationStatus(
    int ContractYearsConsumed,
    int ContractYearsRemaining,
    bool CurrentYearActivated,
    string Summary)
{
    public void Validate()
    {
        if (ContractYearsConsumed < 0 || ContractYearsRemaining < 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Contract year activation status is invalid.");
        }
    }
}

public sealed record ContractSlideHistory(
    string HistoryId,
    string PersonId,
    string ContractId,
    DateOnly EvaluatedOn,
    EntryLevelSlideStatus Status,
    DateOnly PreviousExpiry,
    DateOnly CurrentExpiry,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryId) || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(ContractId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Contract slide history requires identity and summary.");
        }
    }
}

public sealed record OrganizationRosterPlayer(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int Age,
    OrganizationRosterGroup Group,
    string? ContractId,
    string CurrentLevel,
    string? CurrentTeamName,
    bool CountsTowardContractLimit,
    bool IsContractCountExempt,
    string ContractCountReason,
    AffiliateAssignment? AffiliateAssignment = null,
    EntryLevelSlideEligibility? SlideEligibility = null,
    ContractYearActivationStatus? ContractYearStatus = null,
    IReadOnlyList<string>? AssignmentHistory = null)
{
    public IReadOnlyList<string> History => AssignmentHistory ?? Array.Empty<string>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || Age < 0
            || string.IsNullOrWhiteSpace(CurrentLevel) || string.IsNullOrWhiteSpace(ContractCountReason))
        {
            throw new ArgumentException("Organization roster player requires identity, level, age, and contract-count context.");
        }

        AffiliateAssignment?.Validate();
        SlideEligibility?.Validate();
        ContractYearStatus?.Validate();
    }
}

public sealed record OrganizationRoster(string OrganizationId, IReadOnlyList<OrganizationRosterPlayer> Players)
{
    public static OrganizationRoster Empty(string organizationId) => new(organizationId, Array.Empty<OrganizationRosterPlayer>());

    public IReadOnlyList<OrganizationRosterPlayer> In(OrganizationRosterGroup group) =>
        Players.Where(player => player.Group == group).ToArray();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization roster requires organization id.");
        }

        if (Players.GroupBy(player => player.PersonId, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A player cannot appear in more than one organization roster group.");
        }

        foreach (var player in Players)
        {
            player.Validate();
        }
    }
}

public sealed record OrganizationContractInventory(
    int MaximumContracts,
    int ContractsUsed,
    int NhlContracts,
    int AhlContracts,
    int OtherContracted,
    int JuniorReturnsCounting,
    int ExemptJuniorReturns,
    IReadOnlyList<OrganizationRosterPlayer> ContractedPlayers)
{
    public int OpenSlots => MaximumContracts <= 0 ? 0 : Math.Max(0, MaximumContracts - ContractsUsed);

    public void Validate()
    {
        if (MaximumContracts < 0 || ContractsUsed < 0 || NhlContracts < 0 || AhlContracts < 0
            || OtherContracted < 0 || JuniorReturnsCounting < 0 || ExemptJuniorReturns < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumContracts), "Contract inventory counts cannot be negative.");
        }
    }
}

public sealed record RosterAllocationSummary(
    int NhlActiveCount,
    int NhlActiveMaximum,
    int AhlCount,
    int UnsignedRightsCount,
    int JuniorReturnCount,
    OrganizationContractInventory ContractInventory,
    string Summary)
{
    public void Validate()
    {
        if (NhlActiveCount < 0 || NhlActiveMaximum < 0 || AhlCount < 0 || UnsignedRightsCount < 0
            || JuniorReturnCount < 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Roster allocation summary is invalid.");
        }

        ContractInventory.Validate();
    }
}

public sealed record EntryLevelSlideResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    EntryLevelSlideEligibility Eligibility,
    Contract? UpdatedContract,
    ContractSlideHistory? History,
    string Summary)
{
    public bool Slid => History?.Status == EntryLevelSlideStatus.SlidePreserved;
}
