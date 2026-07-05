namespace LegacyEngine.Integration;

public sealed record GmActionResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    AlphaWorldSnapshot AlphaSnapshot,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        AlphaSnapshot.Validate();

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Action summary is required.", nameof(Summary));
        }
    }
}
