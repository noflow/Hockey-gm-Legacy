using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public enum OrganizationAiPersonality
{
    Unknown,
    AggressiveTrader,
    Conservative,
    DraftAndDevelop,
    ProspectHoarder,
    VeteranBuilder,
    BudgetConscious,
    BigSpender,
    WinNow,
    PatientRebuilder,
    RiskTaker,
    DefenseFirst,
    SkillFirst,
    GoalieFocused
}

public enum OrganizationStrategyPhase
{
    Unknown,
    Rebuilding,
    Retooling,
    Developing,
    Competing,
    Contending,
    AllIn,
    BudgetReset
}

public enum TeamNeedType
{
    StartingGoalie,
    BackupGoalie,
    TopPairDefense,
    DefensiveDefenseman,
    TopSixForward,
    Scoring,
    Physicality,
    Prospects,
    DraftPicks,
    BudgetRelief,
    VeteranLeadership,
    StaffUpgrade
}

public enum AiAssetType
{
    RosterPlayer,
    ProspectRights,
    DraftPick,
    FutureConsideration,
    BudgetRelief,
    VeteranHelpNow,
    YoungUpside,
    StaffCandidate,
    FreeAgentContract,
    Goalie,
    Defenseman,
    ScoringForward
}

public enum AiDecisionCategory
{
    Draft,
    Trade,
    FreeAgency,
    StaffHiring,
    Strategy
}

public enum AiDecisionOutcome
{
    NotInterested,
    Reject,
    Wait,
    Counter,
    Accept,
    VeryInterested
}

public sealed record TeamNeedProfile(
    TeamNeedType NeedType,
    TradePriority Priority,
    string Reason,
    string Urgency,
    AiAssetType SuggestedAssetType)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(Urgency))
        {
            throw new ArgumentException("Team need profile requires reason and urgency.");
        }
    }
}

public sealed record OrganizationStrategy(
    OrganizationStrategyPhase Phase,
    string Summary,
    string DraftPhilosophy,
    string TradeBehavior,
    string FreeAgencyBehavior,
    string BudgetBehavior,
    string ScoutingBehavior,
    string StaffBehavior,
    int RiskTolerance)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(DraftPhilosophy)
            || string.IsNullOrWhiteSpace(TradeBehavior)
            || string.IsNullOrWhiteSpace(FreeAgencyBehavior)
            || string.IsNullOrWhiteSpace(BudgetBehavior)
            || string.IsNullOrWhiteSpace(ScoutingBehavior)
            || string.IsNullOrWhiteSpace(StaffBehavior))
        {
            throw new ArgumentException("Organization strategy requires readable behavior summaries.");
        }

        if (RiskTolerance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(RiskTolerance), "Risk tolerance must be between 0 and 100.");
        }
    }
}

public sealed record OrganizationStrategyChange(
    DateOnly Date,
    OrganizationStrategyPhase FromPhase,
    OrganizationStrategyPhase ToPhase,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Strategy change requires a reason.", nameof(Reason));
        }
    }
}

public sealed record OrganizationAiProfile(
    string OrganizationId,
    string TeamName,
    OrganizationAiPersonality Personality,
    OrganizationStrategy Strategy,
    IReadOnlyList<TeamNeedProfile> CurrentNeeds,
    IReadOnlyList<OrganizationStrategyChange> StrategyHistory,
    DateOnly LastUpdated,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Organization AI profile requires organization identity and summary.");
        }

        Strategy.Validate();

        if (CurrentNeeds.Count == 0)
        {
            throw new ArgumentException("Organization AI profile requires current needs.", nameof(CurrentNeeds));
        }

        foreach (var need in CurrentNeeds)
        {
            need.Validate();
        }

        foreach (var change in StrategyHistory)
        {
            change.Validate();
        }
    }
}

public sealed record AiDecisionContext(
    AiDecisionCategory Category,
    IReadOnlyList<AiAssetType>? IncomingAssets = null,
    decimal BudgetImpact = 0m,
    RosterPosition? Position = null,
    int Age = 18,
    DraftClassTheme? DraftClassTheme = null,
    StaffRole? StaffRole = null,
    bool IsHighRisk = false,
    string Notes = "")
{
    public IReadOnlyList<AiAssetType> Assets => IncomingAssets ?? Array.Empty<AiAssetType>();

    public void Validate()
    {
        if (Age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Decision context age cannot be negative.");
        }

        if (Notes is null)
        {
            throw new ArgumentException("Decision context notes cannot be null.", nameof(Notes));
        }
    }
}

public sealed record AiDecisionResult(
    AiDecisionOutcome Outcome,
    int Score,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<AiAssetType> PreferredAssets,
    string Explanation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("AI decision result requires an explanation.", nameof(Explanation));
        }

        if (Reasons.Count == 0 || Reasons.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("AI decision result requires readable reasons.", nameof(Reasons));
        }
    }
}
