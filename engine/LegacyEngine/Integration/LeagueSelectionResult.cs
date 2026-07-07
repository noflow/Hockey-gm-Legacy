using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed record LeagueSelectionResult(
    LeagueProfile LeagueProfile,
    TeamSelectionOption SelectedTeam,
    NewGmScenarioSettings ScenarioSettings,
    Rulebook Rulebook)
{
    public void Validate()
    {
        LeagueProfile.Validate();
        SelectedTeam.Validate();
        ScenarioSettings.Validate();
        if (!string.Equals(LeagueProfile.Identity.LeagueId, ScenarioSettings.LeagueId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Selected league and scenario settings must reference the same league.");
        }

        if (!string.Equals(SelectedTeam.OrganizationId, ScenarioSettings.OrganizationId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Selected team and scenario settings must reference the same organization.");
        }
    }
}
