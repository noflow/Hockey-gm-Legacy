using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum PlayerKnowledgeSource
{
    HeadCoach,
    AssistantCoach,
    DevelopmentCoach,
    AhlCoach,
    JuniorScout,
    ProfessionalScout,
    MedicalStaff,
    TrainingStaff,
    GameUsage,
    TrainingCamp,
    DevelopmentReport
}

public enum PlayerKnowledgeLevel
{
    Unknown,
    ExternalScouting,
    Basic,
    Working,
    Detailed,
    Full
}

public sealed record InternalAttributeEvaluation(
    PlayerAttributeKey Attribute,
    PlayerRatingCategory Category,
    int Estimate,
    PlayerRatingConfidence Confidence,
    DateOnly LastEvaluated,
    PlayerKnowledgeSource Source,
    string Note)
{
    public void Validate()
    {
        if (Estimate is < 0 or > 100 || string.IsNullOrWhiteSpace(Note))
        {
            throw new ArgumentException("Internal attribute evaluation requires a valid estimate and note.");
        }
    }
}

public sealed record InternalPlayerKnowledge(
    string OrganizationId,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    PlayerKnowledgeLevel KnowledgeLevel,
    int OverallEstimate,
    int PotentialEstimate,
    int OriginalPotentialEstimate,
    PlayerRatingConfidence Confidence,
    DateOnly LastEvaluated,
    IReadOnlyList<PlayerKnowledgeSource> Sources,
    IReadOnlyList<InternalAttributeEvaluation> Attributes,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(Summary)
            || OverallEstimate is < 0 or > 100 || PotentialEstimate is < 0 or > 100
            || OriginalPotentialEstimate is < 0 or > 100 || PotentialEstimate < OverallEstimate)
        {
            throw new ArgumentException("Internal player knowledge is invalid.");
        }

        if (Sources.Count == 0 || Attributes.Count == 0)
        {
            throw new ArgumentException("Internal player knowledge requires sources and attribute evaluations.");
        }

        foreach (var attribute in Attributes)
        {
            attribute.Validate();
        }
    }
}

public sealed record InternalEvaluationUpdate(
    NewGmScenarioSnapshot ScenarioSnapshot,
    InternalPlayerKnowledge Knowledge,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Knowledge.Validate();
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Internal evaluation update requires a summary.");
        }
    }
}

public sealed record OrganizationPlayerEvaluation(
    string PersonId,
    string PlayerName,
    int Overall,
    int Potential,
    PlayerKnowledgeLevel KnowledgeLevel,
    PlayerGrowthStage CareerStage,
    string Trend,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Trend) || string.IsNullOrWhiteSpace(Summary)
            || Overall is < 0 or > 100 || Potential < Overall || Potential > 100)
        {
            throw new ArgumentException("Organization player evaluation is invalid.");
        }
    }
}
