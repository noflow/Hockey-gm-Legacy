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
    IReadOnlyList<string> AssignmentHistory)
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

        foreach (var entry in AssignmentHistory)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new ArgumentException("Player pipeline assignment history cannot contain blank entries.", nameof(AssignmentHistory));
            }
        }
    }
}
