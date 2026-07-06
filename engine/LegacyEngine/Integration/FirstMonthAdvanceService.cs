using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed class FirstMonthAdvanceService
{
    private readonly DailySimulationCoordinator _coordinator = new();
    private readonly MonthlyGmSummaryService _monthlySummaries = new();

    public FirstMonthAdvanceResult AdvanceDays(EngineRegistry registry, NewGmScenarioSnapshot scenario, int days) =>
        Advance(registry, scenario, Math.Max(1, days), FirstMonthAdvanceMode.FixedDays);

    public FirstMonthAdvanceResult AdvanceToNextGame(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var next = new SeasonFrameworkService().NextGame(scenario);
        var days = next is null ? 31 : Math.Max(1, next.Date.DayNumber - scenario.CurrentDate.DayNumber);
        return Advance(registry, scenario, days, FirstMonthAdvanceMode.NextGame);
    }

    public FirstMonthAdvanceResult AdvanceToMonthEnd(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var lastDay = DateTime.DaysInMonth(scenario.CurrentDate.Year, scenario.CurrentDate.Month);
        var monthEnd = new DateOnly(scenario.CurrentDate.Year, scenario.CurrentDate.Month, lastDay);
        var days = Math.Max(1, monthEnd.DayNumber - scenario.CurrentDate.DayNumber);
        return Advance(registry, scenario, days, FirstMonthAdvanceMode.MonthEnd);
    }

    private FirstMonthAdvanceResult Advance(EngineRegistry registry, NewGmScenarioSnapshot scenario, int maxDays, FirstMonthAdvanceMode mode)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var urgent = UrgentPendingActions(scenario);
        if (urgent.Count > 0)
        {
            return Result(scenario, 0, 0, Array.Empty<AlphaInboxItem>(), $"Stopped because urgent pending action needs review: {urgent[0].Title}.", true);
        }

        var inbox = new List<AlphaInboxItem>();
        var processed = 0;
        var current = scenario;
        MonthlyGmSummary? monthlySummary = null;

        for (var day = 0; day < maxDays; day++)
        {
            var previousRecapCount = current.GameRecaps.Count;
            var previousScoutingReports = current.CompletedScoutingReports.Count;
            var previousInjuryNotes = current.GameRecaps.SelectMany(recap => recap.InjuryNotes).Count();
            var daily = _coordinator.AdvanceScenarioOneDay(registry, current);
            current = daily.ScenarioSnapshot;
            inbox.AddRange(daily.InboxItems);
            processed += daily.SimulationResult.ProcessedEventCount;

            var newRecaps = current.GameRecaps.Skip(previousRecapCount).ToArray();
            var playerRecap = newRecaps.FirstOrDefault(recap =>
                recap.BoxScore.Home.OrganizationId == current.Organization.OrganizationId
                || recap.BoxScore.Away.OrganizationId == current.Organization.OrganizationId);
            if (current.GameRecaps.SelectMany(recap => recap.InjuryNotes).Count() > previousInjuryNotes)
            {
                var note = current.GameRecaps.SelectMany(recap => recap.InjuryNotes).Last();
                return Result(current, day + 1, processed, inbox, $"Stopped because {note}", true);
            }

            if (playerRecap is not null)
            {
                var opponent = playerRecap.BoxScore.Home.OrganizationId == current.Organization.OrganizationId
                    ? playerRecap.BoxScore.Away.TeamName
                    : playerRecap.BoxScore.Home.TeamName;
                return Result(current, day + 1, processed, inbox, $"Stopped because {current.Organization.Name} played {opponent}.", true);
            }

            if (current.CompletedScoutingReports.Count > previousScoutingReports)
            {
                return Result(current, day + 1, processed, inbox, "Stopped because a major scouting report was completed.", true);
            }

            if (daily.InboxItems.Any(item => item.EventType == LegacyEventType.PlayerInjured || item.Severity == LegacyEventSeverity.Critical))
            {
                var item = daily.InboxItems.First(item => item.EventType == LegacyEventType.PlayerInjured || item.Severity == LegacyEventSeverity.Critical);
                return Result(current, day + 1, processed, inbox, $"Stopped because {item.Title}.", true);
            }

            urgent = UrgentPendingActions(current);
            if (urgent.Count > 0)
            {
                return Result(current, day + 1, processed, inbox, $"Stopped because urgent pending action needs review: {urgent[0].Title}.", true);
            }

            var rosterReport = new SeasonReadinessService().Evaluate(registry, current).RosterReport;
            if (!rosterReport.ValidationResult.IsValid)
            {
                return Result(current, day + 1, processed, inbox, $"Stopped because roster is invalid: {rosterReport.ValidationResult.Message}", true);
            }

            if (ShouldGenerateMonthEnd(current, mode))
            {
                var monthly = _monthlySummaries.Generate(registry, current);
                current = monthly.ScenarioSnapshot;
                inbox.AddRange(monthly.InboxItems);
                monthlySummary = monthly.Summary;
                return Result(current, day + 1, processed, inbox, "Stopped because monthly report is ready.", true, monthlySummary);
            }
        }

        var reason = mode switch
        {
            FirstMonthAdvanceMode.NextGame => "Advance stopped because no player-team game was reached in the search window.",
            FirstMonthAdvanceMode.MonthEnd => "Advance stopped before month end because the requested window ended.",
            _ => $"Advanced {maxDays} day(s)."
        };
        return Result(current, maxDays, processed, inbox, reason, false, monthlySummary);
    }

    public static IReadOnlyList<PendingGmAction> UrgentPendingActions(NewGmScenarioSnapshot scenario) =>
        scenario.PendingActions
            .Where(action => action.IsOpen && IsUrgent(action))
            .OrderBy(action => action.CreatedOn)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .ToArray();

    public static bool IsUrgent(PendingGmAction action) =>
        action.ActionType is PendingGmActionType.AddToRoster
            or PendingGmActionType.ReleasePlayer
            or PendingGmActionType.CutPlayer
            or PendingGmActionType.ApproveContract
            or PendingGmActionType.SignRecruit
            or PendingGmActionType.SignDraftPick
        || $"{action.Title} {action.Reason} {action.RecommendedAction}".Contains("urgent", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldGenerateMonthEnd(NewGmScenarioSnapshot scenario, FirstMonthAdvanceMode mode)
    {
        if (mode != FirstMonthAdvanceMode.MonthEnd)
        {
            return false;
        }

        var lastDay = DateTime.DaysInMonth(scenario.CurrentDate.Year, scenario.CurrentDate.Month);
        return scenario.CurrentDate.Day == lastDay;
    }

    private static FirstMonthAdvanceResult Result(
        NewGmScenarioSnapshot scenario,
        int days,
        int processed,
        IReadOnlyList<AlphaInboxItem> inbox,
        string stopReason,
        bool stopped,
        MonthlyGmSummary? monthlySummary = null)
    {
        var result = new FirstMonthAdvanceResult(scenario, days, processed, inbox, stopReason, stopped, monthlySummary);
        result.Validate();
        return result;
    }
}
