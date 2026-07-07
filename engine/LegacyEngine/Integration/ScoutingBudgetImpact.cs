namespace LegacyEngine.Integration;

public sealed record ScoutingBudgetImpact(
    decimal ScoutingBudget,
    string TravelCoverage,
    string TournamentCoverage,
    string InternationalCoverage,
    string OwnerComment)
{
    public void Validate()
    {
        if (ScoutingBudget < 0
            || string.IsNullOrWhiteSpace(TravelCoverage)
            || string.IsNullOrWhiteSpace(TournamentCoverage)
            || string.IsNullOrWhiteSpace(InternationalCoverage)
            || string.IsNullOrWhiteSpace(OwnerComment))
        {
            throw new ArgumentException("Scouting budget impact requires non-negative budget and readable coverage text.");
        }
    }
}
