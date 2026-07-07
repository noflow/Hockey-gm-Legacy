namespace LegacyEngine.Integration;

public sealed record DevelopmentV2Result(
    NewGmScenarioSnapshot ScenarioSnapshot,
    PlayerDevelopmentPlan Plan,
    DevelopmentProgressSnapshot Progress,
    IReadOnlyList<DevelopmentRecommendation> Recommendations,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Plan.Validate();
        Progress.Validate();
        foreach (var recommendation in Recommendations)
        {
            recommendation.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Development v2 result message is required.", nameof(Message));
        }
    }
}
