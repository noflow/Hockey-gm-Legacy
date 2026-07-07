using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffCareerHistory(
    string PersonId,
    string StaffName,
    StaffRole CurrentRole,
    string CurrentOrganization,
    IReadOnlyList<string> PreviousRoles,
    IReadOnlyList<string> NotableHistory,
    string RelationshipWithGm,
    string EvaluationSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(StaffName)
            || string.IsNullOrWhiteSpace(CurrentOrganization)
            || string.IsNullOrWhiteSpace(RelationshipWithGm)
            || string.IsNullOrWhiteSpace(EvaluationSummary))
        {
            throw new ArgumentException("Staff career history requires identity and summaries.");
        }
    }
}
