using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffCompensationProfile(
    string PersonId,
    StaffRole Role,
    StaffBudgetCategory Category,
    StaffSalary Salary,
    StaffSalaryRange Range,
    bool IsObligation = false,
    string Notes = "")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Compensation profile requires person id.", nameof(PersonId));
        }

        Salary.Validate();
        Range.Validate();

        if (!Range.Contains(Salary.AnnualAmount) && !IsObligation)
        {
            throw new ArgumentOutOfRangeException(nameof(Salary), "Staff salary should be inside the configured league range.");
        }
    }
}
