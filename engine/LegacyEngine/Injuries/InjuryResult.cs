using LegacyEngine.Events;

namespace LegacyEngine.Injuries;

public sealed record InjuryResult(
    bool Success,
    Injury Injury,
    string Summary,
    IReadOnlyList<LegacyEvent> Events);
