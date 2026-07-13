using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

/// <summary>
/// Coordinates the move from offseason contract decisions into camp and opening-night
/// preparation. It reports problems and creates transition notices, but never signs,
/// cuts, assigns, or releases a player on the GM's behalf.
/// </summary>
public sealed class OffseasonRosterReadinessService
{
    public OffseasonRosterReadinessReport BuildReport(
        NewGmScenarioSnapshot scenario,
        Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rules = rulebook ?? scenario.LeagueProfile.Rulebook;
        var currentCalendar = SeasonCalendar.Build(scenario.CurrentDate.Year, scenario.Season.Settings);
        var campOpensOn = MilestoneDate(currentCalendar, SeasonMilestoneType.TrainingCampOpens);
        var openingNightOn = MilestoneDate(currentCalendar, SeasonMilestoneType.SeasonBegins);
        var roster = scenario.AlphaSnapshot.Roster;
        var rosterValidation = rules.RosterRules is null
            ? RosterValidationResult.Valid("Roster rules are not configured for this league.")
            : ToRosterValidation(new RosterRuleValidator(rules).Validate(new RosterValidationRequest(
                roster.Players.Count,
                roster.ActivePlayers.Count,
                roster.ActivePlayers.Count(player => player.Position == RosterPosition.Goalie),
                roster.Players.Count(player => player.IsOverage()),
                roster.Players.Count(player => player.IsImport))));
        var cap = new SalaryCapService().BuildSnapshot(scenario, rules);
        var market = new ContractMarketService().BuildSummary(scenario, rules);
        var staffVacancies = new StaffOfficeService().BuildVacancies(scenario, rules);
        var pending = scenario.PendingActions.Count(action => action.IsOpen);
        var unsignedProspects = scenario.ProspectRights.Count(prospect => prospect.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered);
        var openContractDecisions = market.Negotiations.Count(negotiation => negotiation.IsOpen)
            + market.Deadlines.Count(deadline => deadline.IsActionable);
        var capCompliant = !cap.IsEnabled || cap.Status is not (SalaryCapStatus.OverCap or SalaryCapStatus.Violation);
        var phase = DeterminePhase(scenario, campOpensOn, openingNightOn);
        var issues = BuildIssues(
            scenario,
            rules,
            phase,
            campOpensOn,
            openingNightOn,
            roster,
            rosterValidation,
            cap,
            capCompliant,
            pending,
            unsignedProspects,
            openContractDecisions,
            staffVacancies.Count);
        var summary = BuildSummary(
            scenario,
            phase,
            campOpensOn,
            openingNightOn,
            roster,
            rosterValidation,
            capCompliant,
            unsignedProspects,
            openContractDecisions,
            pending,
            issues);

        var report = new OffseasonRosterReadinessReport(
            phase,
            campOpensOn,
            openingNightOn,
            campOpensOn.DayNumber - scenario.CurrentDate.DayNumber,
            openingNightOn.DayNumber - scenario.CurrentDate.DayNumber,
            roster.ActivePlayers.Count,
            rules.RosterRules?.ActiveRoster ?? roster.ActivePlayers.Count,
            unsignedProspects,
            openContractDecisions,
            pending,
            staffVacancies.Count,
            capCompliant,
            cap.IsEnabled ? cap.Status.ToString() : "Disabled",
            rosterValidation,
            issues,
            summary);
        report.Validate();
        return report;
    }

    public OffseasonRosterReadinessResult Process(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rulebook = registry.Rulebook ?? scenario.LeagueProfile.Rulebook;
        var report = BuildReport(scenario, rulebook);
        var state = scenario.OffseasonRosterReadinessState;
        var notices = state.ProcessedTransitionNotices.ToHashSet(StringComparer.Ordinal);
        var inbox = new List<AlphaInboxItem>();
        var phaseChanged = state.LastPhase is not null && state.LastPhase != report.Phase;
        var transitionKey = $"{report.Phase}:{scenario.CurrentDate:yyyy-MM-dd}";
        if (phaseChanged && notices.Add(transitionKey) && IsPlayerUsefulTransition(report.Phase))
        {
            var title = report.Phase switch
            {
                OffseasonReadinessPhase.CampPreparation => "Camp preparation begins",
                OffseasonReadinessPhase.TrainingCamp => "Training camp is open",
                OffseasonReadinessPhase.OpeningRosterReview => "Opening roster deadline reached",
                OffseasonReadinessPhase.ReadyForSeason => "Organization is ready for the season",
                _ => "Offseason readiness update"
            };
            var severity = report.Phase is OffseasonReadinessPhase.OpeningRosterReview
                && !report.IsReadyForOpeningNight
                ? LegacyEventSeverity.Warning
                : LegacyEventSeverity.Notice;
            inbox.Add(new AlphaInboxItem(
                $"inbox:offseason-readiness:{transitionKey}",
                At(scenario.CurrentDate, 9),
                LegacyEventType.MilestoneReached,
                severity,
                title,
                report.Summary,
                null));
            var legacyEvent = registry.EventEngine.CreateEvent(
                At(scenario.CurrentDate, 9),
                LegacyEventType.MilestoneReached,
                severity,
                LegacyEventVisibility.Organization,
                title,
                report.Summary,
                new LegacyEventContext(OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
                new Dictionary<string, object?> { ["source"] = "alpha_8_9_offseason_readiness", ["phase"] = report.Phase.ToString() });
            registry.EventEngine.QueueEvent(legacyEvent);
        }

        var updated = scenario with
        {
            OffseasonRosterReadinessState = state with
            {
                LastEvaluatedDate = scenario.CurrentDate,
                LastPhase = report.Phase,
                TransitionNotices = notices.OrderBy(item => item, StringComparer.Ordinal).Take(100).ToArray()
            }
        };
        var result = new OffseasonRosterReadinessResult(
            true,
            updated,
            report,
            inbox,
            report.Summary);
        result.Validate();
        return result;
    }

    private static OffseasonReadinessPhase DeterminePhase(
        NewGmScenarioSnapshot scenario,
        DateOnly campOpensOn,
        DateOnly openingNightOn)
    {
        if (scenario.SeasonReadiness.SeasonBegun)
        {
            return OffseasonReadinessPhase.ReadyForSeason;
        }

        if (scenario.TrainingCamp is { IsCompleted: false })
        {
            return OffseasonReadinessPhase.TrainingCamp;
        }

        if (scenario.CurrentDate >= openingNightOn)
        {
            return OffseasonReadinessPhase.OpeningRosterReview;
        }

        if (scenario.CurrentDate >= campOpensOn)
        {
            return OffseasonReadinessPhase.TrainingCamp;
        }

        if (scenario.CurrentDate >= scenario.DraftDate)
        {
            return OffseasonReadinessPhase.CampPreparation;
        }

        if (scenario.FreeAgencyMarketState?.Window.Phase is FreeAgencyPhase.OpeningDay or FreeAgencyPhase.ActiveMarket or FreeAgencyPhase.SlowMarket
            || scenario.ContractNegotiations.Any(negotiation => negotiation.IsOpen))
        {
            return OffseasonReadinessPhase.MarketReview;
        }

        return OffseasonReadinessPhase.ContractReview;
    }

    private static IReadOnlyList<OffseasonRosterReadinessIssue> BuildIssues(
        NewGmScenarioSnapshot scenario,
        Rulebook rulebook,
        OffseasonReadinessPhase phase,
        DateOnly campOpensOn,
        DateOnly openingNightOn,
        Roster roster,
        RosterValidationResult rosterValidation,
        SalaryCapSnapshot cap,
        bool capCompliant,
        int pending,
        int unsignedProspects,
        int openContractDecisions,
        int staffVacancies)
    {
        var issues = new List<OffseasonRosterReadinessIssue>();
        var target = rulebook.RosterRules?.ActiveRoster ?? roster.ActivePlayers.Count;
        if (!capCompliant)
        {
            issues.Add(new OffseasonRosterReadinessIssue(
                "salary-cap",
                OffseasonReadinessSeverity.Urgent,
                "Salary cap compliance needed",
                $"Player payroll is in {cap.Status} status with {cap.CapRemaining:C0} of cap space remaining.",
                "The league may reject the opening roster until cap compliance is restored.",
                "Open Contract Market and review expiring deals, offers, or roster plans.",
                openingNightOn));
        }

        if (!rosterValidation.IsValid)
        {
            issues.Add(new OffseasonRosterReadinessIssue(
                "roster-compliance",
                OffseasonReadinessSeverity.Urgent,
                "Opening roster compliance needed",
                rosterValidation.Message,
                "Opening night remains blocked until the roster passes the rulebook.",
                "Review Hockey Operations and make explicit keep, cut, release, or assignment decisions.",
                openingNightOn));
        }

        if (roster.ActivePlayers.Count > target)
        {
            issues.Add(new OffseasonRosterReadinessIssue(
                "roster-over-limit",
                OffseasonReadinessSeverity.Urgent,
                "Roster is over the opening target",
                $"The active roster has {roster.ActivePlayers.Count} players against a target of {target}.",
                "No automatic cuts will be made; the season cannot begin until the GM resolves the excess.",
                "Review Roster or Training Camp and choose the appropriate player action.",
                openingNightOn));
        }
        else if (phase is OffseasonReadinessPhase.TrainingCamp or OffseasonReadinessPhase.OpeningRosterReview
            && roster.ActivePlayers.Count < target)
        {
            issues.Add(new OffseasonRosterReadinessIssue(
                "roster-under-target",
                OffseasonReadinessSeverity.Important,
                "Opening roster needs players",
                $"The active roster has {roster.ActivePlayers.Count} players against a target of {target}.",
                "The club may enter camp short-handed if the gap is not addressed.",
                "Review Contract Market, Prospects, and Training Camp for approved additions.",
                openingNightOn));
        }

        if (pending > 0)
        {
            var action = scenario.PendingActions.FirstOrDefault(item => item.IsOpen);
            issues.Add(new OffseasonRosterReadinessIssue(
                "pending-gm-decisions",
                OffseasonReadinessSeverity.Urgent,
                $"{pending} GM decision(s) are pending",
                action?.Reason ?? "One or more roster, contract, or prospect decisions await approval.",
                "Nothing changes until the GM approves or declines the decision.",
                "Open Action Center and resolve the highest-priority decision first.",
                action?.CreatedOn.AddDays(7),
                action?.PersonId,
                action?.PersonName));
        }

        if (openContractDecisions > 0)
        {
            var negotiation = scenario.ContractNegotiations.FirstOrDefault(item => item.IsOpen);
            issues.Add(new OffseasonRosterReadinessIssue(
                "contract-market",
                OffseasonReadinessSeverity.Important,
                "Contract decisions remain open",
                $"The Contract Market has {openContractDecisions} open negotiation or deadline item(s).",
                "Rights, cap room, and roster plans can change when deadlines pass.",
                "Review Contract Market and approve, revise, or decline each important decision.",
                negotiation?.DecisionDeadline,
                negotiation?.PersonId,
                negotiation?.PersonName));
        }

        if (unsignedProspects > 0 && phase is OffseasonReadinessPhase.CampPreparation or OffseasonReadinessPhase.TrainingCamp or OffseasonReadinessPhase.OpeningRosterReview)
        {
            var prospect = scenario.ProspectRights
                .Where(item => item.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered)
                .OrderBy(item => item.PickNumber)
                .FirstOrDefault();
            issues.Add(new OffseasonRosterReadinessIssue(
                "unsigned-prospects",
                OffseasonReadinessSeverity.Important,
                $"{unsignedProspects} drafted prospect decision(s) remain",
                prospect is null ? "Draft rights still need a path." : $"{prospect.ProspectName} still has draft rights without a final path.",
                "Unsigned prospects will not join the active roster automatically.",
                "Review Prospect List and choose Offer Contract, Invite to Camp, Return, or Release Rights.",
                openingNightOn,
                prospect?.ProspectPersonId,
                prospect?.ProspectName));
        }

        if (staffVacancies > 0 && phase is OffseasonReadinessPhase.CampPreparation or OffseasonReadinessPhase.TrainingCamp or OffseasonReadinessPhase.OpeningRosterReview)
        {
            issues.Add(new OffseasonRosterReadinessIssue(
                "staff-vacancies",
                OffseasonReadinessSeverity.Important,
                $"{staffVacancies} staff vacancy(ies) remain",
                "Rulebook staff coverage is incomplete.",
                "Scouting, coaching, or medical follow-through may be weaker during camp.",
                "Review Organization and hire or reassign staff where appropriate.",
                openingNightOn));
        }

        if (phase == OffseasonReadinessPhase.CampPreparation && campOpensOn.DayNumber - scenario.CurrentDate.DayNumber <= 14)
        {
            issues.Add(new OffseasonRosterReadinessIssue(
                "camp-preparation",
                OffseasonReadinessSeverity.Information,
                "Training camp is approaching",
                $"Camp opens on {campOpensOn:yyyy-MM-dd}.",
                "Unsigned players and unresolved roster plans will make camp harder to manage.",
                "Review the camp preview and settle only the decisions that matter most."));
        }

        return issues
            .GroupBy(issue => issue.IssueId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.DueDate ?? DateOnly.MaxValue)
            .ThenBy(issue => issue.Title, StringComparer.Ordinal)
            .Take(8)
            .ToArray();
    }

    private static string BuildSummary(
        NewGmScenarioSnapshot scenario,
        OffseasonReadinessPhase phase,
        DateOnly campOpensOn,
        DateOnly openingNightOn,
        Roster roster,
        RosterValidationResult rosterValidation,
        bool capCompliant,
        int unsignedProspects,
        int openContractDecisions,
        int pending,
        IReadOnlyList<OffseasonRosterReadinessIssue> issues)
    {
        var status = issues.FirstOrDefault()?.Title
            ?? (capCompliant && rosterValidation.IsValid ? "Organization is on track" : "Organization needs review");
        return $"{status}. Phase: {Readable(phase)}. Camp opens {campOpensOn:yyyy-MM-dd}; opening night is {openingNightOn:yyyy-MM-dd}. "
            + $"Active roster: {roster.ActivePlayers.Count}. Unsigned prospects: {unsignedProspects}. Open contract decisions: {openContractDecisions}. Pending GM decisions: {pending}. "
            + "All roster and contract moves remain explicit GM decisions.";
    }

    private static bool IsPlayerUsefulTransition(OffseasonReadinessPhase phase) =>
        phase is OffseasonReadinessPhase.CampPreparation
            or OffseasonReadinessPhase.TrainingCamp
            or OffseasonReadinessPhase.OpeningRosterReview
            or OffseasonReadinessPhase.ReadyForSeason;

    private static DateOnly MilestoneDate(SeasonCalendar calendar, SeasonMilestoneType type) =>
        calendar.Milestones.First(milestone => milestone.Type == type).Date.Value;

    private static RosterValidationResult ToRosterValidation(RuleValidationResult result) =>
        result.IsValid
            ? RosterValidationResult.Valid(result.Message)
            : RosterValidationResult.Failure(result.RuleCode, result.Message, result.Details);

    private static string Readable(OffseasonReadinessPhase phase) => phase switch
    {
        OffseasonReadinessPhase.ContractReview => "Contract review",
        OffseasonReadinessPhase.MarketReview => "Market review",
        OffseasonReadinessPhase.CampPreparation => "Camp preparation",
        OffseasonReadinessPhase.TrainingCamp => "Training camp",
        OffseasonReadinessPhase.OpeningRosterReview => "Opening roster review",
        OffseasonReadinessPhase.ReadyForSeason => "Ready for season",
        _ => phase.ToString()
    };

    private static DateTimeOffset At(DateOnly date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.Zero);
}
