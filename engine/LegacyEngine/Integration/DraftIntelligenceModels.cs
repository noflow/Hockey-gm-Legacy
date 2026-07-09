using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public enum DraftWarRoomViewType
{
    MyBoard,
    ScoutBoard,
    ConsensusBoard,
    WatchList,
    TeamNeeds,
    Picks,
    CompareProspects,
    DraftClassSummary,
    HiddenGemsAvoidList
}

public enum DraftIntelligenceAlertType
{
    HiddenGemCandidate,
    BustRisk,
    HighCeilingLowFloor,
    SafePick,
    NeedsPatience,
    BadFitForOrganization,
    MedicalRisk,
    CharacterRisk
}

public sealed record DraftAttributeIntelligenceLine(
    PlayerRatingCategory Category,
    PlayerAttributeKey Attribute,
    PlayerRatingRange Estimate,
    PlayerRatingColor ConfidenceColor,
    string SourceScoutName,
    DateOnly? LastScoutedDate,
    string Note)
{
    public string DisplayText =>
        $"{Readable(Category)} / {Readable(Attribute)} {Estimate.Display} {ConfidenceColor}";

    public void Validate()
    {
        Estimate.Validate();
        if (string.IsNullOrWhiteSpace(SourceScoutName) || string.IsNullOrWhiteSpace(Note))
        {
            throw new ArgumentException("Draft attribute intelligence requires source and note.");
        }
    }

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }
}

public sealed record DraftIntelligenceAlert(
    DraftIntelligenceAlertType AlertType,
    string ProspectPersonId,
    string ProspectName,
    int Priority,
    string Summary,
    string RecommendedAction)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(RecommendedAction))
        {
            throw new ArgumentException("Draft intelligence alert requires prospect and readable guidance.");
        }

        if (Priority is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Priority), "Draft intelligence alert priority must stay within 0-100.");
        }
    }
}

public sealed record DraftProspectIntelligenceCard(
    string ProspectPersonId,
    string ProspectName,
    int MyBoardRank,
    int ScoutBoardRank,
    int ConsensusBoardRank,
    RosterPosition Position,
    int? Age,
    string CurrentTeamLeague,
    string ShootsCatches,
    string Height,
    string Weight,
    PlayerRatingRange OverallEstimate,
    PlayerRatingRange PotentialEstimate,
    PlayerRatingColor RatingConfidenceColor,
    ScoutingConfidenceLevel? ScoutingConfidence,
    ScoutConsensusLevel ScoutConsensus,
    int ScoutAgreementScore,
    int TeamFitScore,
    string Projection,
    string PlayerType,
    string DevelopmentCurve,
    string DevelopmentPace,
    string Eta,
    string RiskSummary,
    string ScoutRecommendation,
    string GmNotes,
    IReadOnlyList<DraftAttributeIntelligenceLine> Attributes,
    IReadOnlyList<DraftIntelligenceAlert> Alerts)
{
    public string RatingDisplay => $"OVR {OverallEstimate.Display} {RatingConfidenceColor} | POT {PotentialEstimate.Display} {RatingConfidenceColor}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId)
            || string.IsNullOrWhiteSpace(ProspectName)
            || string.IsNullOrWhiteSpace(CurrentTeamLeague)
            || string.IsNullOrWhiteSpace(ShootsCatches)
            || string.IsNullOrWhiteSpace(Height)
            || string.IsNullOrWhiteSpace(Weight)
            || string.IsNullOrWhiteSpace(Projection)
            || string.IsNullOrWhiteSpace(PlayerType)
            || string.IsNullOrWhiteSpace(DevelopmentCurve)
            || string.IsNullOrWhiteSpace(DevelopmentPace)
            || string.IsNullOrWhiteSpace(Eta)
            || string.IsNullOrWhiteSpace(RiskSummary)
            || string.IsNullOrWhiteSpace(ScoutRecommendation)
            || string.IsNullOrWhiteSpace(GmNotes))
        {
            throw new ArgumentException("Draft prospect intelligence card requires readable prospect context.");
        }

        OverallEstimate.Validate();
        PotentialEstimate.Validate();
        if (MyBoardRank < 0 || ScoutBoardRank < 0 || ConsensusBoardRank < 0 || TeamFitScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MyBoardRank), "Draft prospect intelligence ranks and fit must be valid.");
        }

        foreach (var attribute in Attributes)
        {
            attribute.Validate();
        }

        foreach (var alert in Alerts)
        {
            alert.Validate();
        }
    }
}

public sealed record DraftWarRoomBoardView(
    DraftWarRoomViewType ViewType,
    string Title,
    IReadOnlyList<string> Rows,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft war room board view requires title and summary.");
        }
    }
}
