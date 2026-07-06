namespace LegacyEngine.Integration;

public sealed record MonthlyGmSummary(
    string SummaryId,
    int Year,
    int Month,
    string MonthName,
    string TeamRecordForMonth,
    string OverallRecord,
    string StandingsPosition,
    string BestPlayer,
    string StrugglingPlayer,
    string TopGoalie,
    string BiggestInjuryConcern,
    string OwnerMood,
    string CoachConcern,
    string HeadScoutUpdate,
    string DevelopmentUpdate,
    string RosterWarning,
    string BudgetStatus,
    int ScoutingReportsCompleted,
    int PendingGmActions,
    string ExecutiveNarrative,
    IReadOnlyList<MonthlyGmSummarySection> Sections)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SummaryId) || string.IsNullOrWhiteSpace(MonthName) || string.IsNullOrWhiteSpace(ExecutiveNarrative))
        {
            throw new ArgumentException("Monthly GM summary requires identity, month, and narrative.");
        }

        foreach (var section in Sections)
        {
            section.Validate();
        }
    }
}

public sealed record MonthlyGmSummarySection(
    string Title,
    IReadOnlyList<string> Lines)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || Lines.Count == 0 || Lines.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Monthly GM summary section requires title and readable lines.");
        }
    }
}

public sealed record MonthlyGmSummaryResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    MonthlyGmSummary Summary,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    bool Created,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Summary.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Monthly summary result requires message.");
        }
    }
}
