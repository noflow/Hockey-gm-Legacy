namespace LegacyEngine.Integration;

public sealed record DepartmentGradeReport(
    string DepartmentName,
    DepartmentGrade Grade,
    int Score,
    string Summary,
    IReadOnlyList<string> Evidence)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DepartmentName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Department grade report requires department and summary text.");
        }

        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Department score must be between 0 and 100.");
        }
    }
}
