namespace LegacyEngine.Integration;

public enum StoryType
{
    Unknown,
    YoungStarRising,
    LateBloomer,
    CareerRevival,
    CaptainJourney,
    VeteranDecline,
    InjuryComeback,
    TopProspect,
    DraftSteal,
    FirstOverallBust,
    Journeyman,
    CareerYear,
    Rebuild,
    Dynasty,
    ChampionshipWindow,
    PlayoffDrought,
    YouthMovement,
    GoaltendingCrisis,
    DefenseFirst,
    OffensiveExplosion,
    FirstSeason,
    HotSeat,
    MasterDraftClass,
    AggressiveTrader,
    ProspectBuilder,
    BudgetGenius,
    CupContender,
    DynastyBuilder,
    LegendaryScout,
    PlayerWhisperer,
    CoachRebuild,
    MedicalTurnaround,
    AssistantPromoted,
    PatientBuilder,
    ChampionshipDemand,
    BudgetFreeze,
    SupportsRebuild,
    LosingConfidence
}

public enum StoryStatus
{
    Emerging,
    Active,
    Rising,
    AtRisk,
    Resolved,
    Archived
}

public enum StoryImportance
{
    Background,
    Normal,
    Notable,
    Major,
    Defining
}

public sealed record StoryEvent(
    string StoryEventId,
    DateOnly Date,
    string Title,
    string Description,
    string? RelatedEventId,
    string? RelatedPersonId,
    string? RelatedOrganizationId,
    StoryImportance Importance)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StoryEventId)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Story event requires id, title, and description.");
        }
    }
}

public sealed record StoryArc(
    string StoryArcId,
    string Name,
    StoryStatus Status,
    int Progress,
    IReadOnlyList<StoryEvent> Events,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StoryArcId)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Story arc requires id, name, and summary.");
        }

        if (Progress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Progress), "Story arc progress must be between zero and one hundred.");
        }

        if (Events.Count == 0)
        {
            throw new ArgumentException("Story arc requires at least one event.", nameof(Events));
        }

        foreach (var storyEvent in Events)
        {
            storyEvent.Validate();
        }
    }
}

public sealed record StorySummary(
    string Headline,
    string ShortSummary,
    string LongSummary,
    IReadOnlyList<string> KeyMoments)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Headline)
            || string.IsNullOrWhiteSpace(ShortSummary)
            || string.IsNullOrWhiteSpace(LongSummary)
            || KeyMoments.Count == 0
            || KeyMoments.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Story summary requires headline, summaries, and key moments.");
        }
    }
}

public sealed record Story(
    string StoryId,
    StoryType StoryType,
    StoryStatus Status,
    StoryImportance Importance,
    string SubjectId,
    string SubjectName,
    string SubjectKind,
    string? OrganizationId,
    string? OrganizationName,
    DateOnly StartedOn,
    DateOnly LastUpdated,
    IReadOnlyList<StoryArc> Arcs,
    StorySummary Summary)
{
    public StoryArc CurrentArc => Arcs.OrderByDescending(arc => arc.Progress).First();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StoryId)
            || string.IsNullOrWhiteSpace(SubjectId)
            || string.IsNullOrWhiteSpace(SubjectName)
            || string.IsNullOrWhiteSpace(SubjectKind))
        {
            throw new ArgumentException("Story requires identity and subject context.");
        }

        if (StoryType == StoryType.Unknown)
        {
            throw new ArgumentException("Story type must be known.", nameof(StoryType));
        }

        if (Arcs.Count == 0)
        {
            throw new ArgumentException("Story requires at least one arc.", nameof(Arcs));
        }

        foreach (var arc in Arcs)
        {
            arc.Validate();
        }

        Summary.Validate();
    }
}
