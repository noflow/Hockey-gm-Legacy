namespace LegacyEngine.Integration;

public sealed record LeagueTeamBehavior(
    string DraftBehavior,
    string TradeBehavior,
    string FreeAgencyBehavior,
    string ScoutingBehavior,
    string DevelopmentBehavior,
    string StaffHiringBehavior)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DraftBehavior)
            || string.IsNullOrWhiteSpace(TradeBehavior)
            || string.IsNullOrWhiteSpace(FreeAgencyBehavior)
            || string.IsNullOrWhiteSpace(ScoutingBehavior)
            || string.IsNullOrWhiteSpace(DevelopmentBehavior)
            || string.IsNullOrWhiteSpace(StaffHiringBehavior))
        {
            throw new ArgumentException("League team behavior requires readable descriptions.");
        }
    }
}
