namespace LegacyEngine.Integration;

public sealed record TrainingCampDecisionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    TrainingCamp Camp,
    TrainingCampDecision Decision,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Camp.Validate();
        Decision.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Training camp decision message is required.", nameof(Message));
        }
    }
}
