using LegacyEngine.Contracts;
using LegacyEngine.Organizations;
using LegacyEngine.Scouting;
using LegacyEngine.Seasons;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record NewGmScenarioSnapshot(
    AlphaWorldSnapshot AlphaSnapshot,
    Organization Organization,
    Season Season,
    IReadOnlyList<StaffMember> StaffMembers,
    IReadOnlyList<Contract> Contracts,
    GmCreationResult GeneralManagerProfile,
    IReadOnlyList<ScoutingAssignment> ScoutingAssignments,
    DateOnly DraftDate,
    IReadOnlyList<AlphaInboxItem> FirstDayInbox,
    string ScenarioSummary)
{
    public DateOnly CurrentDate => AlphaSnapshot.CurrentDate;

    public int DaysUntilDraft => DraftDate.DayNumber - CurrentDate.DayNumber;

    public DraftExperienceState? DraftExperience { get; init; }

    public IReadOnlyList<DraftPickSummary> DraftRights { get; init; } = Array.Empty<DraftPickSummary>();

    public IReadOnlyList<DraftRightsRecord> ProspectRights { get; init; } = Array.Empty<DraftRightsRecord>();

    public TrainingCamp? TrainingCamp { get; init; }

    public IReadOnlyList<PendingGmAction> PendingActions { get; init; } = Array.Empty<PendingGmAction>();

    public IReadOnlyList<ScoutingOperationAssignment> ScoutingOperations { get; init; } = Array.Empty<ScoutingOperationAssignment>();

    public IReadOnlyList<ScoutingReport> CompletedScoutingReports { get; init; } = Array.Empty<ScoutingReport>();

    public IReadOnlyDictionary<string, string> PlayerDossierNotes { get; init; } = new Dictionary<string, string>();

    public SeasonReadinessState SeasonReadiness { get; init; } = new();

    public ExecutiveReportArchive ExecutiveReports { get; init; } = ExecutiveReportArchive.Empty;

    public void Validate()
    {
        AlphaSnapshot.Validate();
        Organization.Validate();
        Season.Validate();
        GeneralManagerProfile.Validate();

        if (FirstDayInbox.Count == 0)
        {
            throw new ArgumentException("New GM scenario must include first-day inbox items.", nameof(FirstDayInbox));
        }

        if (string.IsNullOrWhiteSpace(ScenarioSummary))
        {
            throw new ArgumentException("Scenario summary is required.", nameof(ScenarioSummary));
        }

        foreach (var staffMember in StaffMembers)
        {
            staffMember.Validate();
        }

        foreach (var contract in Contracts)
        {
            contract.Validate();
        }

        foreach (var assignment in ScoutingAssignments)
        {
            assignment.Validate();
        }

        foreach (var assignment in ScoutingOperations)
        {
            assignment.Validate();
        }

        foreach (var report in CompletedScoutingReports)
        {
            report.Validate();
        }

        foreach (var note in PlayerDossierNotes)
        {
            if (string.IsNullOrWhiteSpace(note.Key))
            {
                throw new ArgumentException("Player dossier note person id is required.", nameof(PlayerDossierNotes));
            }

            if (note.Value is null)
            {
                throw new ArgumentException("Player dossier note cannot be null.", nameof(PlayerDossierNotes));
            }
        }

        DraftExperience?.Validate();
        foreach (var prospect in ProspectRights)
        {
            prospect.Validate();
        }

        TrainingCamp?.Validate();
        foreach (var pendingAction in PendingActions)
        {
            pendingAction.Validate();
        }

        SeasonReadiness.Validate();
        ExecutiveReports.Validate();
    }
}
