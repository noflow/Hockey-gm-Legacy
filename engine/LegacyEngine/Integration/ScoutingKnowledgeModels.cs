using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum ScoutingAccuracy
{
    Unknown,
    Poor,
    Fair,
    Good,
    Excellent
}

public enum ScoutingBias
{
    Optimistic,
    Conservative,
    OvervaluesSize,
    OvervaluesSkating,
    UnderestimatesSkill,
    StrongGoalieEvaluator,
    StrongCharacterEvaluator,
    PoorProjectionScout,
    ExcellentRegionalScout,
    SleeperFinder,
    SafePickScout
}

public sealed record AttributeKnowledgeState(
    PlayerAttributeKey Attribute,
    PlayerRatingCategory Category,
    PlayerRatingRange Estimate,
    PlayerRatingColor ConfidenceColor,
    DateOnly? LastViewedDate,
    string? SourceScoutId,
    string SourceScoutName,
    int DisagreementLevel,
    bool IsStale,
    string Note)
{
    public bool IsKnown => !Estimate.IsUnknown;

    public void Validate()
    {
        Estimate.Validate();
        if (DisagreementLevel is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(DisagreementLevel), "Disagreement level must stay within 0-100.");
        }

        if (string.IsNullOrWhiteSpace(SourceScoutName) || string.IsNullOrWhiteSpace(Note))
        {
            throw new ArgumentException("Attribute knowledge requires source text and a note.");
        }
    }
}

public sealed record ScoutAttributeOpinion(
    string OpinionId,
    string ScoutId,
    string ScoutName,
    string PlayerId,
    string PlayerName,
    PlayerAttributeKey Attribute,
    PlayerRatingRange Estimate,
    PlayerRatingColor ConfidenceColor,
    ScoutingBias Bias,
    DateOnly Date,
    string Note)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OpinionId)
            || string.IsNullOrWhiteSpace(ScoutId)
            || string.IsNullOrWhiteSpace(ScoutName)
            || string.IsNullOrWhiteSpace(PlayerId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Note))
        {
            throw new ArgumentException("Scout attribute opinion requires ids, names, and a note.");
        }

        Estimate.Validate();
    }
}

public sealed record ScoutingConsensus(
    string PlayerId,
    string PlayerName,
    PlayerRatingRange OverallEstimate,
    PlayerRatingRange PotentialEstimate,
    PlayerRatingColor ConfidenceColor,
    int DisagreementLevel,
    string Summary,
    string BiggestDisagreement,
    IReadOnlyList<ScoutAttributeOpinion> ScoutOpinions)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayerId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(BiggestDisagreement))
        {
            throw new ArgumentException("Scouting consensus requires player identity and summary.");
        }

        OverallEstimate.Validate();
        PotentialEstimate.Validate();
        if (DisagreementLevel is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(DisagreementLevel), "Disagreement level must stay within 0-100.");
        }

        foreach (var opinion in ScoutOpinions)
        {
            opinion.Validate();
        }
    }
}

public sealed record ScoutingKnowledgeProfile(
    string OrganizationId,
    string PlayerId,
    string PlayerName,
    RosterPosition Position,
    DateOnly CreatedOn,
    DateOnly LastViewedDate,
    IReadOnlyList<AttributeKnowledgeState> Attributes,
    IReadOnlyList<ScoutAttributeOpinion> ScoutOpinions,
    ScoutingConsensus Consensus,
    bool IsStale,
    string RecommendedNextAction)
{
    public int KnownAttributeCount => Attributes.Count(attribute => attribute.IsKnown);

    public int UnknownAttributeCount => Attributes.Count(attribute => !attribute.IsKnown);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(PlayerId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(RecommendedNextAction))
        {
            throw new ArgumentException("Scouting knowledge profile requires organization, player, and recommendation.");
        }

        if (Attributes.Count == 0)
        {
            throw new ArgumentException("Scouting knowledge profile requires attribute states.", nameof(Attributes));
        }

        foreach (var attribute in Attributes)
        {
            attribute.Validate();
        }

        foreach (var opinion in ScoutOpinions)
        {
            opinion.Validate();
        }

        Consensus.Validate();
    }
}

public sealed record ScoutingKnowledgeUpdate(
    NewGmScenarioSnapshot ScenarioSnapshot,
    ScoutingKnowledgeProfile Profile,
    ScoutingConsensus Consensus,
    IReadOnlyList<ActionCenterItem> ActionItems,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Profile.Validate();
        Consensus.Validate();
        foreach (var item in ActionItems)
        {
            item.Validate();
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Scouting knowledge update requires a summary.", nameof(Summary));
        }
    }
}

public sealed record ScoutAccuracyRecord(
    string ScoutId,
    string ScoutName,
    int CorrectHits,
    int Misses,
    int GemsFound,
    int BustsRecommended,
    PlayerRatingCategory StrongestCategory,
    PlayerRatingCategory WeakestCategory,
    ScoutingAccuracy Accuracy,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScoutId) || string.IsNullOrWhiteSpace(ScoutName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Scout accuracy record requires identity and summary.");
        }

        if (CorrectHits < 0 || Misses < 0 || GemsFound < 0 || BustsRecommended < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CorrectHits), "Scout accuracy counters cannot be negative.");
        }
    }
}
