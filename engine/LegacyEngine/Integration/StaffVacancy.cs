using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffVacancy(
    StaffRole Role,
    StaffDepartment Department,
    int Required,
    int Current,
    int Maximum,
    string Warning)
{
    public bool IsOpen => Current < Required;

    public void Validate()
    {
        if (Required < 0 || Current < 0 || Maximum < Required)
        {
            throw new ArgumentOutOfRangeException(nameof(Required), "Staff vacancy counts are invalid.");
        }

        if (string.IsNullOrWhiteSpace(Warning))
        {
            throw new ArgumentException("Staff vacancy warning is required.", nameof(Warning));
        }
    }
}
