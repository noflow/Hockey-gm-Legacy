namespace LegacyEngine.Integration;

public sealed record ReturnToPlayDecisionResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    string PersonId,
    ReturnToPlayOption Option,
    string Message,
    IReadOnlyList<AlphaInboxItem> InboxItems)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Return-to-play result requires person id and message.");
        }

        ScenarioSnapshot.Validate();
    }
}
