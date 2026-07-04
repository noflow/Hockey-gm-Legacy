namespace LegacyEngine.Scouting;

public sealed record ScoutingAssignment(
    string AssignmentId,
    string ScoutId,
    ScoutingAssignmentType AssignmentType,
    string TargetId,
    string TargetName,
    IReadOnlyCollection<ScoutSpecialty> FocusAreas,
    DateOnly AssignedOn,
    DateOnly? DueOn)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AssignmentId))
        {
            throw new ArgumentException("Assignment id is required.", nameof(AssignmentId));
        }

        if (string.IsNullOrWhiteSpace(ScoutId))
        {
            throw new ArgumentException("Scout id is required.", nameof(ScoutId));
        }

        if (string.IsNullOrWhiteSpace(TargetId))
        {
            throw new ArgumentException("Assignment target id is required.", nameof(TargetId));
        }

        if (string.IsNullOrWhiteSpace(TargetName))
        {
            throw new ArgumentException("Assignment target name is required.", nameof(TargetName));
        }

        if (FocusAreas.Count == 0)
        {
            throw new ArgumentException("A scouting assignment must have at least one focus area.", nameof(FocusAreas));
        }

        if (DueOn.HasValue && DueOn.Value < AssignedOn)
        {
            throw new ArgumentOutOfRangeException(nameof(DueOn), "Assignment due date cannot be before the assigned date.");
        }
    }
}
