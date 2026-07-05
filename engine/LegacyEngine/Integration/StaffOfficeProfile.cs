using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffOfficeProfile(
    string PersonId,
    string Name,
    StaffRole CurrentRole,
    StaffDepartment Department,
    string ContractStatus,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    int RelationshipWithGm,
    StaffChemistryReport Chemistry,
    string CurrentAssignment,
    string CurrentFocus)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Staff control profile requires person id and name.");
        }

        if (string.IsNullOrWhiteSpace(ContractStatus) || string.IsNullOrWhiteSpace(CurrentAssignment) || string.IsNullOrWhiteSpace(CurrentFocus))
        {
            throw new ArgumentException("Staff control profile display text is required.");
        }

        Chemistry.Validate();
    }
}
