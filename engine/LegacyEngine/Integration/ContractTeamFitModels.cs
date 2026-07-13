namespace LegacyEngine.Integration;

public sealed record ContractTeamPreference(
    int WinningImportance,
    int StaffFitImportance,
    int HometownImportance,
    int MinimumTeamFit,
    decimal MaximumHometownDiscountPercent,
    bool PrefersContender,
    string Summary)
{
    public static ContractTeamPreference Neutral { get; } = new(
        WinningImportance: 35,
        StaffFitImportance: 35,
        HometownImportance: 20,
        MinimumTeamFit: 35,
        MaximumHometownDiscountPercent: 0m,
        PrefersContender: false,
        Summary: "Money and role remain the primary contract priorities.");

    public void Validate()
    {
        if (WinningImportance is < 0 or > 100
            || StaffFitImportance is < 0 or > 100
            || HometownImportance is < 0 or > 100
            || MinimumTeamFit is < 0 or > 100
            || MaximumHometownDiscountPercent is < 0m or > 25m
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentOutOfRangeException(nameof(WinningImportance), "Contract team preferences must contain valid scores, discount, and summary.");
        }
    }
}

public sealed record ContractTeamFitEvaluation(
    int TeamFitScore,
    int WinningScore,
    int StaffFitScore,
    int HometownScore,
    int RelationshipScore,
    bool HometownDiscountApplied,
    decimal AcceptedSalaryDiscountPercent,
    string Label,
    string Summary,
    string Risk)
{
    public static ContractTeamFitEvaluation Neutral { get; } = new(
        50,
        50,
        50,
        0,
        50,
        false,
        0m,
        "Neutral",
        "Team fit is neutral; money and role will carry most of the decision.",
        "No major team-fit risk is visible.");

    public void Validate()
    {
        if (TeamFitScore is < 0 or > 100
            || WinningScore is < 0 or > 100
            || StaffFitScore is < 0 or > 100
            || HometownScore is < 0 or > 100
            || RelationshipScore is < 0 or > 100
            || AcceptedSalaryDiscountPercent is < 0m or > 25m
            || string.IsNullOrWhiteSpace(Label)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(Risk))
        {
            throw new ArgumentOutOfRangeException(nameof(TeamFitScore), "Contract team fit must contain valid scores and explanations.");
        }
    }
}
