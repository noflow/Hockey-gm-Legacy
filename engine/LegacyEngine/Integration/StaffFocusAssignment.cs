using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffFocusAssignment(
    string FocusId,
    string PersonId,
    StaffDepartment Department,
    string Focus,
    DateOnly SetOn,
    string Notes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FocusId))
        {
            throw new ArgumentException("Staff focus id is required.", nameof(FocusId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Staff focus person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(Focus))
        {
            throw new ArgumentException("Staff focus is required.", nameof(Focus));
        }

        if (string.IsNullOrWhiteSpace(Notes))
        {
            throw new ArgumentException("Staff focus notes are required.", nameof(Notes));
        }
    }
}
