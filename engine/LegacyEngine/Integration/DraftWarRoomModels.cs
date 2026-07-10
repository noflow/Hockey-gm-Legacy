using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public enum DraftWatchTag
{
    Watching,
    Priority,
    Sleeper,
    Avoid,
    MedicalConcern,
    CharacterConcern,
    LateRoundTarget,
    Favorite,
    Pinned
}

public enum ScoutConsensusLevel
{
    Unknown,
    StrongConsensus,
    MixedOpinions,
    VeryDivided
}

public sealed record DraftWarRoomEntry(
    string ProspectPersonId,
    string ProspectName,
    int PersonalRank,
    int OriginalRank,
    IReadOnlyList<DraftWatchTag> Tags,
    string GroupName,
    bool IsPinned,
    bool IsFavorite,
    bool IsRemoved,
    string GmNotes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(GroupName)
            || GmNotes is null)
        {
            throw new ArgumentException("Draft war room entry requires prospect identity, group, and notes.");
        }

        if (PersonalRank <= 0 || OriginalRank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PersonalRank), "Draft war room ranks must be positive.");
        }
    }
}

public sealed record DraftNeedAnalysis(
    TeamNeedType NeedType,
    string Label,
    string Reason,
    TradePriority Priority,
    RosterPosition? TargetPosition)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Label) || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Draft need analysis requires label and reason.");
        }
    }
}

public sealed record DraftDepartmentOpinion(
    string Department,
    string ProspectPersonId,
    string ProspectName,
    string Opinion,
    ScoutingConfidenceLevel Confidence)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Department)
            || string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(Opinion))
        {
            throw new ArgumentException("Draft department opinion requires readable department, prospect, and opinion.");
        }
    }
}

public sealed record DraftScoutConsensus(
    string ProspectPersonId,
    string ProspectName,
    ScoutConsensusLevel Level,
    int AgreementScore,
    IReadOnlyList<DraftDepartmentOpinion> Opinions,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft scout consensus requires prospect identity and summary.");
        }

        foreach (var opinion in Opinions)
        {
            opinion.Validate();
        }
    }
}

public sealed record DraftProspectComparisonItem(
    string ProspectPersonId,
    string ProspectName,
    RosterPosition Position,
    int? Age,
    string Height,
    string Weight,
    string CurrentTeamLeague,
    ScoutingConfidenceLevel? Confidence,
    string Projection,
    string Character,
    string Development,
    string Medical,
    string DraftStory)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(Height)
            || string.IsNullOrWhiteSpace(Weight)
            || string.IsNullOrWhiteSpace(CurrentTeamLeague)
            || string.IsNullOrWhiteSpace(Projection)
            || string.IsNullOrWhiteSpace(Character)
            || string.IsNullOrWhiteSpace(Development)
            || string.IsNullOrWhiteSpace(Medical)
            || string.IsNullOrWhiteSpace(DraftStory))
        {
            throw new ArgumentException("Draft comparison item requires readable prospect context.");
        }
    }
}

public sealed record DraftProspectComparison(
    IReadOnlyList<DraftProspectComparisonItem> Prospects,
    string Summary)
{
    public void Validate()
    {
        if (Prospects.Count is < 2 or > 4 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft comparison requires two to four prospects and a summary.");
        }

        foreach (var prospect in Prospects)
        {
            prospect.Validate();
        }
    }
}

public sealed record DraftWarRoomBoardSnapshot(
    int Rank,
    string ProspectPersonId,
    string ProspectName,
    RosterPosition Position,
    string Projection,
    ScoutingConfidenceLevel? Confidence,
    string Notes)
{
    public void Validate()
    {
        if (Rank <= 0
            || string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(Projection)
            || Notes is null)
        {
            throw new ArgumentException("Draft board snapshot requires rank, prospect, projection, and notes.");
        }
    }
}

public sealed record DraftPostDraftReview(
    int SeasonYear,
    DateOnly CreatedOn,
    string HeadScoutReview,
    string OwnerReview,
    string CoachReview,
    string LeagueGrade,
    IReadOnlyList<string> PlayerImpactSummaries)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HeadScoutReview)
            || string.IsNullOrWhiteSpace(OwnerReview)
            || string.IsNullOrWhiteSpace(CoachReview)
            || string.IsNullOrWhiteSpace(LeagueGrade))
        {
            throw new ArgumentException("Draft review requires staff, owner, coach, and league grade summaries.");
        }
    }
}

public sealed record DraftWarRoomState(
    string OrganizationId,
    int SeasonYear,
    IReadOnlyList<DraftWarRoomEntry> BoardEntries,
    IReadOnlyList<DraftNeedAnalysis> Needs,
    IReadOnlyList<DraftClassStoryline> Storylines,
    IReadOnlyList<DraftDepartmentOpinion> BestPlayerAvailableOpinions,
    IReadOnlyList<DraftWarRoomBoardSnapshot> OriginalBoardSnapshot,
    DraftPostDraftReview? PostDraftReview)
{
    public IReadOnlyList<DraftWarRoomBoardView> BoardViews { get; init; } = Array.Empty<DraftWarRoomBoardView>();

    public IReadOnlyList<DraftIntelligenceAlert> IntelligenceAlerts { get; init; } = Array.Empty<DraftIntelligenceAlert>();

    public DraftBoardRealismProfile? RealismProfile { get; init; }

    public DraftPositionValueProfile? PositionValueProfile { get; init; }

    public DraftBoardValidationResult? RealismValidation { get; init; }

    public DraftBoardRebalancingResult? RebalancingResult { get; init; }

    public IReadOnlyList<DraftWarRoomBoardSnapshot> FinalBoardSnapshot { get; init; } = Array.Empty<DraftWarRoomBoardSnapshot>();

    public static DraftWarRoomState Empty { get; } = new(
        string.Empty,
        0,
        Array.Empty<DraftWarRoomEntry>(),
        Array.Empty<DraftNeedAnalysis>(),
        Array.Empty<DraftClassStoryline>(),
        Array.Empty<DraftDepartmentOpinion>(),
        Array.Empty<DraftWarRoomBoardSnapshot>(),
        null);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) && BoardEntries.Count > 0)
        {
            throw new ArgumentException("Draft war room requires organization id when entries exist.");
        }

        foreach (var entry in BoardEntries)
        {
            entry.Validate();
        }

        if (BoardEntries.Select(entry => entry.ProspectPersonId).Distinct(StringComparer.Ordinal).Count() != BoardEntries.Count)
        {
            throw new ArgumentException("Draft war room entries must be unique by prospect.");
        }

        foreach (var need in Needs)
        {
            need.Validate();
        }

        foreach (var storyline in Storylines)
        {
            storyline.Validate();
        }

        foreach (var opinion in BestPlayerAvailableOpinions)
        {
            opinion.Validate();
        }

        foreach (var snapshot in OriginalBoardSnapshot)
        {
            snapshot.Validate();
        }

        foreach (var view in BoardViews)
        {
            view.Validate();
        }

        foreach (var alert in IntelligenceAlerts)
        {
            alert.Validate();
        }

        RealismProfile?.Validate();
        PositionValueProfile?.Validate();
        RealismValidation?.Validate();
        RebalancingResult?.Validate();
        foreach (var snapshot in FinalBoardSnapshot)
        {
            snapshot.Validate();
        }

        PostDraftReview?.Validate();
    }
}
