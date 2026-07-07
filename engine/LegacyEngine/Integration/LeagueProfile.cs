using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed record LeagueProfile(
    LeagueExperience Experience,
    LeagueIdentity Identity,
    Rulebook Rulebook,
    IReadOnlyList<TeamSelectionOption> Teams,
    IReadOnlyList<string> RosterRulesSummary,
    IReadOnlyList<string> DraftRulesSummary,
    IReadOnlyList<string> TradeRulesSummary,
    IReadOnlyList<string> ContractRulesSummary,
    IReadOnlyList<string> BudgetRulesSummary,
    IReadOnlyList<string> DevelopmentRulesSummary,
    IReadOnlyList<string> AffiliateRulesSummary,
    IReadOnlyList<string> RecruitingRulesSummary)
{
    public bool DraftEnabled => Rulebook.DraftRules?.DraftEnabled == true;

    public void Validate()
    {
        Identity.Validate();
        if (string.IsNullOrWhiteSpace(Rulebook.RulebookId) || string.IsNullOrWhiteSpace(Rulebook.LeagueType) || Teams.Count == 0)
        {
            throw new ArgumentException("League profile requires a rulebook and at least one team.");
        }

        foreach (var team in Teams)
        {
            team.Validate();
        }

        if (RosterRulesSummary.Count == 0
            || DraftRulesSummary.Count == 0
            || TradeRulesSummary.Count == 0
            || ContractRulesSummary.Count == 0
            || BudgetRulesSummary.Count == 0
            || DevelopmentRulesSummary.Count == 0
            || AffiliateRulesSummary.Count == 0
            || RecruitingRulesSummary.Count == 0)
        {
            throw new ArgumentException("League profile summaries must describe the playable rules.");
        }
    }
}
