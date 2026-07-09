using System.Globalization;
using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed class MonthlyGmSummaryService
{
    public MonthlyGmSummaryResult Generate(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var month = scenario.CurrentDate.Month;
        var year = scenario.CurrentDate.Year;
        var existing = scenario.MonthlySummaries.FirstOrDefault(summary => summary.Year == year && summary.Month == month);
        if (existing is not null)
        {
            return Result(scenario, existing, Array.Empty<AlphaInboxItem>(), false, $"Monthly GM Summary for {existing.MonthName} already exists.");
        }

        var summary = BuildSummary(registry, scenario, year, month);
        var archive = scenario.MonthlySummaries.Concat(new[] { summary }).ToArray();
        var updated = scenario with { MonthlySummaries = archive };
        updated.Validate();

        QueueEvent(registry, updated, summary);
        var inbox = new[]
        {
            new AlphaInboxItem(
                InboxItemId: $"inbox:monthly-summary:{year:D4}-{month:D2}",
                Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 18, 0, 0, TimeSpan.Zero),
                EventType: LegacyEventType.MonthlyGmSummaryCreated,
                Severity: LegacyEventSeverity.Notice,
                Title: $"Monthly GM Summary: {summary.MonthName}",
                Summary: summary.ExecutiveNarrative,
                PrimaryPersonId: null)
        };

        return Result(updated, summary, inbox, true, $"Monthly GM Summary for {summary.MonthName} is ready.");
    }

    private static MonthlyGmSummary BuildSummary(EngineRegistry registry, NewGmScenarioSnapshot scenario, int year, int month)
    {
        var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
        var teamId = scenario.Organization.OrganizationId;
        var monthRecaps = scenario.GameRecaps
            .Where(recap => recap.Date.Year == year
                && recap.Date.Month == month
                && (recap.BoxScore.Home.OrganizationId == teamId || recap.BoxScore.Away.OrganizationId == teamId))
            .ToArray();
        var wins = monthRecaps.Count(recap => recap.WinnerOrganizationId == teamId);
        var losses = Math.Max(0, monthRecaps.Length - wins);
        var monthRecord = $"{wins}-{losses}-0";
        var standing = scenario.Standings?.OrderedTeams()
            .Select((team, index) => new { Team = team, Rank = index + 1 })
            .FirstOrDefault(item => item.Team.OrganizationId == teamId);
        var overallRecord = standing is null
            ? "0-0-0"
            : $"{standing.Team.Wins}-{standing.Team.Losses}-{standing.Team.OvertimeLosses}, {standing.Team.Points} pts";
        var standingsPosition = standing is null
            ? "Standings unavailable"
            : $"{Ordinal(standing.Rank)} of {scenario.Standings!.Teams.Count}";
        var bestPlayer = scenario.PlayerStats.OrderByDescending(player => player.Points).ThenByDescending(player => player.Goals).FirstOrDefault();
        var strugglingPlayer = scenario.PlayerStats.OrderBy(player => player.Points).ThenBy(player => player.PlayerName, StringComparer.Ordinal).FirstOrDefault();
        var topGoalie = scenario.GoalieStats.OrderByDescending(goalie => goalie.Wins).ThenByDescending(goalie => goalie.SavePercentage).FirstOrDefault();
        var injuryConcern = scenario.AlphaSnapshot.Injuries.FirstOrDefault(injury => injury.Status is LegacyEngine.Injuries.InjuryStatus.Active or LegacyEngine.Injuries.InjuryStatus.Recovering);
        var recapInjury = monthRecaps.SelectMany(recap => recap.InjuryNotes).FirstOrDefault();
        var biggestInjuryConcern = injuryConcern is not null
            ? $"{PersonName(scenario, injuryConcern.PersonId)} remains on the medical list."
            : recapInjury ?? "No major injury concern.";
        var budget = new BudgetOverviewService().Build(scenario);
        var scoutingReports = scenario.CompletedScoutingReports.Count(report => report.CreatedOn.Year == year && report.CreatedOn.Month == month);
        var pending = scenario.PendingActions.Count(action => action.IsOpen);
        var rosterWarning = new SeasonReadinessService().Evaluate(registry, scenario).RosterReport.ValidationResult.Message;

        var ownerMood = wins >= losses
            ? "Ownership is pleased with the month."
            : "Ownership wants a steadier response next month.";
        var coachConcern = standing?.Team.GoalsAgainst > standing?.Team.GoalsFor
            ? "Coaching staff is concerned about defensive detail."
            : "Coaching staff sees a workable structure.";
        var scoutUpdate = scoutingReports == 0
            ? "Head scout has no completed reports this month."
            : $"Head scout delivered {scoutingReports} completed report(s).";
        var developmentUpdate = monthRecaps.SelectMany(recap => recap.DevelopmentNotes).FirstOrDefault()
            ?? "Routine development notes stayed internal; no major player development alert.";
        var storyItems = new StoryService().BuildExecutiveReportItems(scenario);
        var topStory = storyItems.TryGetValue("Top Story", out var storyHeadline) ? storyHeadline : "No major story yet.";
        var narrative = $"The {scenario.Organization.Name} finished {monthName} {monthRecord}. {ownerMood} {coachConcern} Top story: {topStory} Medical staff: {biggestInjuryConcern} Pending GM actions: {pending}.";

        var sections = new[]
        {
            new MonthlyGmSummarySection("Record", new[] { $"Monthly record: {monthRecord}", $"Overall record: {overallRecord}", $"Standings position: {standingsPosition}" }),
            new MonthlyGmSummarySection("Players", new[] { $"Best player: {bestPlayer?.PlayerName ?? "No skater stats yet"}", $"Struggling player: {strugglingPlayer?.PlayerName ?? "No skater stats yet"}", $"Top goalie: {topGoalie?.PlayerName ?? "No goalie stats yet"}" }),
            new MonthlyGmSummarySection("Owner / Staff", new[] { ownerMood, coachConcern, scoutUpdate }),
            new MonthlyGmSummarySection("Development / Medical", new[] { developmentUpdate, biggestInjuryConcern }),
            new MonthlyGmSummarySection("Storylines", storyItems.Take(4).Select(item => $"{item.Key}: {item.Value}").ToArray()),
            new MonthlyGmSummarySection("Roster / Budget", new[] { $"Roster warning: {rosterWarning}", $"Budget status: {budget.Status} - {budget.OwnerBudgetConfidence}" }),
            new MonthlyGmSummarySection("Decisions", new[] { $"Scouting reports completed: {scoutingReports}", $"Pending GM actions: {pending}" })
        };

        var summary = new MonthlyGmSummary(
            SummaryId: $"monthly-summary:{year:D4}-{month:D2}",
            Year: year,
            Month: month,
            MonthName: monthName,
            TeamRecordForMonth: monthRecord,
            OverallRecord: overallRecord,
            StandingsPosition: standingsPosition,
            BestPlayer: bestPlayer?.PlayerName ?? "No skater stats yet",
            StrugglingPlayer: strugglingPlayer?.PlayerName ?? "No skater stats yet",
            TopGoalie: topGoalie?.PlayerName ?? "No goalie stats yet",
            BiggestInjuryConcern: biggestInjuryConcern,
            OwnerMood: ownerMood,
            CoachConcern: coachConcern,
            HeadScoutUpdate: scoutUpdate,
            DevelopmentUpdate: developmentUpdate,
            RosterWarning: rosterWarning,
            BudgetStatus: $"{budget.Status}: {budget.OwnerBudgetConfidence}",
            ScoutingReportsCompleted: scoutingReports,
            PendingGmActions: pending,
            ExecutiveNarrative: narrative,
            Sections: sections);
        summary.Validate();
        return summary;
    }

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, MonthlyGmSummary summary)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 18, 0, 0, TimeSpan.Zero),
            LegacyEventType.MonthlyGmSummaryCreated,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            $"Monthly GM Summary: {summary.MonthName}",
            summary.ExecutiveNarrative,
            new LegacyEventContext(OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["month"] = summary.MonthName, ["record"] = summary.TeamRecordForMonth });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static MonthlyGmSummaryResult Result(
        NewGmScenarioSnapshot scenario,
        MonthlyGmSummary summary,
        IReadOnlyList<AlphaInboxItem> inbox,
        bool created,
        string message)
    {
        var result = new MonthlyGmSummaryResult(scenario, summary, inbox, created, message);
        result.Validate();
        return result;
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;

    private static string Ordinal(int number)
    {
        if ((number % 100) is 11 or 12 or 13)
        {
            return $"{number}th";
        }

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }
}
