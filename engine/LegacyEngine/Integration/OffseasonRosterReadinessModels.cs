using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public enum OffseasonReadinessPhase
{
    ContractReview,
    MarketReview,
    CampPreparation,
    TrainingCamp,
    OpeningRosterReview,
    ReadyForSeason
}

public enum OffseasonReadinessSeverity
{
    Information,
    Important,
    Urgent
}

public sealed record OffseasonRosterReadinessState(
    DateOnly? LastEvaluatedDate = null,
    OffseasonReadinessPhase? LastPhase = null,
    IReadOnlyList<string>? TransitionNotices = null)
{
    public IReadOnlyList<string> ProcessedTransitionNotices => TransitionNotices ?? Array.Empty<string>();

    public static OffseasonRosterReadinessState Empty { get; } = new();

    public void Validate()
    {
        foreach (var notice in ProcessedTransitionNotices)
        {
            if (string.IsNullOrWhiteSpace(notice))
            {
                throw new ArgumentException("Offseason readiness transition keys cannot be blank.");
            }
        }
    }
}

public sealed record OffseasonRosterReadinessIssue(
    string IssueId,
    OffseasonReadinessSeverity Severity,
    string Title,
    string Reason,
    string Consequence,
    string RecommendedAction,
    DateOnly? DueDate = null,
    string? RelatedPersonId = null,
    string? RelatedPersonName = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IssueId)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(Consequence)
            || string.IsNullOrWhiteSpace(RecommendedAction))
        {
            throw new ArgumentException("Offseason readiness issues require readable identity and guidance.");
        }
    }
}

public sealed record OffseasonRosterReadinessReport(
    OffseasonReadinessPhase Phase,
    DateOnly CampOpensOn,
    DateOnly OpeningNightOn,
    int DaysUntilCamp,
    int DaysUntilOpeningNight,
    int ActiveRosterCount,
    int OpeningRosterTarget,
    int UnsignedProspectCount,
    int OpenContractDecisionCount,
    int OpenPendingActionCount,
    int StaffVacancyCount,
    bool CapCompliant,
    string CapStatus,
    RosterValidationResult RosterValidationResult,
    IReadOnlyList<OffseasonRosterReadinessIssue> Issues,
    string Summary)
{
    public bool IsRosterCompliant => RosterValidationResult.IsValid;

    public bool IsReadyForCamp => CapCompliant && OpenPendingActionCount == 0;

    public bool IsReadyForOpeningNight => CapCompliant && IsRosterCompliant && OpenPendingActionCount == 0;

    public void Validate()
    {
        if (DaysUntilCamp < -10000 || DaysUntilOpeningNight < -10000
            || ActiveRosterCount < 0 || OpeningRosterTarget < 0
            || UnsignedProspectCount < 0 || OpenContractDecisionCount < 0
            || OpenPendingActionCount < 0 || StaffVacancyCount < 0
            || string.IsNullOrWhiteSpace(CapStatus)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Offseason readiness report contains invalid counts or summary text.");
        }

        foreach (var issue in Issues)
        {
            issue.Validate();
        }
    }
}

public sealed record OffseasonRosterReadinessResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    OffseasonRosterReadinessReport Report,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Summary)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Report.Validate();
        foreach (var item in InboxItems)
        {
            if (string.IsNullOrWhiteSpace(item.InboxItemId) || string.IsNullOrWhiteSpace(item.Title))
            {
                throw new ArgumentException("Offseason readiness inbox items require ids and titles.");
            }
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Offseason readiness result requires summary text.");
        }
    }
}
