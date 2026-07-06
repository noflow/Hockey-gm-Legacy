using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffSalaryRange(StaffRole? Role, StaffBudgetCategory Category, decimal Minimum, decimal Maximum)
{
    public decimal Midpoint => (Minimum + Maximum) / 2m;

    public bool Contains(decimal salary) => salary >= Minimum && salary <= Maximum;

    public void Validate()
    {
        if (Minimum < 0 || Maximum < 0 || Maximum < Minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(Maximum), "Staff salary range is invalid.");
        }
    }
}
