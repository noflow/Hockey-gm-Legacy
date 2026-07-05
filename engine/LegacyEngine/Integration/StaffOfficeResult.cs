using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffOfficeResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    StaffCandidate? Candidate,
    StaffFocusAssignment? FocusAssignment,
    StaffEvaluation? Evaluation,
    StaffChemistryReport? Chemistry,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Candidate?.Validate();
        FocusAssignment?.Validate();
        Evaluation?.Validate();
        Chemistry?.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Staff control result message is required.", nameof(Message));
        }
    }
}
