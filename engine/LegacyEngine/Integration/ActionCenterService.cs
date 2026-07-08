using LegacyEngine.Events;
using LegacyEngine.Injuries;

namespace LegacyEngine.Integration;

public sealed class ActionCenterService
{
    public IReadOnlyList<ActionCenterItem> BuildItems(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<InboxMessage> inboxMessages,
        BudgetSnapshot budget,
        SeasonReadinessReport readiness,
        IReadOnlyList<StaffVacancy> staffVacancies,
        IReadOnlyDictionary<string, ActionCenterStatus>? statusOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(inboxMessages);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(staffVacancies);

        var items = new List<ActionCenterItem>();
        AddPendingActions(scenario, items);
        AddPendingFreeAgentResponses(scenario, items);
        AddUrgentInbox(scenario, inboxMessages, items);
        AddRosterWarnings(scenario, readiness, items);
        AddStaffVacancies(staffVacancies, items);
        AddBudgetWarnings(budget, items);
        AddScoutingCompletions(scenario, items);
        AddDevelopmentRecommendations(scenario, items);
        AddStaffCoachingItems(scenario, items);
        AddOwnerOfficeItems(scenario, budget, items);
        AddUpcomingGames(scenario, items);
        AddInjuryIssues(scenario, items);
        AddSeasonReadiness(readiness, items);
        AddTradeDeadlineItems(scenario, items);
        AddOffseasonChecklist(scenario, items);
        AddPlayerLifeCycleItems(scenario, items);
        AddStaffLifeCycleItems(scenario, items);
        AddOwnerLifeCycleItems(scenario, items);

        var output = items
            .GroupBy(item => item.ActionCenterItemId, StringComparer.Ordinal)
            .Select(group => ApplyStatus(group.First(), statusOverrides))
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => item.DueDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ToArray();

        foreach (var item in output)
        {
            item.Validate();
        }

        return output;
    }

    public IReadOnlyList<string> BuildDailyAgenda(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<ActionCenterItem> items,
        BudgetSnapshot budget)
    {
        var lines = new List<string> { "Good morning, GM." };
        var todayGame = scenario.Schedule?.Games.FirstOrDefault(game => game.Date == scenario.CurrentDate && InvolvesPlayerTeam(scenario, game));
        if (todayGame is not null)
        {
            lines.Add($"Game vs {OpponentName(scenario, todayGame)}.");
        }

        var contractActions = items.Count(item => item.Status == ActionCenterStatus.Open && item.Category == ActionCenterCategory.Contracts);
        if (contractActions > 0)
        {
            lines.Add($"{contractActions} pending contract decision(s).");
        }

        var scouting = items.FirstOrDefault(item => item.Status == ActionCenterStatus.Open && item.Category == ActionCenterCategory.Scouting);
        if (scouting is not null)
        {
            lines.Add(scouting.Title);
        }

        var development = items.FirstOrDefault(item => item.Status == ActionCenterStatus.Open && item.Category == ActionCenterCategory.PlayerDevelopment);
        if (development is not null)
        {
            lines.Add(development.Title);
        }

        lines.Add(items.Any(item => item.Status == ActionCenterStatus.Open && item.Category == ActionCenterCategory.Roster)
            ? "Roster needs attention."
            : "Roster is compliant.");
        lines.Add($"Budget is {budget.Status}.");
        return lines;
    }

    public IReadOnlyList<string> BuildAssistantGmRecommendations(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<ActionCenterItem> items,
        BudgetSnapshot budget)
    {
        var recommendations = new List<string>();
        var top = items.FirstOrDefault(item => item.Status == ActionCenterStatus.Open && item.Priority == ActionCenterPriority.Urgent)
            ?? items.FirstOrDefault(item => item.Status == ActionCenterStatus.Open && item.Priority == ActionCenterPriority.Important)
            ?? items.FirstOrDefault(item => item.Status == ActionCenterStatus.Open);
        if (top is not null)
        {
            recommendations.Add(top.RecommendedAction);
        }

        if (scenario.ScoutingOperations.All(assignment => !assignment.IsOpen))
        {
            recommendations.Add("Assign an available scout before advancing.");
        }

        var development = items.FirstOrDefault(item => item.Status == ActionCenterStatus.Open && item.Category == ActionCenterCategory.PlayerDevelopment);
        if (development is not null)
        {
            recommendations.Add(development.RecommendedAction);
        }

        recommendations.Add(items.Any(item => item.Status == ActionCenterStatus.Open && item.Category == ActionCenterCategory.Roster)
            ? "Review roster warnings before opening night."
            : "Roster is compliant for opening night.");

        if (budget.Status is BudgetStatus.NearLimit or BudgetStatus.OverBudget)
        {
            recommendations.Add("Budget is near limit; avoid expensive staff hires.");
        }

        return recommendations.Distinct(StringComparer.Ordinal).Take(4).ToArray();
    }

    public IReadOnlyList<string> BuildUpcomingEvents(NewGmScenarioSnapshot scenario)
    {
        var events = new List<string>();
        var nextGame = scenario.Schedule?.NextGameFor(scenario.Organization.OrganizationId, scenario.CurrentDate);
        if (nextGame is not null)
        {
            events.Add($"Next game: {nextGame.Date:yyyy-MM-dd} vs {OpponentName(scenario, nextGame)}.");
        }

        var scoutReturn = scenario.ScoutingOperations
            .Where(assignment => assignment.IsOpen)
            .OrderBy(assignment => assignment.ReturnDate ?? assignment.ExpectedReportDate)
            .FirstOrDefault();
        if (scoutReturn is not null)
        {
            events.Add($"Scout return: {(scoutReturn.ReturnDate ?? scoutReturn.ExpectedReportDate):yyyy-MM-dd} - {scoutReturn.TargetName}.");
        }

        var pending = scenario.PendingActions.Where(action => action.IsOpen).OrderBy(action => action.CreatedOn).FirstOrDefault();
        if (pending is not null)
        {
            events.Add($"Contract/decision deadline: {pending.CreatedOn.AddDays(7):yyyy-MM-dd} - {pending.Title}.");
        }

        if (scenario.CurrentDate <= scenario.DraftDate)
        {
            events.Add($"Draft milestone: {scenario.DraftDate:yyyy-MM-dd}.");
        }

        if (scenario.TrainingCamp is not null)
        {
            events.Add($"Training camp: opened {scenario.TrainingCamp.OpenedOn:yyyy-MM-dd}; roster deadline {scenario.Season.Calendar.SeasonStart.Value:yyyy-MM-dd}.");
        }

        var deadline = new TradeDeadlineService().GetWindow(scenario);
        if (deadline.Status != TradeDeadlineStatus.NotStarted)
        {
            events.Add($"Trade deadline: {deadline.Summary}");
        }

        events.Add($"Month-end report: {new DateOnly(scenario.CurrentDate.Year, scenario.CurrentDate.Month, DateTime.DaysInMonth(scenario.CurrentDate.Year, scenario.CurrentDate.Month)):yyyy-MM-dd}.");
        return events.Take(6).ToArray();
    }

    public ActionCenterItem ApplyStatus(ActionCenterItem item, ActionCenterStatus status) =>
        item with { Status = status };

    private static void AddPendingActions(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        foreach (var action in scenario.PendingActions.Where(action => action.IsOpen))
        {
            items.Add(new ActionCenterItem(
                $"action-center:pending:{action.ActionId}",
                action.Title,
                CategoryFor(action.ActionType),
                PriorityFor(action.ActionType),
                action.CreatedOn.AddDays(7),
                action.PersonId,
                action.PersonName,
                action.OrganizationId,
                scenario.Organization.Name,
                action.Reason,
                "Nothing changes until the GM approves or declines this decision.",
                action.RecommendedAction,
                null,
                null,
                action.ActionId));
        }
    }

    private static void AddPendingFreeAgentResponses(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        if (scenario.FreeAgencyMarketState is null)
        {
            return;
        }

        foreach (var offer in scenario.FreeAgencyMarketState.OfferStates.Where(offer => offer.IsPendingResponse))
        {
            var personName = PersonName(scenario, offer.PersonId);
            items.Add(new ActionCenterItem(
                $"action-center:free-agency-response:{offer.OfferStateId}",
                $"Free-agent response due: {personName}",
                ActionCenterCategory.Contracts,
                offer.ResponseDate <= scenario.CurrentDate ? ActionCenterPriority.Important : ActionCenterPriority.Normal,
                offer.ResponseDate,
                offer.PersonId,
                personName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                offer.Explanation,
                "The player is weighing your offer against market pressure and competing clubs.",
                "Review Free Agents and be ready to approve, revise, or move on when the response arrives.",
                null,
                null,
                null));
        }
    }

    private static void AddStaffCoachingItems(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        var service = new StaffCoachingService();
        foreach (var warning in service.BuildStaffChemistry(scenario).Where(link => link.IsWarning).Take(2))
        {
            items.Add(new ActionCenterItem(
                $"action-center:staff-chemistry:{warning.FromPersonId}:{warning.ToPersonId}",
                "Staff chemistry warning",
                ActionCenterCategory.Staff,
                ActionCenterPriority.Important,
                scenario.CurrentDate.AddDays(7),
                warning.FromPersonId,
                warning.FromName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                warning.Summary,
                "Poor staff chemistry can reduce communication quality, delay recommendations, and weaken player-development follow-through.",
                "Review the staff profile, clarify responsibilities, or consider a role/focus change.",
                null,
                null,
                null));
        }

        var meeting = service.GenerateMonthlyMeetingReport(scenario);
        var topRecommendation = meeting.Recommendations.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(topRecommendation))
        {
            items.Add(new ActionCenterItem(
                $"action-center:staff-meeting:{scenario.CurrentDate:yyyyMM}",
                "Monthly staff meeting review",
                ActionCenterCategory.Staff,
                ActionCenterPriority.Normal,
                new DateOnly(scenario.CurrentDate.Year, scenario.CurrentDate.Month, DateTime.DaysInMonth(scenario.CurrentDate.Year, scenario.CurrentDate.Month)),
                null,
                null,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                meeting.Summary,
                "Staff recommendations help connect roster decisions, development plans, scouting information, and medical context.",
                topRecommendation,
                null,
                null,
                null));
        }
    }

    private static void AddOwnerOfficeItems(NewGmScenarioSnapshot scenario, BudgetSnapshot budget, List<ActionCenterItem> items)
    {
        var ownerOffice = new OwnerOfficeService().BuildSummary(scenario, budget);
        var nextMeeting = ownerOffice.Meetings
            .Where(meeting => meeting.ScheduledDate >= scenario.CurrentDate)
            .OrderBy(meeting => meeting.ScheduledDate)
            .FirstOrDefault();
        if (nextMeeting is not null && nextMeeting.ScheduledDate.DayNumber - scenario.CurrentDate.DayNumber <= 7)
        {
            items.Add(new ActionCenterItem(
                $"action-center:owner-meeting:{nextMeeting.MeetingId}",
                nextMeeting.ScheduledDate == scenario.CurrentDate ? $"Owner meeting today: {nextMeeting.MeetingType}" : $"Owner meeting upcoming: {nextMeeting.MeetingType}",
                ActionCenterCategory.Owner,
                nextMeeting.MeetingType is OwnerMeetingType.BudgetReview or OwnerMeetingType.TradeDeadline ? ActionCenterPriority.Important : ActionCenterPriority.Normal,
                nextMeeting.ScheduledDate,
                scenario.AlphaSnapshot.Owner.OwnerId,
                scenario.AlphaSnapshot.Owner.Name,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                nextMeeting.OwnerComments,
                "Owner meetings can reset expectations, budget pressure, and GM accountability.",
                nextMeeting.Recommendations.First(),
                null,
                null,
                null));
        }

        var warningLetter = ownerOffice.Letters.FirstOrDefault(letter => letter.IsWarning);
        if (warningLetter is not null)
        {
            items.Add(new ActionCenterItem(
                $"action-center:owner-letter:{warningLetter.LetterId}",
                $"Owner letter: {warningLetter.Subject}",
                ActionCenterCategory.Owner,
                ActionCenterPriority.Important,
                warningLetter.Date,
                scenario.AlphaSnapshot.Owner.OwnerId,
                scenario.AlphaSnapshot.Owner.Name,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                warningLetter.Body,
                "Owner letters become part of the GM accountability trail.",
                "Read the Owner workspace and adjust budget, roster, or development priorities before the next review.",
                null,
                null,
                null));
        }

        if (ownerOffice.JobSecurity.Level is JobSecurityLevel.HotSeat or JobSecurityLevel.Critical or JobSecurityLevel.Questioned)
        {
            items.Add(new ActionCenterItem(
                "action-center:owner:job-security",
                $"Job security: {ownerOffice.JobSecurity.Level}",
                ActionCenterCategory.Owner,
                ownerOffice.JobSecurity.Level == JobSecurityLevel.Questioned ? ActionCenterPriority.Important : ActionCenterPriority.Urgent,
                scenario.CurrentDate.AddDays(7),
                scenario.AlphaSnapshot.Owner.OwnerId,
                scenario.AlphaSnapshot.Owner.Name,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                ownerOffice.JobSecurity.Explanation,
                "Alpha 4.8 creates warnings only; it does not fire the GM automatically.",
                ownerOffice.PerformanceReview.Recommendation,
                null,
                null,
                null));
        }

        var decision = ownerOffice.Decisions.FirstOrDefault(decision => decision.RequiresGmAttention);
        if (decision is not null)
        {
            items.Add(new ActionCenterItem(
                $"action-center:owner-decision:{decision.DecisionType}",
                $"Owner direction: {decision.DecisionType}",
                decision.DecisionType is OwnerDecisionType.FreezeHiring or OwnerDecisionType.ReduceBudget ? ActionCenterCategory.Budget : ActionCenterCategory.Owner,
                ActionCenterPriority.Important,
                scenario.CurrentDate.AddDays(14),
                scenario.AlphaSnapshot.Owner.OwnerId,
                scenario.AlphaSnapshot.Owner.Name,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                decision.Reason,
                decision.Impact,
                "Review Owner expectations and adjust the hockey operations plan.",
                null,
                null,
                null));
        }
    }

    private static void AddUrgentInbox(NewGmScenarioSnapshot scenario, IReadOnlyList<InboxMessage> inboxMessages, List<ActionCenterItem> items)
    {
        foreach (var message in inboxMessages.Where(message => message.IsImportant && !message.IsArchived && !message.IsDeleted).Take(8))
        {
            var personName = string.IsNullOrWhiteSpace(message.Item.PrimaryPersonId)
                ? null
                : PersonName(scenario, message.Item.PrimaryPersonId);
            items.Add(new ActionCenterItem(
                $"action-center:inbox:{message.InboxItemId}",
                message.Item.Title,
                CategoryFor(message.Category),
                PriorityFor(message),
                DateOnly.FromDateTime(message.Item.Date.Date),
                message.Item.PrimaryPersonId,
                personName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                message.Item.Summary,
                "Important messages may represent owner, staff, league, or medical items that deserve review.",
                "Open the related inbox message and decide whether a follow-up is needed.",
                message.InboxItemId,
                null,
                null));
        }
    }

    private static void AddRosterWarnings(NewGmScenarioSnapshot scenario, SeasonReadinessReport readiness, List<ActionCenterItem> items)
    {
        var roster = readiness.RosterReport;
        if (!roster.ValidationResult.IsValid || roster.CurrentRosterSize != roster.RequiredRosterSize || roster.PlayersRequiringDecisions > 0 || roster.UnsignedPlayers > 0)
        {
            items.Add(new ActionCenterItem(
                "action-center:roster:readiness",
                "Roster needs review",
                ActionCenterCategory.Roster,
                ActionCenterPriority.Urgent,
                scenario.Season.Calendar.SeasonStart.Value,
                null,
                null,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                $"{readiness.RosterStatus}: {roster.ValidationResult.Message}",
                "Opening night may remain blocked if roster issues are unresolved.",
                "Review Hockey Operations roster, training camp, and pending roster decisions.",
                null,
                null,
                null));
        }
    }

    private static void AddStaffVacancies(IReadOnlyList<StaffVacancy> staffVacancies, List<ActionCenterItem> items)
    {
        foreach (var vacancy in staffVacancies)
        {
            items.Add(new ActionCenterItem(
                $"action-center:staff-vacancy:{vacancy.Role}",
                $"{vacancy.Role} vacancy",
                ActionCenterCategory.Staff,
                ActionCenterPriority.Important,
                null,
                null,
                null,
                null,
                null,
                vacancy.Warning,
                "Thin staff coverage can reduce scouting, medical, or coaching quality.",
                "Review Organization staff hiring and candidate recommendations.",
                null,
                null,
                null));
        }
    }

    private static void AddBudgetWarnings(BudgetSnapshot budget, List<ActionCenterItem> items)
    {
        if (budget.Status is BudgetStatus.UnderBudget)
        {
            return;
        }

        items.Add(new ActionCenterItem(
            "action-center:budget:status",
            $"Budget is {budget.Status}",
            ActionCenterCategory.Budget,
            budget.Status == BudgetStatus.OverBudget ? ActionCenterPriority.Urgent : ActionCenterPriority.Important,
            null,
            null,
            null,
            null,
            null,
            $"{budget.OwnerBudgetConfidence}. Remaining budget: {budget.RemainingBudget:C0}.",
            "Owner confidence can suffer if hockey operations spending is ignored.",
            "Review Organization budget before approving expensive staff or contract decisions.",
            null,
            null,
            null));
    }

    private static void AddScoutingCompletions(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        foreach (var assignment in scenario.ScoutingOperations.Where(assignment => assignment.Status == ScoutingOperationStatus.Completed).OrderByDescending(assignment => assignment.CompletedOn).Take(3))
        {
            items.Add(new ActionCenterItem(
                $"action-center:scouting:{assignment.AssignmentId}",
                $"Scout report returned: {assignment.TargetName}",
                ActionCenterCategory.Scouting,
                ActionCenterPriority.Important,
                assignment.CompletedOn,
                assignment.TargetPlayerId,
                assignment.TargetName,
                null,
                null,
                $"{assignment.ScoutName} completed a {assignment.Priority} priority assignment.",
                "Updated scouting information may affect draft, recruiting, or roster decisions.",
                "Review Scouting Operations and update the draft board or prospect plan if needed.",
                null,
                null,
                null));
        }
    }

    private static void AddDevelopmentRecommendations(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        foreach (var recommendation in scenario.DevelopmentRecommendations.Where(item => item.IsActive).OrderByDescending(item => item.CreatedOn).Take(5))
        {
            items.Add(new ActionCenterItem(
                $"action-center:development:{recommendation.RecommendationId}",
                $"Development review: {recommendation.PlayerName}",
                ActionCenterCategory.PlayerDevelopment,
                recommendation.RecommendationType is DevelopmentRecommendationType.ReadyForNextRole or DevelopmentRecommendationType.NeedsConfidence or DevelopmentRecommendationType.RecoveryPlan
                    ? ActionCenterPriority.Important
                    : ActionCenterPriority.Normal,
                recommendation.CreatedOn.AddDays(7),
                recommendation.PersonId,
                recommendation.PlayerName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                recommendation.Reason,
                "Development plans are affected by role, confidence, morale, coaching fit, injuries, and opportunity.",
                recommendation.RecommendedAction,
                null,
                null,
                null));
        }
    }

    private static void AddUpcomingGames(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        var nextGame = scenario.Schedule?.NextGameFor(scenario.Organization.OrganizationId, scenario.CurrentDate);
        if (nextGame is null)
        {
            return;
        }

        var daysAway = nextGame.Date.DayNumber - scenario.CurrentDate.DayNumber;
        if (daysAway <= 3)
        {
            items.Add(new ActionCenterItem(
                $"action-center:game:{nextGame.GameId}",
                daysAway == 0 ? $"Game day vs {OpponentName(scenario, nextGame)}" : $"Upcoming game vs {OpponentName(scenario, nextGame)}",
                ActionCenterCategory.GameDay,
                daysAway == 0 ? ActionCenterPriority.Important : ActionCenterPriority.Normal,
                nextGame.Date,
                null,
                null,
                nextGame.HomeOrganizationId,
                OpponentName(scenario, nextGame),
                "The next scheduled game is approaching.",
                "Roster, medical, and staff notes may matter before advancing.",
                "Review the dashboard, roster warnings, and schedule before advancing.",
                null,
                null,
                null));
        }
    }

    private static void AddInjuryIssues(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        var medical = new MedicalHealthService();
        foreach (var injury in scenario.AlphaSnapshot.Injuries.Where(injury => injury.IsActive).OrderByDescending(injury => injury.Severity).Take(3))
        {
            var report = medical.BuildMedicalReport(scenario, injury.PersonId);
            var name = report.PlayerName;
            var priority = injury.Severity is InjurySeverity.Major or InjurySeverity.Severe or InjurySeverity.CareerThreatening
                ? ActionCenterPriority.Urgent
                : report.ConditioningStatus == ConditioningStatus.NotGameReady ? ActionCenterPriority.Important : ActionCenterPriority.Normal;
            items.Add(new ActionCenterItem(
                $"action-center:medical:{injury.InjuryId}",
                report.ConditioningStatus == ConditioningStatus.GameReady ? $"Return decision required: {name}" : $"Medical review ready: {name}",
                ActionCenterCategory.Medical,
                priority,
                injury.ExpectedReturnDate,
                injury.PersonId,
                name,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                $"{report.PlayerName} ({report.Position}) - {report.WhyItMatters}",
                "Medical issues can affect availability, conditioning, development, and reinjury risk.",
                report.ReturnRecommendation,
                null,
                null,
                null));
        }

        foreach (var profile in medical.BuildHealthProfiles(scenario).Where(profile => profile.CurrentHealth == HealthStatus.HighRisk || profile.InjuryRisk >= 75).Take(2))
        {
            items.Add(new ActionCenterItem(
                $"action-center:medical-risk:{profile.PersonId}",
                $"High injury risk: {profile.PlayerName}",
                ActionCenterCategory.Medical,
                ActionCenterPriority.Important,
                scenario.CurrentDate.AddDays(7),
                profile.PersonId,
                profile.PlayerName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                $"{profile.PlayerName} ({profile.Position}) has injury risk {profile.InjuryRisk}/100 and wear {profile.WearAndTear}/100.",
                "High-risk players may need rest, conditioning, or medical follow-up to avoid long-term damage.",
                "Review the medical report before increasing workload.",
                null,
                null,
                null));
        }
    }

    private static void AddSeasonReadiness(SeasonReadinessReport readiness, List<ActionCenterItem> items)
    {
        if (readiness.CanBeginSeason)
        {
            return;
        }

        foreach (var item in readiness.ChecklistItems.Where(item => !item.IsComplete).Take(3))
        {
            items.Add(new ActionCenterItem(
                $"action-center:season-readiness:{item.Code}",
                item.Text,
                ActionCenterCategory.League,
                ActionCenterPriority.Important,
                null,
                null,
                null,
                null,
                null,
                readiness.BlockedReason,
                "Season start remains blocked until readiness items are complete.",
                "Review Season Readiness and resolve the blocked item.",
                null,
                null,
                null));
        }
    }

    private static void AddTradeDeadlineItems(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        var window = new TradeDeadlineService().GetWindow(scenario);
        if (window.Status == TradeDeadlineStatus.NotStarted)
        {
            return;
        }

        var priority = window.Status is TradeDeadlineStatus.DeadlineDay or TradeDeadlineStatus.Closed
            ? ActionCenterPriority.Urgent
            : window.Status == TradeDeadlineStatus.DeadlineWeek ? ActionCenterPriority.Important : ActionCenterPriority.Normal;
        items.Add(new ActionCenterItem(
            "action-center:trade-deadline:review",
            window.Status == TradeDeadlineStatus.Closed ? "Trade deadline closed" : "Review trade deadline plan",
            ActionCenterCategory.League,
            priority,
            window.DeadlineDate,
            null,
            null,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            window.Summary,
            window.Status == TradeDeadlineStatus.Closed ? "New trade proposals are locked." : "Deadline pressure can change the trade block, owner expectations, and budget decisions.",
            window.Status == TradeDeadlineStatus.Closed ? "Review completed trades and unresolved pending actions." : "Review Hockey Operations trades and resolve pending trade decisions.",
            null,
            null,
            null));

        if (scenario.PendingActions.Any(action => action.IsOpen && action.ActionType == PendingGmActionType.ApproveTrade))
        {
            items.Add(new ActionCenterItem(
                "action-center:trade-deadline:pending-trade",
                "Resolve pending accepted trade",
                ActionCenterCategory.Contracts,
                ActionCenterPriority.Urgent,
                window.DeadlineDate,
                null,
                null,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                "An accepted trade is waiting for GM approval.",
                "Accepted pre-deadline trades remain valid, but the roster plan is unclear until the GM decides.",
                "Approve or decline the pending trade in the Action Center.",
                null,
                null,
                scenario.PendingActions.First(action => action.IsOpen && action.ActionType == PendingGmActionType.ApproveTrade).ActionId));
        }
    }

    private static void AddOffseasonChecklist(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        if (scenario.SeasonRollover.Checklist.Count == 0)
        {
            return;
        }

        foreach (var checklistItem in scenario.SeasonRollover.Checklist.Take(5))
        {
            items.Add(new ActionCenterItem(
                $"action-center:offseason:{StableId(checklistItem)}",
                checklistItem,
                ActionCenterCategory.League,
                checklistItem.Contains("contract", StringComparison.OrdinalIgnoreCase) ? ActionCenterPriority.Important : ActionCenterPriority.Normal,
                null,
                null,
                null,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                scenario.SeasonRollover.DraftClassSummary,
                "The season has rolled over. These items keep the club moving toward the next training camp.",
                checklistItem.Contains("draft", StringComparison.OrdinalIgnoreCase)
                    ? "Review Hockey Operations draft board and assign scouts."
                    : "Review offseason planning before advancing too far.",
                null,
                null,
                null));
        }
    }

    private static void AddStaffLifeCycleItems(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        var lifecycle = scenario.StaffCareerSummaries.Count == 0
            ? new StaffLifeCycleService().EnsureLifeCycle(scenario)
            : scenario;
        items.AddRange(new StaffLifeCycleService().BuildActionItems(lifecycle));
    }

    private static void AddOwnerLifeCycleItems(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        var lifecycle = scenario.OwnerCareerSummary is null
            ? new OwnerLifeCycleService().EnsureLifeCycle(scenario)
            : scenario;
        items.AddRange(new OwnerLifeCycleService().BuildActionItems(lifecycle));
    }

    private static void AddPlayerLifeCycleItems(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        items.AddRange(new PlayerLifeCycleService().BuildActionItems(scenario));
    }

    private static ActionCenterItem ApplyStatus(ActionCenterItem item, IReadOnlyDictionary<string, ActionCenterStatus>? statusOverrides) =>
        statusOverrides is not null && statusOverrides.TryGetValue(item.ActionCenterItemId, out var status)
            ? item with { Status = status }
            : item;

    private static ActionCenterCategory CategoryFor(PendingGmActionType type) =>
        type switch
        {
            PendingGmActionType.SignRecruit => ActionCenterCategory.Recruiting,
            PendingGmActionType.SignDraftPick or PendingGmActionType.SignFreeAgent or PendingGmActionType.ApproveContract or PendingGmActionType.DeclineContract or PendingGmActionType.ApproveTrade or PendingGmActionType.DeclineTrade => ActionCenterCategory.Contracts,
            PendingGmActionType.InviteToCamp or PendingGmActionType.AddToRoster or PendingGmActionType.ReleasePlayer or PendingGmActionType.CutPlayer or PendingGmActionType.AssignToAffiliate or PendingGmActionType.ReturnToParent or PendingGmActionType.ReturnToJuniorTeam or PendingGmActionType.PlaceOnWaivers => ActionCenterCategory.Roster,
            _ => ActionCenterCategory.System
        };

    private static ActionCenterCategory CategoryFor(InboxCategory category) =>
        category switch
        {
            InboxCategory.Owner => ActionCenterCategory.Owner,
            InboxCategory.Staff => ActionCenterCategory.Staff,
            InboxCategory.Scouting => ActionCenterCategory.Scouting,
            InboxCategory.Recruiting => ActionCenterCategory.Recruiting,
            InboxCategory.PlayerDevelopment => ActionCenterCategory.PlayerDevelopment,
            InboxCategory.Medical => ActionCenterCategory.Medical,
            InboxCategory.Contracts => ActionCenterCategory.Contracts,
            InboxCategory.League or InboxCategory.Draft => ActionCenterCategory.League,
            _ => ActionCenterCategory.System
        };

    private static ActionCenterPriority PriorityFor(PendingGmActionType type) =>
        type is PendingGmActionType.ApproveContract or PendingGmActionType.DeclineContract or PendingGmActionType.AddToRoster
            ? ActionCenterPriority.Urgent
            : ActionCenterPriority.Important;

    private static ActionCenterPriority PriorityFor(InboxMessage message) =>
        message.Priority switch
        {
            InboxPriority.Urgent => ActionCenterPriority.Urgent,
            InboxPriority.Important => ActionCenterPriority.Important,
            InboxPriority.Low => ActionCenterPriority.Low,
            _ => message.Item.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical ? ActionCenterPriority.Important : ActionCenterPriority.Normal
        };

    private static bool InvolvesPlayerTeam(NewGmScenarioSnapshot scenario, ScheduledGame game) =>
        game.HomeOrganizationId == scenario.Organization.OrganizationId || game.AwayOrganizationId == scenario.Organization.OrganizationId;

    private static string OpponentName(NewGmScenarioSnapshot scenario, ScheduledGame game)
    {
        var opponentId = game.HomeOrganizationId == scenario.Organization.OrganizationId
            ? game.AwayOrganizationId
            : game.HomeOrganizationId;
        var standing = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == opponentId);
        return standing?.TeamName ?? opponentId;
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private static string StableId(string text) =>
        new string(text.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
}
