using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed record ScoutingOperationResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    ScoutingOperationAssignment? Assignment,
    ScoutingReport? Report,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Assignment?.Validate();
        Report?.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Scouting operation result message is required.", nameof(Message));
        }
    }
}
