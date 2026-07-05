namespace LegacyEngine.Integration;

public sealed record StaffChemistryReport(
    string PersonId,
    string StaffName,
    int GmFit,
    int DepartmentFit,
    IReadOnlyList<string> ConflictWarnings,
    IReadOnlyList<string> StrongPartnerships,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(StaffName))
        {
            throw new ArgumentException("Staff chemistry report requires person id and name.");
        }

        if (GmFit is < 0 or > 100 || DepartmentFit is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(GmFit), "Staff chemistry scores must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff chemistry summary is required.", nameof(Summary));
        }
    }
}
