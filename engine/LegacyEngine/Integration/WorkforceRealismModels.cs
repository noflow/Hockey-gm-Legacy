using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum WorkforceCareerStage
{
    Rookie,
    YoungDeveloping,
    EmergingNhlPlayer,
    Prime,
    EstablishedVeteran,
    AgingVeteran,
    LateCareerDepth,
    NearRetirement
}

public enum FreeAgentMarketTier
{
    ImpactFreeAgent,
    NhlRegular,
    RolePlayer,
    VeteranDepth,
    DevelopmentPlayer,
    AhlDepth,
    CampInvite,
    RetirementRisk
}

public enum RetirementRisk
{
    None,
    LateCareer,
    ConsideringRetirement,
    LikelyFinalSeason,
    RetirementRisk,
    ExpectedToRetire
}

public enum LateCareerStatus
{
    NotApplicable,
    ActiveLateCareer,
    FinalContractCandidate,
    RetirementWatch
}

public enum WorkforceValidationSeverity
{
    Information,
    Warning,
    Invalid
}

public sealed record PlayerAgeDistributionProfile(
    int Age18To21,
    int Age22To24,
    int Age25To29,
    int Age30To33,
    int Age34To36,
    int Age37Plus)
{
    public int Total => Age18To21 + Age22To24 + Age25To29 + Age30To33 + Age34To36 + Age37Plus;

    public void Validate()
    {
        if (Age18To21 < 0 || Age22To24 < 0 || Age25To29 < 0 || Age30To33 < 0 || Age34To36 < 0 || Age37Plus < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age18To21), "Age distribution counts cannot be negative.");
        }
    }
}

public sealed record CareerStageDistributionProfile(IReadOnlyDictionary<WorkforceCareerStage, int> Counts)
{
    public int Total => Counts.Values.Sum();

    public void Validate()
    {
        if (Counts.Count == 0 || Counts.Any(item => item.Value < 0))
        {
            throw new ArgumentException("Career-stage distribution requires non-negative counts.");
        }
    }
}

public sealed record FinalContractPreference(
    bool PrefersOneYearTerm,
    bool RequiresNhlOpportunity,
    bool PrefersContender,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Final-contract preference requires a summary.");
        }
    }
}

public sealed record RetirementConsideration(
    string PersonId,
    string PlayerName,
    int Age,
    RosterPosition Position,
    RetirementRisk Risk,
    LateCareerStatus Status,
    FinalContractPreference Preference,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || Age < 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Retirement consideration requires player identity and context.");
        }

        Preference.Validate();
    }
}

public sealed record WorkforcePlayerRecord(
    string PersonId,
    string PlayerName,
    string OrganizationId,
    string OrganizationName,
    RosterPosition Position,
    int Age,
    WorkforceCareerStage CareerStage,
    int EstimatedOverall,
    int RemainingPotential,
    int ContractYearsRemaining,
    DateOnly ContractExpiryDate,
    FreeAgentRightsStatus ProjectedRightsStatus,
    decimal EstimatedCapHit,
    RetirementRisk RetirementRisk,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(OrganizationName)
            || Position == RosterPosition.Unknown || Age < 18 || EstimatedOverall is < 0 or > 100
            || RemainingPotential < EstimatedOverall || RemainingPotential > 100 || ContractYearsRemaining < 0
            || EstimatedCapHit < 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Workforce player record is invalid.");
        }
    }
}

public sealed record TeamWorkforceProfile(
    string OrganizationId,
    string OrganizationName,
    string Strategy,
    PlayerAgeDistributionProfile AgeDistribution,
    CareerStageDistributionProfile CareerStageDistribution,
    IReadOnlyList<WorkforcePlayerRecord> Players,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Strategy) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Team workforce profile requires identity, strategy, and summary.");
        }

        AgeDistribution.Validate();
        CareerStageDistribution.Validate();
        foreach (var player in Players)
        {
            player.Validate();
        }
    }
}

public sealed record LeagueWorkforceProfile(
    string LeagueId,
    int SeasonYear,
    PlayerAgeDistributionProfile AgeDistribution,
    CareerStageDistributionProfile CareerStageDistribution,
    IReadOnlyList<TeamWorkforceProfile> Teams,
    IReadOnlyList<WorkforcePlayerRecord> LeaguePlayers,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeagueId) || SeasonYear < 1 || Teams.Count == 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("League workforce profile requires a league, season, teams, and summary.");
        }

        AgeDistribution.Validate();
        CareerStageDistribution.Validate();
        foreach (var team in Teams)
        {
            team.Validate();
        }
    }
}

public sealed record WorkforceValidationIssue(WorkforceValidationSeverity Severity, string Code, string Message)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Workforce validation issue requires a code and message.");
        }
    }
}

public sealed record WorkforceValidationResult(bool IsValid, IReadOnlyList<WorkforceValidationIssue> Issues, string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Workforce validation result requires a summary.");
        }

        foreach (var issue in Issues)
        {
            issue.Validate();
        }
    }
}
