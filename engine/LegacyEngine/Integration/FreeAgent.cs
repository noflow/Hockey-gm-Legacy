using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed record FreeAgent(
    string PersonId,
    string Name,
    RosterPosition Position,
    string ShootsCatches,
    int Age,
    int HeightInches,
    int WeightPounds,
    string Nationality,
    string Hometown,
    string PreviousTeam,
    PriorSeasonStatLine LastSeasonStats,
    CareerStatSummary CareerStats,
    string InjuryRisk,
    string DevelopmentTrend,
    string PlayerType,
    string ProjectedLineupRole,
    FreeAgentContractAsk ContractAsk,
    FreeAgentInterest Interest,
    string RightsEligibilityNotes,
    ScoutingConfidenceLevel ScoutingConfidence,
    FreeAgentFitSummary FitSummary,
    FreeAgentStatus Status,
    bool IsShortlisted)
{
    public FreeAgentMarketTier MarketTier { get; init; } = FreeAgentMarketTier.RolePlayer;

    public WorkforceCareerStage CareerStage { get; init; } = WorkforceCareerStage.YoungDeveloping;

    public RetirementRisk RetirementRisk { get; init; } = RetirementRisk.None;

    public FinalContractPreference? FinalContractPreference { get; init; }

    public string HeightDisplay => $"{HeightInches / 12}'{HeightInches % 12}\"";

    public string WeightDisplay => $"{WeightPounds} lbs";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(ShootsCatches)
            || string.IsNullOrWhiteSpace(Nationality)
            || string.IsNullOrWhiteSpace(Hometown)
            || string.IsNullOrWhiteSpace(PreviousTeam)
            || string.IsNullOrWhiteSpace(InjuryRisk)
            || string.IsNullOrWhiteSpace(DevelopmentTrend)
            || string.IsNullOrWhiteSpace(PlayerType)
            || string.IsNullOrWhiteSpace(ProjectedLineupRole)
            || string.IsNullOrWhiteSpace(RightsEligibilityNotes))
        {
            throw new ArgumentException("Free agent profile requires readable identity, bio, and summary fields.");
        }

        if (Position == RosterPosition.Unknown)
        {
            throw new ArgumentException("Free agent must have a known public position.", nameof(Position));
        }

        if (Age <= 0 || HeightInches is < 60 or > 84 || WeightPounds is < 130 or > 280)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Free agent bio measurements must be realistic.");
        }

        LastSeasonStats.Validate();
        CareerStats.Validate();
        ContractAsk.Validate();
        Interest.Validate();
        FitSummary.Validate();
        FinalContractPreference?.Validate();
    }
}
