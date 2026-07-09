namespace LegacyEngine.Integration;

public enum FranchisePhilosophy
{
    Unknown,
    DraftAndDevelop,
    DefenseFirst,
    OffensiveHockey,
    GoaltendingFactory,
    EuropeanPipeline,
    PhysicalTeam,
    FastTeam,
    ProspectOrganization,
    ChampionshipCulture,
    ProfessionalOrganization,
    BudgetBuilder,
    AggressiveMarketBuilder
}

public enum FranchiseCulture
{
    Unknown,
    WinningCulture,
    DevelopmentCulture,
    Professional,
    PlayerFriendly,
    Demanding,
    HardWorking,
    Disciplined,
    FamilyOrganization,
    PressureOrganization
}

public enum FranchiseDirection
{
    Unknown,
    Rebuild,
    DevelopCore,
    Retool,
    Compete,
    Contend,
    Sustain,
    BudgetReset
}

public enum FranchiseReputation
{
    Unknown,
    EliteOrganization,
    ModelOrganization,
    Respected,
    Average,
    Rebuilding,
    PoorlyRun,
    BudgetTeam
}

public sealed record FranchiseEra(
    string EraId,
    int StartYear,
    int? EndYear,
    string Name,
    string GeneralManagerName,
    string OwnerName,
    string CoachName,
    FranchisePhilosophy Identity,
    IReadOnlyList<string> Achievements)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EraId)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(GeneralManagerName)
            || string.IsNullOrWhiteSpace(OwnerName)
            || string.IsNullOrWhiteSpace(CoachName))
        {
            throw new ArgumentException("Franchise era requires identity, leadership, and name.");
        }

        if (StartYear < 1900 || EndYear < StartYear)
        {
            throw new ArgumentOutOfRangeException(nameof(StartYear), "Franchise era year range is invalid.");
        }

        if (Achievements.Count == 0 || Achievements.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Franchise era requires readable achievements.", nameof(Achievements));
        }
    }
}

public sealed record FranchiseIdentityShift(
    DateOnly Date,
    FranchisePhilosophy FromIdentity,
    FranchisePhilosophy ToIdentity,
    FranchiseCulture FromCulture,
    FranchiseCulture ToCulture,
    string Reason,
    string VisibleExplanation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(VisibleExplanation))
        {
            throw new ArgumentException("Franchise identity shift requires reason and visible explanation.");
        }
    }
}

public sealed record FranchiseHistory(
    int PlayoffAppearances,
    int Championships,
    int FinalsAppearances,
    int Rebuilds,
    int Dynasties,
    int LongestPlayoffStreak,
    string WorstSeason,
    string GreatestDraftClass,
    string BestTrade)
{
    public void Validate()
    {
        if (PlayoffAppearances < 0
            || Championships < 0
            || FinalsAppearances < 0
            || Rebuilds < 0
            || Dynasties < 0
            || LongestPlayoffStreak < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayoffAppearances), "Franchise history counters cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(WorstSeason)
            || string.IsNullOrWhiteSpace(GreatestDraftClass)
            || string.IsNullOrWhiteSpace(BestTrade))
        {
            throw new ArgumentException("Franchise history requires readable story fields.");
        }
    }
}

public sealed record FranchiseIdentity(
    string OrganizationId,
    string TeamName,
    FranchisePhilosophy CurrentIdentity,
    IReadOnlyList<FranchisePhilosophy> HistoricalIdentity,
    FranchisePhilosophy CurrentPhilosophy,
    FranchiseCulture Culture,
    FranchiseDirection FutureDirection,
    FranchiseEra CurrentEra,
    IReadOnlyList<FranchiseEra> HistoricalEras,
    FranchiseHistory History,
    FranchiseReputation Reputation,
    IReadOnlyList<string> TeamDna,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> FutureGoals,
    IReadOnlyList<FranchiseIdentityShift> IdentityShifts,
    DateOnly LastUpdated,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Franchise identity requires organization identity and summary.");
        }

        if (CurrentIdentity == FranchisePhilosophy.Unknown
            || CurrentPhilosophy == FranchisePhilosophy.Unknown
            || Culture == FranchiseCulture.Unknown
            || FutureDirection == FranchiseDirection.Unknown
            || Reputation == FranchiseReputation.Unknown)
        {
            throw new ArgumentException("Franchise identity requires known identity, culture, direction, and reputation.");
        }

        CurrentEra.Validate();
        History.Validate();
        foreach (var era in HistoricalEras)
        {
            era.Validate();
        }

        foreach (var shift in IdentityShifts)
        {
            shift.Validate();
        }

        if (HistoricalIdentity.Count == 0
            || TeamDna.Count == 0
            || Strengths.Count == 0
            || Weaknesses.Count == 0
            || FutureGoals.Count == 0)
        {
            throw new ArgumentException("Franchise identity requires historical identity, DNA, strengths, weaknesses, and future goals.");
        }

        if (TeamDna.Any(string.IsNullOrWhiteSpace)
            || Strengths.Any(string.IsNullOrWhiteSpace)
            || Weaknesses.Any(string.IsNullOrWhiteSpace)
            || FutureGoals.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Franchise identity text fields cannot be blank.");
        }
    }
}

public sealed record FranchiseFitResult(
    string SubjectId,
    string SubjectName,
    string SubjectType,
    string FitLabel,
    int Score,
    IReadOnlyList<string> Reasons,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SubjectId)
            || string.IsNullOrWhiteSpace(SubjectName)
            || string.IsNullOrWhiteSpace(SubjectType)
            || string.IsNullOrWhiteSpace(FitLabel)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Franchise fit result requires subject and summary text.");
        }

        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Franchise fit score must be between 0 and 100.");
        }

        if (Reasons.Count == 0 || Reasons.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Franchise fit requires readable reasons.", nameof(Reasons));
        }
    }
}
