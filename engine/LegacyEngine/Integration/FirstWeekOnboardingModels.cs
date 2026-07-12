namespace LegacyEngine.Integration;

public sealed record AssistantGmBriefing(
    string TeamIdentity,
    string CompetitiveWindow,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Concerns,
    string KeyContractDecision,
    string BestProspect,
    string BiggestRosterNeed,
    string RecommendedFirstAction,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TeamIdentity) || string.IsNullOrWhiteSpace(CompetitiveWindow)
            || Strengths.Count == 0 || Concerns.Count == 0 || string.IsNullOrWhiteSpace(KeyContractDecision)
            || string.IsNullOrWhiteSpace(BestProspect) || string.IsNullOrWhiteSpace(BiggestRosterNeed)
            || string.IsNullOrWhiteSpace(RecommendedFirstAction) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Assistant GM briefing requires concise organizational context.");
        }
    }
}

public sealed record FirstWeekOnboardingPlan(
    DateOnly StartDate,
    AssistantGmBriefing AssistantGmBriefing,
    int FirstDayActionLimit,
    IReadOnlyDictionary<int, int> ActionLimitsByDay,
    string Summary)
{
    public void Validate()
    {
        AssistantGmBriefing.Validate();
        if (FirstDayActionLimit is < 3 or > 5 || ActionLimitsByDay.Count == 0 || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("First-week onboarding plan is invalid.");
        }
    }
}
