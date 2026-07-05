using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public static class DraftUiPolicy
{
    public static bool IsDraftUiEnabled(Rulebook? rulebook) =>
        rulebook?.DraftRules?.DraftEnabled ?? true;
}
