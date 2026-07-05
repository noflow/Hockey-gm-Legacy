namespace LegacyEngine.Integration;

public sealed record DraftExperienceResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    DraftExperienceState DraftState,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        DraftState.Validate();

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Draft experience summary is required.", nameof(Summary));
        }
    }
}
