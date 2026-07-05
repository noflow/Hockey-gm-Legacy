namespace LegacyEngine.Integration;

public sealed record RecruitingV2Result(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    RecruitingV2Profile Profile,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message,
    string DecisionExplanation = "")
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Profile.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Recruiting result message is required.", nameof(Message));
        }
    }
}
