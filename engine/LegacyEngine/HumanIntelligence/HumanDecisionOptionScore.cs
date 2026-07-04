namespace LegacyEngine.HumanIntelligence;

public sealed record HumanDecisionOptionScore(
    HumanDecisionOption Option,
    decimal Score,
    IReadOnlyList<HumanDecisionReason> Reasons);
