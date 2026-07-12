using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

/// <summary>Builds presentation data from existing systems; it never advances or changes the world.</summary>
public sealed class DailyHockeyWorldService
{
    public DailyHockeyWorldSnapshot Build(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<ActionCenterItem> actionItems,
        IReadOnlyList<InboxMessage> inbox,
        IReadOnlyList<LeagueTransaction> transactions,
        Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(actionItems);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(transactions);

        var budget = new BudgetOverviewService().Build(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var cap = new SalaryCapService().BuildSnapshot(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var owner = new OwnerOfficeService().BuildSummary(scenario, budget);
        var organizationId = scenario.Organization.OrganizationId;
        var standing = scenario.Standings?.OrderedTeams().FirstOrDefault(team => team.OrganizationId == organizationId);
        var nextGame = scenario.Schedule?.NextGameFor(organizationId, scenario.CurrentDate);
        var activeInjuries = scenario.AlphaSnapshot.Injuries.Where(injury => injury.IsActive).ToArray();
        var expiring = scenario.PlayerRightsDecisions.Count(decision => decision.IsOpenDecision);
        var improving = scenario.DevelopmentRecommendations.Count(recommendation => recommendation.IsActive);
        var organizationCards = new[]
        {
            Card("organization-record", "Current record", standing is null ? "Season record pending." : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses} | {standing.Points} pts", "Season/Standings"),
            Card("organization-next-game", "Next game", nextGame is null ? "No game scheduled." : GameText(nextGame, organizationId), "Season/Schedule"),
            Card("organization-owner", "Owner mood", $"{owner.JobSecurity.Level} | confidence {owner.Confidence.Confidence}/100", "Organization/Owner", important: owner.JobSecurity.Level is JobSecurityLevel.HotSeat or JobSecurityLevel.Critical),
            Card("organization-cap", "Cap space", cap.IsEnabled ? $"{cap.AvailableCapSpace:C0} available | {cap.Status}" : "No salary cap under this rulebook.", "Organization/Budget", important: cap.Status == SalaryCapStatus.Violation),
            Card("organization-prospects", "Prospects improving", improving == 0 ? "No development priority today." : $"{improving} active development recommendation(s).", "Hockey Operations/Prospects"),
            Card("organization-medical", "Injuries", activeInjuries.Length == 0 ? "No active injuries." : $"{activeInjuries.Length} active injury concern(s).", "Hockey Operations/Roster", important: activeInjuries.Length > 0),
            Card("organization-contracts", "Expiring contracts", expiring == 0 ? "No open contract-rights decision." : $"{expiring} RFA/UFA review(s) remain.", "Hockey Operations/Contracts", important: expiring > 0)
        };

        var topStory = scenario.MediaFeed.Articles.OrderByDescending(article => article.Date).ThenByDescending(article => article.Importance).FirstOrDefault();
        var trade = transactions.Where(transaction => transaction.TransactionType == LeagueTransactionType.TradeCompleted).OrderByDescending(transaction => transaction.Date).FirstOrDefault();
        var signing = transactions.Where(transaction => transaction.Category == LeagueNewsCategory.Signings).OrderByDescending(transaction => transaction.Date).FirstOrDefault();
        var leader = scenario.Standings?.OrderedTeams().FirstOrDefault();
        var hotPlayer = scenario.PlayerStats.OrderByDescending(stat => stat.Points).ThenByDescending(stat => stat.Goals).FirstOrDefault();
        var coldPlayer = scenario.PlayerStats.OrderBy(stat => stat.PlusMinus).ThenBy(stat => stat.Points).FirstOrDefault();
        var pulse = new[]
        {
            Card("pulse-story", "Top story", topStory?.Headline ?? "No major league story today.", "Reports / History/Media"),
            Card("pulse-trade", "Biggest trade", trade?.Description ?? "No notable trade today.", "League/Transactions"),
            Card("pulse-signing", "Biggest signing", signing?.Description ?? "No notable signing today.", "League/League Free Agents"),
            Card("pulse-standings", "Standings movement", leader is null ? "Standings are waiting for games." : $"{leader.TeamName} leads with {leader.Points} points.", "Season/Standings"),
            Card("pulse-hot", "Hot player", hotPlayer is null ? "Season scoring is not underway." : $"{hotPlayer.PlayerName}: {hotPlayer.Points} points.", "Season/Stats", hotPlayer?.PersonId),
            Card("pulse-cold", "Cold player", coldPlayer is null ? "No cold streak to flag yet." : $"{coldPlayer.PlayerName}: {coldPlayer.PlusMinus:+#;-#;0} plus/minus.", "Season/Stats", coldPlayer?.PersonId)
        };

        var todayActions = actionItems
            .Where(action => action.Status == ActionCenterStatus.Open)
            .OrderByDescending(action => action.Priority)
            .ThenBy(action => action.DueDate ?? DateOnly.MaxValue)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .Take(3)
            .Select(action => Card($"action:{action.ActionCenterItemId}", action.Title, action.RecommendedAction, "Dashboard/Action Center", action.RelatedPersonId, action.RelatedTeamId, action.Priority is ActionCenterPriority.Important or ActionCenterPriority.Urgent, action.Priority == ActionCenterPriority.Urgent))
            .ToArray();

        var briefing = (scenario.OnboardingPlan?.AssistantGmBriefing is { } inherited
                ? new[] { inherited.RecommendedFirstAction, inherited.BiggestRosterNeed, inherited.KeyContractDecision }
                : actionItems.Where(action => action.Status == ActionCenterStatus.Open).OrderByDescending(action => action.Priority).Select(action => action.RecommendedAction))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        var coach = scenario.CurrentGameUsage?.CoachRecommendations.FirstOrDefault(recommendation => recommendation.IsImportant)?.SuggestedAction
            ?? scenario.CurrentTactics?.Recommendations.FirstOrDefault(recommendation => recommendation.IsImportant)?.SuggestedAction
            ?? scenario.CurrentLineChemistry?.Overall.Recommendation
            ?? "Coaching has no urgent lineup adjustment today.";
        var report = scenario.CompletedScoutingReports.OrderByDescending(item => item.CreatedOn).FirstOrDefault();
        var upcomingScout = scenario.ScoutingOperations.Where(item => item.IsOpen).OrderBy(item => item.ExpectedReportDate).FirstOrDefault();
        var scout = report is not null
            ? $"New report: {PersonName(scenario, report.PlayerId)} - {report.Recommendation}."
            : upcomingScout is not null
                ? $"{upcomingScout.ScoutName} is due back on {upcomingScout.ExpectedReportDate:MMM d} from {upcomingScout.TargetName}."
                : "No scouting report is due today.";
        var medicalSummary = new MedicalHealthService().BuildMedicalSummary(scenario);
        var medical = medicalSummary.ActiveInjuries == 0
            ? "Medical staff report no active injury concern."
            : $"{medicalSummary.ActiveInjuries} active injury concern(s). {medicalSummary.MostSignificantInjury}.";

        var leagueSnapshot = new[]
        {
            Card("league-games", "Today's games", $"{scenario.Schedule?.GamesOn(scenario.CurrentDate).Count ?? 0} game(s) scheduled.", "Season/Schedule"),
            Card("league-leader", "Top scorer", hotPlayer is null ? "Leader pending." : $"{hotPlayer.PlayerName}: {hotPlayer.Points} points.", "Season/Stats", hotPlayer?.PersonId),
            Card("league-goalie", "Top goalie", GoalieText(scenario), "Season/Stats"),
            Card("league-wire", "Recent transactions", transactions.Count == 0 ? "No new league transaction." : $"{transactions.Count} transaction(s) on the wire.", "Inbox/League News / Transaction Wire")
        };
        var prospects = scenario.ProspectRights
            .OrderBy(record => record.RoundNumber).ThenBy(record => record.PickNumber)
            .Take(2)
            .Select(record => Card($"prospect:{record.ProspectPersonId}", record.ProspectName, record.ProjectionText, "Hockey Operations/Prospects", record.ProspectPersonId))
            .Concat(scenario.DevelopmentRecommendations.Where(item => item.IsActive).Take(2).Select(item => Card($"development:{item.RecommendationId}", item.PlayerName, item.RecommendedAction, "Hockey Operations/Prospects", item.PersonId)))
            .Take(3)
            .ToArray();
        if (prospects.Length == 0)
        {
            prospects = new[] { Card("prospect-watch-none", "Prospect watch", "No prospect movement needs attention today.", "Hockey Operations/Prospects") };
        }

        var wire = transactions.OrderByDescending(item => item.Date).Take(3)
            .Select(item => Card($"wire:{item.TransactionId}", item.TeamName, item.Description, "Inbox/League News / Transaction Wire", item.PersonId, item.OrganizationId))
            .ToArray();
        if (wire.Length == 0)
        {
            wire = new[] { Card("wire-none", "Transaction wire", "No notable league transaction today.", "Inbox/League News / Transaction Wire") };
        }

        var schedule = (scenario.Schedule?.Games.Where(game => game.Date >= scenario.CurrentDate).OrderBy(game => game.Date).Take(5) ?? Array.Empty<ScheduledGame>())
            .Select(game => Card($"schedule:{game.GameId}", game.Date.ToString("MMM d"), GameText(game, organizationId), "Season/Schedule"))
            .ToArray();
        if (schedule.Length == 0)
        {
            schedule = new[] { Card("schedule-none", "Schedule", "No game is scheduled in the next five days.", "Season/Schedule") };
        }
        var calendar = scenario.Season.Calendar.Milestones.Where(item => item.Date.Value >= scenario.CurrentDate).OrderBy(item => item.Date.Value).Take(5)
            .Select(item => Card($"calendar:{item.Type}", item.Label, item.Date.Value.ToString("MMM d, yyyy"), "Season/Season Readiness", important: item.Date.Value <= scenario.CurrentDate.AddDays(14)))
            .ToArray();
        var playerOfDay = ProspectOrPlayerOfDay(scenario, hotPlayer);
        var teamOfDay = leader is null
            ? Card("team-of-day", "Team of the day", "League results are still building.", "League/Teams")
            : Card($"team:{leader.OrganizationId}", leader.TeamName, $"League leader with {leader.Points} points.", "League/Teams", null, leader.OrganizationId);

        var latestBriefing = scenario.DailyBriefings
            .OrderByDescending(item => item.CurrentDate)
            .ThenByDescending(item => item.GeneratedAt)
            .FirstOrDefault();
        var snapshot = new DailyHockeyWorldSnapshot(scenario.CurrentDate, organizationCards, pulse, todayActions, briefing, coach, scout, medical, leagueSnapshot, prospects, wire, schedule, calendar, playerOfDay, teamOfDay, latestBriefing);
        snapshot.Validate();
        return snapshot;
    }

    public DailyBriefingRecord CreateBriefing(
        NewGmScenarioSnapshot previousScenario,
        NewGmScenarioSnapshot currentScenario,
        FirstMonthAdvanceResult advance,
        IReadOnlyList<ActionCenterItem> actionItems,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        IReadOnlyList<LeagueTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(previousScenario);
        ArgumentNullException.ThrowIfNull(currentScenario);
        ArgumentNullException.ThrowIfNull(advance);

        var standing = currentScenario.Standings?.OrderedTeams()
            .FirstOrDefault(item => item.OrganizationId == currentScenario.Organization.OrganizationId);
        var record = standing is null
            ? "Season record pending."
            : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses} | {standing.Points} pts";
        var headline = currentScenario.MediaFeed.Articles
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Importance)
            .Select(item => item.Headline)
            .FirstOrDefault()
            ?? "No major league headline during this period.";
        var periodRecaps = currentScenario.GameRecaps
            .Where(recap => recap.Date > previousScenario.CurrentDate && recap.Date <= currentScenario.CurrentDate)
            .Where(recap => recap.BoxScore.Home.OrganizationId == currentScenario.Organization.OrganizationId || recap.BoxScore.Away.OrganizationId == currentScenario.Organization.OrganizationId)
            .Select(recap => recap.NarrativeSummary);
        var importantInbox = inboxItems
            .Where(item => item.Severity is LegacyEngine.Events.LegacyEventSeverity.Warning or LegacyEngine.Events.LegacyEventSeverity.Critical)
            .Select(item => item.Title)
            .Concat(inboxItems.Take(3).Select(item => item.Title));
        var items = periodRecaps
            .Concat(transactions.Take(3).Select(item => item.Description))
            .Concat(importantInbox)
            .Append(advance.MonthlySummary?.ExecutiveNarrative ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToArray();
        if (items.Length == 0)
        {
            items = new[] { advance.StopReason };
        }

        var actionCount = actionItems.Count(item => item.Status == ActionCenterStatus.Open && item.Priority is ActionCenterPriority.Important or ActionCenterPriority.Urgent);
        var output = new DailyBriefingRecord(
            BriefingId: $"daily-briefing:{previousScenario.CurrentDate:yyyyMMdd}:{currentScenario.CurrentDate:yyyyMMdd}:{advance.DaysAdvanced}",
            PreviousDate: previousScenario.CurrentDate,
            CurrentDate: currentScenario.CurrentDate,
            DaysAdvanced: advance.DaysAdvanced,
            StopReason: advance.StopReason,
            TeamRecord: record,
            TopHeadline: headline,
            ImportantActionCount: actionCount,
            ImportantItems: items,
            SourceEventIds: inboxItems.Select(item => item.InboxItemId).Concat(transactions.Select(item => item.TransactionId)).Distinct(StringComparer.Ordinal).Take(12).ToArray(),
            GeneratedAt: new DateTimeOffset(currentScenario.CurrentDate.Year, currentScenario.CurrentDate.Month, currentScenario.CurrentDate.Day, 8, 0, 0, TimeSpan.Zero));
        output.Validate();
        return output;
    }

    public NewGmScenarioSnapshot MergeBriefing(NewGmScenarioSnapshot scenario, DailyBriefingRecord briefing)
    {
        var existing = scenario.DailyBriefings.FirstOrDefault(item => item.BriefingId == briefing.BriefingId);
        if (existing is not null)
        {
            return scenario;
        }

        return scenario with
        {
            DailyBriefings = scenario.DailyBriefings
                .Append(briefing)
                .OrderByDescending(item => item.CurrentDate)
                .ThenByDescending(item => item.GeneratedAt)
                .Take(180)
                .ToArray()
        };
    }

    private static DailyHockeyWorldCard Card(string id, string title, string summary, string destination, string? personId = null, string? organizationId = null, bool important = false, bool urgent = false) =>
        new(id, title, summary, destination, personId, organizationId, important, urgent);

    private static string GameText(ScheduledGame game, string playerOrganizationId)
    {
        var playerIsHome = game.HomeOrganizationId == playerOrganizationId;
        var opponent = playerIsHome ? game.AwayOrganizationId : game.HomeOrganizationId;
        return playerIsHome ? $"vs {opponent}" : $"at {opponent}";
    }

    private static string GoalieText(NewGmScenarioSnapshot scenario)
    {
        var goalie = scenario.GoalieStats.OrderByDescending(item => item.Wins).ThenByDescending(item => item.SavePercentage).FirstOrDefault();
        return goalie is null ? "Goalie leader pending." : $"{goalie.PlayerName}: {goalie.Wins} wins, {goalie.SavePercentage:P1}.";
    }

    private static DailyHockeyWorldCard ProspectOrPlayerOfDay(NewGmScenarioSnapshot scenario, PlayerSeasonStatLine? hotPlayer)
    {
        if (hotPlayer is not null)
        {
            return Card($"player-of-day:{hotPlayer.PersonId}", hotPlayer.PlayerName, $"Player of the day: {hotPlayer.Points} points this season.", "Hockey Operations/Roster", hotPlayer.PersonId);
        }

        var prospect = scenario.ProspectRights.OrderBy(item => item.RoundNumber).ThenBy(item => item.PickNumber).FirstOrDefault();
        return prospect is null
            ? Card("player-of-day", "Player of the day", "No player spotlight is available yet.", "Hockey Operations/Roster")
            : Card($"player-of-day:{prospect.ProspectPersonId}", prospect.ProspectName, prospect.ProjectionText, "Hockey Operations/Prospects", prospect.ProspectPersonId);
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;
}
