namespace LegacyEngine.Integration;

public sealed record TrainingCampResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    TrainingCamp Camp,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Camp.Validate();

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Training camp result summary is required.", nameof(Summary));
        }
    }
}
