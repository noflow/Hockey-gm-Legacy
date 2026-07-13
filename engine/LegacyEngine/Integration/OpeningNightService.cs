using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

/// <summary>
/// Presents the final front-office check before regular-season play begins. It does
/// not sign, cut, release, assign, or otherwise decide for the GM.
/// </summary>
public sealed class OpeningNightService
{
    public OpeningNightPreview BuildPreview(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var prepared = new SeasonFrameworkService().EnsureSeasonFramework(registry, scenario);
        var readiness = new SeasonReadinessService().Evaluate(registry, prepared);
        var rules = registry.Rulebook ?? prepared.LeagueProfile.Rulebook;
        var cap = new SalaryCapService().BuildSnapshot(prepared, rules);
        var calendar = SeasonCalendar.Build(prepared.CurrentDate.Year, prepared.Season.Settings);
        var openingNight = MilestoneDate(calendar, SeasonMilestoneType.SeasonBegins);
        var nextGame = new SeasonFrameworkService().NextGame(prepared);
        var active = prepared.AlphaSnapshot.Roster.ActivePlayers;
        var injured = prepared.AlphaSnapshot.Injuries
            .Where(injury => injury.IsActive)
            .OrderByDescending(injury => injury.Severity)
            .Take(5)
            .Select(injury => InjuryNote(prepared, injury))
            .ToArray();
        var capCompliant = !cap.IsEnabled || cap.Status is not (SalaryCapStatus.OverCap or SalaryCapStatus.Violation);
        var canBegin = !prepared.SeasonReadiness.SeasonBegun && readiness.CanBeginSeason && capCompliant;
        var status = prepared.SeasonReadiness.SeasonBegun
            ? OpeningNightStatus.Begun
            : canBegin ? OpeningNightStatus.ReadyToBegin : OpeningNightStatus.Blocked;
        var goalieNames = active
            .Where(player => player.IsGoalie)
            .Select(player => PersonName(prepared, player.PersonId))
            .ToArray();
        var rosterRules = rules.RosterRules;
        var strengths = BuildStrengths(prepared, readiness, cap);
        var concerns = BuildConcerns(prepared, readiness, cap, injured);
        var ownerExpectation = prepared.AlphaSnapshot.Owner.Goals
            .OrderBy(goal => goal.Priority)
            .Select(goal => goal.Description)
            .FirstOrDefault()
            ?? "The owner expects disciplined roster management and competitive progress.";
        var opponent = nextGame is null
            ? null
            : nextGame.HomeOrganizationId == prepared.Organization.OrganizationId
                ? TeamName(prepared, nextGame.AwayOrganizationId)
                : TeamName(prepared, nextGame.HomeOrganizationId);
        var summary = prepared.SeasonReadiness.SeasonBegun
            ? "Opening night has passed. The organization is in the regular season."
            : canBegin
                ? $"The roster is ready for opening night on {openingNight:MMMM d, yyyy}."
                : $"Opening night is not ready: {FirstReason(readiness, capCompliant)}";
        var preview = new OpeningNightPreview(
            status,
            openingNight,
            opponent,
            active.Count,
            rosterRules?.ActiveRoster ?? active.Count,
            readiness.RosterReport.ValidationResult.IsValid ? "Roster compliant" : readiness.RosterReport.ValidationResult.Message,
            capCompliant,
            cap.IsEnabled ? cap.Status.ToString() : "Disabled by rulebook",
            goalieNames.Length == 0
                ? "No goalie plan is available. Review the roster before beginning."
                : goalieNames.Length == 1
                    ? $"{goalieNames[0]} is the only listed goalie. Add or designate a backup before opening night."
                    : $"Starter/backup coverage: {goalieNames[0]} / {goalieNames[1]}",
            $"{active.Count(player => player.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing)} forwards, {active.Count(player => player.Position == RosterPosition.Defense)} defensemen, {goalieNames.Length} goalies.",
            injured,
            strengths,
            concerns,
            ownerExpectation,
            summary,
            canBegin);
        preview.Validate();
        return preview;
    }

    public OpeningNightResult Begin(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var preview = BuildPreview(registry, scenario);
        if (preview.Status == OpeningNightStatus.Begun)
        {
            return Result(true, scenario, preview, Array.Empty<AlphaInboxItem>(), "The regular season has already begun.");
        }

        if (!preview.CanBegin)
        {
            var rejected = new SeasonReadinessService().BeginSeason(registry, scenario);
            return Result(rejected.Success, rejected.ScenarioSnapshot, BuildPreview(registry, rejected.ScenarioSnapshot), rejected.InboxItems, rejected.Message);
        }

        var started = new SeasonReadinessService().BeginSeason(registry, scenario);
        var state = started.ScenarioSnapshot.OpeningNight with
        {
            PreviewGenerated = true,
            BriefingSent = true,
            BeganOn = started.ScenarioSnapshot.CurrentDate
        };
        var updated = started.ScenarioSnapshot with { OpeningNight = state };
        var finalPreview = BuildPreview(registry, updated);
        var inbox = started.InboxItems.ToList();
        inbox.Add(BuildBriefing(updated, finalPreview));
        var result = Result(started.Success, updated, finalPreview, inbox, "Season begun. Opening night briefing delivered.");
        result.Validate();
        return result;
    }

    private static AlphaInboxItem BuildBriefing(NewGmScenarioSnapshot scenario, OpeningNightPreview preview) =>
        new(
            $"inbox:opening-night:{scenario.Season.SeasonId}",
            At(scenario.CurrentDate, 8),
            LegacyEventType.SeasonStarted,
            LegacyEventSeverity.Notice,
            "Opening Night Briefing",
            $"{scenario.Organization.Name} is ready for the regular season. {(preview.OpponentName is null ? "No opponent is scheduled yet." : $"First opponent: {preview.OpponentName}.")} {preview.GoaliePlan} Owner expectation: {preview.OwnerExpectation}",
            null);

    private static IReadOnlyList<string> BuildStrengths(NewGmScenarioSnapshot scenario, SeasonReadinessReport readiness, SalaryCapSnapshot cap)
    {
        var strengths = new List<string>();
        if (readiness.RosterReport.ValidationResult.IsValid)
        {
            strengths.Add("The opening roster passes league roster validation.");
        }

        if (!cap.IsEnabled || cap.Status is SalaryCapStatus.Comfortable or SalaryCapStatus.NearLimit)
        {
            strengths.Add(cap.IsEnabled ? "Player payroll is within the league cap." : "This league does not use a hard player salary cap.");
        }

        if (scenario.StaffMembers.Count > 0)
        {
            strengths.Add("An inherited staff group is already in place.");
        }

        return strengths.Count == 0 ? new[] { "The front office has a clear baseline to improve." } : strengths;
    }

    private static IReadOnlyList<string> BuildConcerns(NewGmScenarioSnapshot scenario, SeasonReadinessReport readiness, SalaryCapSnapshot cap, IReadOnlyList<string> injuries)
    {
        var concerns = new List<string>();
        if (!readiness.RosterReport.ValidationResult.IsValid)
        {
            concerns.Add(readiness.RosterReport.ValidationResult.Message);
        }

        if (cap.IsEnabled && !string.Equals(cap.Status.ToString(), SalaryCapStatus.Comfortable.ToString(), StringComparison.Ordinal))
        {
            concerns.Add($"Player payroll status: {cap.Status}.");
        }

        if (scenario.PendingActions.Any(action => action.IsOpen))
        {
            concerns.Add($"{scenario.PendingActions.Count(action => action.IsOpen)} GM decision(s) remain open.");
        }

        if (injuries.Count > 0)
        {
            concerns.Add($"{injuries.Count} active injury concern(s) need medical review.");
        }

        return concerns;
    }

    private static string FirstReason(SeasonReadinessReport readiness, bool capCompliant) =>
        !capCompliant ? "player payroll is not cap compliant" : readiness.BlockedReason;

    private static string InjuryNote(NewGmScenarioSnapshot scenario, Injury injury) =>
        $"{PersonName(scenario, injury.PersonId)} ({PositionFor(scenario, injury.PersonId)}): {injury.Severity} {injury.InjuryType}, expected back {injury.ExpectedReturnDate:yyyy-MM-dd}.";

    private static string PositionFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.CurrentPlayers.FirstOrDefault(player => player.PersonId == personId)?.Position.ToString() ?? "position not listed";

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;

    private static string TeamName(NewGmScenarioSnapshot scenario, string organizationId) =>
        SeasonFrameworkService.LeagueTeams(scenario).FirstOrDefault(team => team.OrganizationId == organizationId).TeamName
        ?? organizationId;

    private static DateOnly MilestoneDate(SeasonCalendar calendar, SeasonMilestoneType type) =>
        calendar.Milestones.Single(milestone => milestone.Type == type).Date.Value;

    private static DateTimeOffset At(DateOnly date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.Zero);

    private static OpeningNightResult Result(bool success, NewGmScenarioSnapshot scenario, OpeningNightPreview preview, IReadOnlyList<AlphaInboxItem> inbox, string message)
    {
        var result = new OpeningNightResult(success, scenario, preview, inbox, message);
        result.Validate();
        return result;
    }
}
