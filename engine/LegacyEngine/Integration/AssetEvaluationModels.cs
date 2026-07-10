using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum AssetValueBand
{
    Negative,
    Low,
    Moderate,
    Strong,
    Premium,
    Elite
}

public enum AssetEvaluationType
{
    Player,
    Prospect,
    DraftPick,
    Contract
}

public enum PositionMarketPosition
{
    C,
    LW,
    RW,
    LD,
    RD,
    G
}

public enum PositionScarcityLevel
{
    Oversupplied,
    Normal,
    Thin,
    Scarce,
    Critical
}

public sealed record PositionMarketContext(
    PositionMarketPosition Position,
    PositionScarcityLevel ScarcityLevel,
    int CurrentLeagueDepth,
    int AvailableFreeAgents,
    int DraftClassDepth,
    int InjuryDrag,
    int TradeBlockSupply,
    int ProspectPipelineDepth,
    int ScarcityScore,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Position market context requires a summary.", nameof(Summary));
        }

        if (ScarcityScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ScarcityScore), "Scarcity score must stay between 0 and 100.");
        }
    }
}

public sealed record PositionScarcityProfile(
    string LeagueId,
    int SeasonYear,
    IReadOnlyList<PositionMarketContext> Positions,
    string Summary)
{
    public PositionMarketContext For(PositionMarketPosition position) =>
        Positions.FirstOrDefault(item => item.Position == position)
        ?? throw new ArgumentException("Position market context was not found.", nameof(position));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeagueId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Position scarcity profile requires league identity and summary.");
        }

        if (Positions.Count != Enum.GetValues<PositionMarketPosition>().Length)
        {
            throw new ArgumentException("Position scarcity profile must include every tracked position.");
        }

        foreach (var position in Positions)
        {
            position.Validate();
        }
    }
}

public sealed record CurrentValue(int Score, AssetValueBand Band, string Summary)
{
    public void Validate() => ValidateScore(Score, Summary, nameof(CurrentValue));

    internal static void ValidateScore(int score, string summary, string name)
    {
        if (score is < -20 or > 120)
        {
            throw new ArgumentOutOfRangeException(name, "Asset value scores must stay in the comparison range.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Asset value requires a readable summary.", name);
        }
    }
}

public sealed record FutureValue(int Score, AssetValueBand Band, string Summary)
{
    public void Validate() => CurrentValue.ValidateScore(Score, Summary, nameof(FutureValue));
}

public sealed record ContractValue(int Score, AssetValueBand Band, string Summary)
{
    public void Validate() => CurrentValue.ValidateScore(Score, Summary, nameof(ContractValue));
}

public sealed record TradeValue(int Score, AssetValueBand Band, string Summary)
{
    public void Validate() => CurrentValue.ValidateScore(Score, Summary, nameof(TradeValue));
}

public sealed record OrganizationalValue(int Score, AssetValueBand Band, string OrganizationId, string OrganizationName, string Summary)
{
    public void Validate()
    {
        CurrentValue.ValidateScore(Score, Summary, nameof(OrganizationalValue));
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(OrganizationName))
        {
            throw new ArgumentException("Organizational value requires organization identity.");
        }
    }
}

public sealed record PlayerMarketValue(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    PositionMarketPosition MarketPosition,
    PositionScarcityLevel ScarcityLevel,
    int MarketDemandScore,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player market value requires player identity and summary.");
        }

        if (MarketDemandScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MarketDemandScore), "Market demand score must stay between 0 and 100.");
        }
    }
}

public sealed record PlayerAssetValue(
    string PersonId,
    string PlayerName,
    AssetEvaluationType EvaluationType,
    CurrentValue Current,
    FutureValue Future,
    ContractValue Contract,
    TradeValue Trade,
    OrganizationalValue Organizational,
    PlayerMarketValue Market,
    IReadOnlyList<string> Reasons)
{
    public string DisplaySummary => $"{PlayerName}: current {Current.Band}, future {Future.Band}, trade {Trade.Band}, market {Market.ScarcityLevel}.";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || Reasons.Count == 0)
        {
            throw new ArgumentException("Player asset value requires player identity and reasons.");
        }

        Current.Validate();
        Future.Validate();
        Contract.Validate();
        Trade.Validate();
        Organizational.Validate();
        Market.Validate();
    }
}

public sealed record DraftPickValue(
    string PickId,
    string DisplayName,
    int Year,
    int Round,
    string OriginalOwner,
    string CurrentOwner,
    int AssetScore,
    AssetValueBand Band,
    string FutureProjection,
    string RiskSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PickId)
            || string.IsNullOrWhiteSpace(DisplayName)
            || string.IsNullOrWhiteSpace(OriginalOwner)
            || string.IsNullOrWhiteSpace(CurrentOwner)
            || string.IsNullOrWhiteSpace(FutureProjection)
            || string.IsNullOrWhiteSpace(RiskSummary))
        {
            throw new ArgumentException("Draft pick value requires readable pick context.");
        }

        if (Year <= 0 || Round <= 0 || AssetScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(AssetScore), "Draft pick value fields are outside the supported range.");
        }
    }
}

public sealed record AssetEvaluation(
    string AssetId,
    string DisplayName,
    AssetEvaluationType EvaluationType,
    int CompositeScore,
    AssetValueBand Band,
    string ContextSummary,
    IReadOnlyList<string> Reasons)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AssetId)
            || string.IsNullOrWhiteSpace(DisplayName)
            || string.IsNullOrWhiteSpace(ContextSummary)
            || Reasons.Count == 0)
        {
            throw new ArgumentException("Asset evaluation requires asset identity, context, and reasons.");
        }

        if (CompositeScore is < -20 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(CompositeScore), "Asset evaluation score must stay in the comparison range.");
        }
    }
}
