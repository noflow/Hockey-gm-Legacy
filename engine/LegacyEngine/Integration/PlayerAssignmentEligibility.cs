namespace LegacyEngine.Integration;

public sealed record PlayerAssignmentEligibility(
    string PersonId,
    bool IsSigned,
    bool IsJuniorEligible,
    bool IsAhlEligible,
    bool IsNhlEligible,
    bool IsChlProtected,
    bool IsSlideEligible,
    bool SlideCanBeUsed,
    bool SlideUsed,
    int NhlGamesTowardSlideThreshold,
    string RecommendedAssignment,
    IReadOnlyList<string> InvalidReasons,
    IReadOnlyList<string> Warnings)
{
    public bool CanAssignToAhl => IsAhlEligible && InvalidReasons.All(reason => !reason.Contains("AHL", StringComparison.OrdinalIgnoreCase));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(RecommendedAssignment))
        {
            throw new ArgumentException("Player assignment eligibility requires person id and recommendation.");
        }
    }
}
