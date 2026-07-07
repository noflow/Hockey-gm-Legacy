namespace LegacyEngine.Integration;

public sealed record StaffHiringFitSummary(
    string CandidateId,
    string CandidateName,
    string TargetRole,
    int FitScore,
    string SalaryImpact,
    string ChemistryRisk,
    string ExperienceSummary,
    string Recommendation,
    IReadOnlyList<string> Reasons)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CandidateId)
            || string.IsNullOrWhiteSpace(CandidateName)
            || string.IsNullOrWhiteSpace(TargetRole)
            || string.IsNullOrWhiteSpace(SalaryImpact)
            || string.IsNullOrWhiteSpace(ChemistryRisk)
            || string.IsNullOrWhiteSpace(ExperienceSummary)
            || string.IsNullOrWhiteSpace(Recommendation))
        {
            throw new ArgumentException("Staff hiring fit requires display text.");
        }

        if (FitScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(FitScore), "Hiring fit score must be between 0 and 100.");
        }
    }
}
