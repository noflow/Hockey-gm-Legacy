namespace LegacyEngine.Integration;

public sealed record DailyBriefingRecord(
    string BriefingId,
    DateOnly PreviousDate,
    DateOnly CurrentDate,
    int DaysAdvanced,
    string StopReason,
    string TeamRecord,
    string TopHeadline,
    int ImportantActionCount,
    IReadOnlyList<string> ImportantItems,
    IReadOnlyList<string> SourceEventIds,
    DateTimeOffset GeneratedAt,
    bool IsViewed = false,
    bool IsDismissed = false)
{
    public string DateRangeText => PreviousDate == CurrentDate
        ? CurrentDate.ToString("MMM d, yyyy")
        : $"{PreviousDate:MMM d} - {CurrentDate:MMM d, yyyy}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BriefingId) || string.IsNullOrWhiteSpace(StopReason)
            || string.IsNullOrWhiteSpace(TeamRecord) || string.IsNullOrWhiteSpace(TopHeadline)
            || DaysAdvanced < 0 || ImportantActionCount < 0 || CurrentDate < PreviousDate)
        {
            throw new ArgumentException("Daily briefing requires valid summary context.");
        }
    }
}

/// <summary>A compact, decision-aware view of the world for the GM's morning arrival.</summary>
public sealed record DailyHockeyWorldCard(
    string CardId,
    string Title,
    string Summary,
    string Destination,
    string? RelatedPersonId = null,
    string? RelatedOrganizationId = null,
    bool IsImportant = false,
    bool IsUrgent = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CardId) || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Summary) || string.IsNullOrWhiteSpace(Destination))
        {
            throw new ArgumentException("Daily Hockey World cards require identity, text, and a destination.");
        }
    }
}

public sealed record DailyHockeyWorldSnapshot(
    DateOnly Date,
    IReadOnlyList<DailyHockeyWorldCard> OrganizationCards,
    IReadOnlyList<DailyHockeyWorldCard> LeaguePulseCards,
    IReadOnlyList<DailyHockeyWorldCard> TodayActions,
    IReadOnlyList<string> MorningBriefing,
    string CoachReport,
    string ScoutReport,
    string MedicalReport,
    IReadOnlyList<DailyHockeyWorldCard> LeagueSnapshotCards,
    IReadOnlyList<DailyHockeyWorldCard> ProspectWatchCards,
    IReadOnlyList<DailyHockeyWorldCard> TransactionWireCards,
    IReadOnlyList<DailyHockeyWorldCard> ScheduleCards,
    IReadOnlyList<DailyHockeyWorldCard> CalendarCards,
    DailyHockeyWorldCard PlayerOfTheDay,
    DailyHockeyWorldCard TeamOfTheDay,
    DailyBriefingRecord? LatestBriefing = null)
{
    public void Validate()
    {
        if (MorningBriefing.Count > 3 || string.IsNullOrWhiteSpace(CoachReport)
            || string.IsNullOrWhiteSpace(ScoutReport) || string.IsNullOrWhiteSpace(MedicalReport))
        {
            throw new ArgumentException("Daily Hockey World briefing content is invalid.");
        }

        LatestBriefing?.Validate();

        foreach (var card in OrganizationCards.Concat(LeaguePulseCards).Concat(TodayActions)
                     .Concat(LeagueSnapshotCards).Concat(ProspectWatchCards).Concat(TransactionWireCards)
                     .Concat(ScheduleCards).Concat(CalendarCards).Append(PlayerOfTheDay).Append(TeamOfTheDay))
        {
            card.Validate();
        }
    }
}
