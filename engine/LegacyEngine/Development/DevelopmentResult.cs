using LegacyEngine.Events;

namespace LegacyEngine.Development;

public sealed record DevelopmentResult(
    string PersonId,
    DateOnly UpdateDate,
    PlayerDevelopmentProfile UpdatedProfile,
    IReadOnlyList<DevelopmentUpdate> Updates,
    int CurrentAbilityChange,
    string Summary,
    string PlayerFacingSummary,
    IReadOnlyList<LegacyEvent> Events)
{
    public bool IsBreakout => CurrentAbilityChange >= 4;

    public bool IsRegression => CurrentAbilityChange <= -3;
}
