using LegacyEngine.Development;

namespace LegacyEngine.Integration;

public sealed record DevelopmentPipelineResult(
    IReadOnlyList<PlayerDevelopmentProfile> Profiles,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    int ProfilesEvaluated,
    bool WasSkipped,
    string Summary);
