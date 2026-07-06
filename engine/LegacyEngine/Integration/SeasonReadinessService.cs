using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class SeasonReadinessService
{
    public SeasonReadinessReport Evaluate(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rulebook = ResolveRulebook(registry);
        var rosterReport = BuildRosterReport(registry, scenario, rulebook);
        var checklist = BuildChecklist(scenario, rosterReport).ToArray();
        var mandatoryComplete = checklist.Where(item => item.IsMandatory).All(item => item.IsComplete);
        var canBegin = rosterReport.IsReady && mandatoryComplete;
        var blockedReason = canBegin
            ? "Organization ready for opening night."
            : string.Join(" ", checklist.Where(item => item.IsMandatory && !item.IsComplete).Select(item => item.Text));

        var report = new SeasonReadinessReport(
            IsReady: canBegin,
            CanBeginSeason: canBegin,
            RosterStatus: rosterReport.IsReady ? "Ready" : "Not Ready",
            RosterReport: rosterReport,
            ChecklistItems: checklist,
            OrganizationHealth: canBegin ? "Stable and ready." : "Needs GM decisions before opening night.",
            OwnerSatisfaction: OwnerSatisfaction(rosterReport, scenario),
            OwnerReview: BuildOwnerReview(rosterReport, scenario),
            HeadCoachSummary: BuildCoachSummary(rosterReport),
            HeadScoutSummary: BuildScoutSummary(scenario),
            StaffRecommendations: BuildStaffRecommendations(rosterReport, scenario),
            TrainingCampStatus: scenario.TrainingCamp?.IsCompleted == true
                ? "Training camp completed."
                : "Training camp is not complete.",
            BlockedReason: blockedReason);
        report.Validate();
        return report;
    }

    public SeasonReadinessResult GenerateReviews(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var report = Evaluate(registry, scenario);
        var updated = scenario with { SeasonReadiness = scenario.SeasonReadiness with { ReviewsGenerated = true } };
        var inbox = new List<AlphaInboxItem>
        {
            Inbox(updated, LegacyEventType.OwnerOffseasonReview, "Owner offseason review", report.OwnerReview),
            Inbox(updated, LegacyEventType.CoachRosterReview, "Head coach roster review", report.HeadCoachSummary),
            Inbox(updated, LegacyEventType.ScoutOffseasonReview, "Head scout offseason review", report.HeadScoutSummary),
            Inbox(updated, LegacyEventType.MilestoneReached, "League opening night reminder", "League office reminder: opening roster compliance is required before the season begins.")
        };

        QueueEvent(registry, updated, LegacyEventType.OwnerOffseasonReview, "Owner offseason review", report.OwnerReview, updated.AlphaSnapshot.Owner.OwnerId);
        QueueEvent(registry, updated, LegacyEventType.CoachRosterReview, "Head coach roster review", report.HeadCoachSummary, updated.AlphaSnapshot.CoachPerson?.PersonId);
        QueueEvent(registry, updated, LegacyEventType.ScoutOffseasonReview, "Head scout offseason review", report.HeadScoutSummary, updated.AlphaSnapshot.ScoutPerson.PersonId);

        return Result(true, updated, Evaluate(registry, updated), inbox, "Opening night reviews generated.");
    }

    public SeasonReadinessResult BeginSeason(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var report = Evaluate(registry, scenario);
        if (!report.CanBeginSeason)
        {
            QueueEvent(registry, scenario, LegacyEventType.OpeningRosterRejected, "Opening roster rejected", report.BlockedReason);
            var blockedInbox = new[]
            {
                Inbox(scenario, LegacyEventType.OpeningRosterRejected, "Opening roster not ready", report.BlockedReason)
            };
            return Result(false, scenario, report, blockedInbox, "Season cannot begin until mandatory readiness tasks are complete.");
        }

        registry.WorldEngine.SetPhase(WorldPhase.RegularSeason);

        var season = scenario.Season with
        {
            Status = SeasonStatus.Active,
            CurrentPhase = SeasonPhase.RegularSeason
        };
        var world = registry.WorldEngine.State;
        var alphaSnapshot = scenario.AlphaSnapshot with
        {
            WorldState = world,
            Season = season
        };
        var updated = scenario with
        {
            Season = season,
            AlphaSnapshot = alphaSnapshot,
            SeasonReadiness = scenario.SeasonReadiness with { SeasonBegun = true }
        };
        updated = new SeasonFrameworkService().EnsureSeasonFramework(registry, updated);

        var reportResult = new ExecutiveReportService().GenerateFrontOfficeReadinessReport(registry, updated);
        var finalScenario = reportResult.Success ? reportResult.ScenarioSnapshot : updated;

        QueueEvent(registry, finalScenario, LegacyEventType.OpeningRosterValidated, "Opening roster validated", "Opening roster passed readiness validation.");
        QueueEvent(registry, finalScenario, LegacyEventType.SeasonReady, "Season ready", "Organization is ready for opening night.");
        QueueEvent(registry, finalScenario, LegacyEventType.SeasonStarted, "Season started", "The regular season has begun.");
        var inbox = new List<AlphaInboxItem>
        {
            Inbox(finalScenario, LegacyEventType.OpeningRosterValidated, "Opening roster validated", "League office accepted the opening roster."),
            Inbox(finalScenario, LegacyEventType.SeasonReady, "Opening Night ready", "Organization Ready. The regular season may begin.")
        };
        inbox.AddRange(reportResult.InboxItems);

        return Result(true, finalScenario, Evaluate(registry, finalScenario), inbox, "Season begun. Organization is ready for opening night.");
    }

    private static OpeningRosterReport BuildRosterReport(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        Rulebook rulebook)
    {
        var roster = scenario.AlphaSnapshot.Roster;
        var current = roster.CurrentPlayers;
        var active = roster.ActivePlayers;
        var validation = registry.RosterEngine.ValidateRoster(
            roster,
            rulebook.RosterRules is null ? null : new RosterRuleValidator(rulebook));
        var campOpen = scenario.TrainingCamp?.Players
            .Count(player => player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp or TrainingCampStatus.Kept or TrainingCampStatus.Injured) ?? 0;
        var unresolvedProspects = scenario.ProspectRights.Count(item =>
            item.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered or ProspectStatus.InvitedToCamp);
        var pending = scenario.PendingActions.Count(action => action.IsOpen);
        var campNeedsDecision = scenario.TrainingCamp?.Players.Count(player =>
            player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp) ?? 0;

        return new OpeningRosterReport(
            CurrentRosterSize: active.Count,
            RequiredRosterSize: rulebook.RosterRules?.ActiveRoster ?? active.Count,
            Goalies: active.Count(player => player.Position == RosterPosition.Goalie),
            Defense: active.Count(player => player.Position == RosterPosition.Defense),
            Forwards: active.Count(player => player.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing),
            Prospects: scenario.ProspectRights.Count,
            UnsignedPlayers: scenario.ProspectRights.Count(item => item.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered),
            TrainingCampInvitees: campOpen,
            PlayersRequiringDecisions: pending + unresolvedProspects + campNeedsDecision,
            ValidationResult: validation);
    }

    private static IReadOnlyList<OpeningChecklistItem> BuildChecklist(
        NewGmScenarioSnapshot scenario,
        OpeningRosterReport rosterReport)
    {
        var overBy = Math.Max(0, rosterReport.CurrentRosterSize - rosterReport.RequiredRosterSize);
        var underBy = Math.Max(0, rosterReport.RequiredRosterSize - rosterReport.CurrentRosterSize);
        return new[]
        {
            new OpeningChecklistItem("pending-actions", "Resolve every pending GM action.", scenario.PendingActions.All(action => !action.IsOpen)),
            new OpeningChecklistItem("roster-validation", rosterReport.ValidationResult.IsValid ? "Opening roster passes RuleEngine validation." : rosterReport.ValidationResult.Message, rosterReport.ValidationResult.IsValid),
            new OpeningChecklistItem("roster-over", overBy == 0 ? "Roster is not over the opening limit." : $"Reduce roster by {overBy} player(s).", overBy == 0),
            new OpeningChecklistItem("roster-under", underBy == 0 ? "Roster has enough active players." : $"Add {underBy} player(s) to reach opening roster target.", underBy == 0),
            new OpeningChecklistItem("prospect-decisions", "Resolve drafted/unsigned prospect decisions.", rosterReport.UnsignedPlayers == 0),
            new OpeningChecklistItem("camp-complete", "Complete Training Camp.", scenario.TrainingCamp?.IsCompleted == true),
            new OpeningChecklistItem("owner-review", "Review Owner Expectations.", scenario.SeasonReadiness.ReviewsGenerated)
        };
    }

    private static string OwnerSatisfaction(OpeningRosterReport report, NewGmScenarioSnapshot scenario)
    {
        if (report.IsReady && scenario.ProspectRights.Count > 0)
        {
            return "Confident";
        }

        return report.ValidationResult.IsValid ? "Cautious" : "Concerned";
    }

    private static string BuildOwnerReview(OpeningRosterReport report, NewGmScenarioSnapshot scenario)
    {
        if (!report.ValidationResult.IsValid)
        {
            return $"{scenario.AlphaSnapshot.Owner.Name}: Concerned about roster compliance. {report.ValidationResult.Message}";
        }

        return scenario.ProspectRights.Count > 0
            ? $"{scenario.AlphaSnapshot.Owner.Name}: Good offseason. Draft class is in place; make sure development stays patient."
            : $"{scenario.AlphaSnapshot.Owner.Name}: Roster is compliant, but prospect pipeline needs attention.";
    }

    private static string BuildCoachSummary(OpeningRosterReport report)
    {
        var needs = new List<string>();
        if (report.Forwards < 12)
        {
            needs.Add("top-six and depth forward coverage");
        }

        if (report.Defense < 6)
        {
            needs.Add("bottom-pairing defense depth");
        }

        if (report.Goalies < 2)
        {
            needs.Add("backup goalie coverage");
        }

        return needs.Count == 0
            ? "Head coach: roster balance looks workable. Leadership and special teams roles can be sorted next."
            : $"Head coach: needs {string.Join(", ", needs)} before opening night.";
    }

    private static string BuildScoutSummary(NewGmScenarioSnapshot scenario)
    {
        var unsigned = scenario.ProspectRights.Count(item => item.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered);
        var priorities = scenario.ProspectRights
            .OrderBy(item => item.PickNumber)
            .Take(3)
            .Select(item => item.ProspectName)
            .ToArray();
        return priorities.Length == 0
            ? "Head scout: no draft class to review yet."
            : $"Head scout: draft class priority list is {string.Join(", ", priorities)}. Unsigned prospects: {unsigned}.";
    }

    private static string BuildStaffRecommendations(OpeningRosterReport report, NewGmScenarioSnapshot scenario)
    {
        var notes = new List<string>();
        if (report.Goalies < 2)
        {
            notes.Add("Goalie needs more work before opening night.");
        }

        if (scenario.ProspectRights.Any(item => item.Status == ProspectStatus.InvitedToCamp))
        {
            notes.Add("Prospects invited to camp still need final path decisions.");
        }

        if (report.CurrentRosterSize > report.RequiredRosterSize)
        {
            notes.Add("Staff recommend final cutdown before league approval.");
        }

        return notes.Count == 0
            ? "Staff: veteran core can remain; no automatic decisions recommended."
            : string.Join(" ", notes);
    }

    private static void QueueEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string description,
        string? personId = null)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            ToDateTimeOffset(scenario.CurrentDate),
            eventType,
            eventType == LegacyEventType.OpeningRosterRejected ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_1_8_opening_readiness" });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string summary) =>
        new(
            InboxItemId: $"inbox:readiness:{Guid.NewGuid():N}",
            Date: ToDateTimeOffset(scenario.CurrentDate),
            EventType: eventType,
            Severity: eventType == LegacyEventType.OpeningRosterRejected ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            Title: title,
            Summary: summary,
            PrimaryPersonId: null);

    private static SeasonReadinessResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        SeasonReadinessReport report,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        string message)
    {
        var result = new SeasonReadinessResult(success, scenario, report, inboxItems, message);
        result.Validate();
        return result;
    }

    private static Rulebook ResolveRulebook(EngineRegistry registry) =>
        registry.Rulebook ?? RulebookPresets.CreateJuniorMajor();

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 15, 0, 0, TimeSpan.Zero);
}
