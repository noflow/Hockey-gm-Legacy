namespace LegacyEngine.Integration;

public sealed record OrganizationChartNode(
    string PersonId,
    string Name,
    string Role,
    string ReportsToPersonId,
    string Responsibilities,
    string SalaryText)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Role)
            || string.IsNullOrWhiteSpace(ReportsToPersonId)
            || string.IsNullOrWhiteSpace(Responsibilities)
            || string.IsNullOrWhiteSpace(SalaryText))
        {
            throw new ArgumentException("Organization chart node requires identity, role, reporting, responsibilities, and salary.");
        }
    }
}
