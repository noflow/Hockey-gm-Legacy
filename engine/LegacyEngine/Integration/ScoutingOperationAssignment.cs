namespace LegacyEngine.Integration;

public sealed record ScoutingOperationAssignment(
    string AssignmentId,
    string ScoutPersonId,
    string ScoutName,
    ScoutingOperationAssignmentType AssignmentType,
    ScoutingRegionFocus? TargetRegion,
    string? TargetPlayerId,
    string TargetName,
    DateOnly StartDate,
    DateOnly ExpectedReportDate,
    ScoutingOperationPriority Priority,
    string Notes,
    ScoutingOperationStatus Status,
    int WorkloadAtAssignment,
    int RelationshipQualityAtAssignment,
    int CommunicationQuality,
    int DurationDays = 0,
    DateOnly? ReturnDate = null,
    int ProgressDays = 0,
    DateOnly? CompletedOn = null,
    string? ReportId = null)
{
    public bool IsOpen => Status is ScoutingOperationStatus.Active or ScoutingOperationStatus.Delayed;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AssignmentId))
        {
            throw new ArgumentException("Scouting operation assignment id is required.", nameof(AssignmentId));
        }

        if (string.IsNullOrWhiteSpace(ScoutPersonId) || string.IsNullOrWhiteSpace(ScoutName))
        {
            throw new ArgumentException("Scouting operation assignment requires a scout.", nameof(ScoutPersonId));
        }

        if (AssignmentType == ScoutingOperationAssignmentType.Region && TargetRegion is null)
        {
            throw new ArgumentException("Region assignments require a target region.", nameof(TargetRegion));
        }

        if (AssignmentType == ScoutingOperationAssignmentType.Player && string.IsNullOrWhiteSpace(TargetPlayerId))
        {
            throw new ArgumentException("Player assignments require a target player id.", nameof(TargetPlayerId));
        }

        if (string.IsNullOrWhiteSpace(TargetName))
        {
            throw new ArgumentException("Scouting operation assignment target name is required.", nameof(TargetName));
        }

        if (ExpectedReportDate < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(ExpectedReportDate), "Expected report date cannot be before start date.");
        }

        if (ReturnDate is not null && ReturnDate < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(ReturnDate), "Return date cannot be before start date.");
        }

        if (WorkloadAtAssignment < 0 || ProgressDays < 0 || DurationDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(WorkloadAtAssignment), "Scouting operation workload and progress cannot be negative.");
        }

        ValidateScore(RelationshipQualityAtAssignment, nameof(RelationshipQualityAtAssignment));
        ValidateScore(CommunicationQuality, nameof(CommunicationQuality));

        if (Status == ScoutingOperationStatus.Completed && (CompletedOn is null || string.IsNullOrWhiteSpace(ReportId)))
        {
            throw new ArgumentException("Completed scouting assignments must store completion date and report id.");
        }
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Scouting operation scores must be between 0 and 100.");
        }
    }
}
