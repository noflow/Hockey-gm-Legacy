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

    public void Validate()
    {
        AlphaSnapshot.Validate();
        Organization.Validate();
        Season.Validate();
        GeneralManagerProfile.Validate();

        if (DraftDate < CurrentDate)
        {
            throw new ArgumentOutOfRangeException(nameof(DraftDate), "Draft date cannot be before the scenario date.");
        }

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
    }
}
