namespace LegacyEngine.Integration;

public sealed record PlayerPipelineRecord(
    string PersonId,
    string PlayerName,
    string CurrentOrganizationId,
    string CurrentTeamName,
    string CurrentLevel,
    string? RightsHolderOrganizationId,
    string? RightsHolderTeamName,
    ParentOrganizationReference? ParentOrganization,
    AffiliateOrganizationReference? AffiliateOrganization,
    PlayerPipelineStatus PipelineStatus,
    PlayerAssignmentStatus AssignmentStatus,
    IReadOnlyList<string> AssignmentHistory,
    PlayerDevelopmentLevel DevelopmentLevel = PlayerDevelopmentLevel.Junior,
    PlayerRightsStatus RightsStatus = PlayerRightsStatus.RightsHeld,
    bool IsSigned = false,
    bool IsAhlEligible = false,
    bool IsJuniorEligible = false,
    bool IsContractSlideEligible = false,
    bool IsContractSlideUsed = false,
    int NhlGamesTowardSlideThreshold = 0,
    string ContractSlideSummary = "No slide status tracked.",
    string RecommendedAssignment = "Review development path.",
    string StaffRecommendation = "Staff recommendation pending.")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(CurrentOrganizationId)
            || string.IsNullOrWhiteSpace(CurrentTeamName)
            || string.IsNullOrWhiteSpace(CurrentLevel))
        {
            throw new ArgumentException("Player pipeline record requires player identity, current organization, and current level.");
        }

        ParentOrganization?.Validate();
        AffiliateOrganization?.Validate();

        if (NhlGamesTowardSlideThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NhlGamesTowardSlideThreshold), "NHL games toward slide cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(ContractSlideSummary)
            || string.IsNullOrWhiteSpace(RecommendedAssignment)
            || string.IsNullOrWhiteSpace(StaffRecommendation))
        {
            throw new ArgumentException("Pipeline record requires slide, assignment, and staff recommendation summaries.");
        }

        foreach (var entry in AssignmentHistory)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new ArgumentException("Player pipeline assignment history cannot contain blank entries.", nameof(AssignmentHistory));
            }
        }
    }
}
