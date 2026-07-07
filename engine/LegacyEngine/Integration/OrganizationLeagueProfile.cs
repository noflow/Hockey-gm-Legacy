namespace LegacyEngine.Integration;

public sealed record OrganizationLeagueProfile(
    string OrganizationId,
    string TeamName,
    LeagueTeamIdentity Identity,
    LeagueGmPersonality GmPersonality,
    OwnerInfluencePhilosophy OwnerPhilosophy,
    OrganizationStrategyStage CurrentStrategy,
    IReadOnlyList<TeamNeed> CurrentNeeds,
    string BudgetStyle,
    TeamBuildingPhilosophy DraftStyle,
    ScoutingPhilosophy ScoutingFocus,
    string DevelopmentGrade,
    TeamNeedsProfile TradeNeedsProfile,
    LeagueTeamBehavior Behavior,
    string RecentDirection,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(BudgetStyle)
            || string.IsNullOrWhiteSpace(DevelopmentGrade)
            || string.IsNullOrWhiteSpace(RecentDirection)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Organization league profile requires identity and summary text.");
        }

        if (CurrentNeeds.Count == 0)
        {
            throw new ArgumentException("Organization league profile requires current needs.", nameof(CurrentNeeds));
        }

        foreach (var need in CurrentNeeds)
        {
            need.Validate();
        }

        TradeNeedsProfile.Validate();
        Behavior.Validate();
    }
}
