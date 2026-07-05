using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class ProspectDecisionService
{
    private readonly PendingGmActionService _pendingActions = new();
    private readonly TrainingCampService _trainingCamp = new();

    public ProspectListSummary BuildSummary(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        return new ProspectListSummary(
            TotalProspects: scenario.ProspectRights.Count,
            RightsHeld: scenario.ProspectRights.Count(item => item.Status == ProspectStatus.DraftRightsHeld),
            ContractOffered: scenario.ProspectRights.Count(item => item.Status == ProspectStatus.ContractOffered),
            Signed: scenario.ProspectRights.Count(item => item.Status == ProspectStatus.Signed),
            InvitedToCamp: scenario.ProspectRights.Count(item => item.Status == ProspectStatus.InvitedToCamp),
            Returned: scenario.ProspectRights.Count(item => item.Status is ProspectStatus.ReturnedToJunior or ProspectStatus.ReturnedToYouthTeam),
            AssignedToAffiliate: scenario.ProspectRights.Count(item => item.Status == ProspectStatus.AssignedToAffiliate),
            ReleasedOrDeclined: scenario.ProspectRights.Count(item => item.Status is ProspectStatus.Released or ProspectStatus.Declined));
    }

    public IReadOnlyList<ProspectDecisionType> AvailableDecisions(EngineRegistry registry, NewGmScenarioSnapshot scenario, string prospectPersonId)
    {
        var prospect = FindProspect(scenario, prospectPersonId);
        var rulebook = ResolveRulebook(registry);
        var decisions = new List<ProspectDecisionType>();

        if (prospect.Status is ProspectStatus.Released or ProspectStatus.Declined or ProspectStatus.AssignedToAffiliate)
        {
            return decisions;
        }

        if (prospect.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered)
        {
            decisions.Add(ProspectDecisionType.OfferContract);
            decisions.Add(ProspectDecisionType.DeclineSigning);
        }

        if (prospect.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered or ProspectStatus.Signed)
        {
            decisions.Add(ProspectDecisionType.InviteToCamp);
        }

        if (SupportsJuniorReturn(rulebook))
        {
            decisions.Add(ProspectDecisionType.ReturnToJunior);
            decisions.Add(ProspectDecisionType.ReturnToYouthTeam);
        }

        if (SupportsAffiliateAssignment(scenario, rulebook))
        {
            decisions.Add(ProspectDecisionType.AssignToAffiliate);
        }

        decisions.Add(ProspectDecisionType.ReleaseRights);
        return decisions.Distinct().ToArray();
    }

    public ProspectDecisionResult ApplyDecision(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        ProspectDecision decision)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        decision.Validate();
        scenario.Validate();

        var prospect = FindProspect(scenario, decision.ProspectPersonId);
        var rulebook = ResolveRulebook(registry);
        var validation = ValidateDecision(scenario, rulebook, prospect, decision.DecisionType);
        if (validation is not null)
        {
            return BuildResult(false, scenario, prospect, Array.Empty<AlphaInboxItem>(), validation);
        }

        return decision.DecisionType switch
        {
            ProspectDecisionType.OfferContract => OfferContract(registry, scenario, prospect, decision),
            ProspectDecisionType.DeclineSigning => UpdateStatus(registry, scenario, prospect, decision, ProspectStatus.Declined),
            ProspectDecisionType.InviteToCamp => InviteToCamp(registry, scenario, prospect, decision),
            ProspectDecisionType.ReturnToJunior => UpdateStatus(registry, scenario, prospect, decision, ProspectStatus.ReturnedToJunior),
            ProspectDecisionType.ReturnToYouthTeam => UpdateStatus(registry, scenario, prospect, decision, ProspectStatus.ReturnedToYouthTeam),
            ProspectDecisionType.AssignToAffiliate => UpdateStatus(registry, scenario, prospect, decision, ProspectStatus.AssignedToAffiliate),
            ProspectDecisionType.ReleaseRights => UpdateStatus(registry, scenario, prospect, decision, ProspectStatus.Released),
            _ => BuildResult(false, scenario, prospect, Array.Empty<AlphaInboxItem>(), "Unsupported prospect decision.")
        };
    }

    private ProspectDecisionResult OfferContract(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        DraftRightsRecord prospect,
        ProspectDecision decision)
    {
        var statusScenario = ReplaceProspect(scenario, prospect with { Status = ProspectStatus.ContractOffered });
        var pendingExists = statusScenario.PendingActions.Any(action =>
            action.IsOpen
            && action.PersonId == prospect.ProspectPersonId
            && action.ActionType == PendingGmActionType.SignDraftPick);
        var updatedScenario = statusScenario;
        var inbox = new List<AlphaInboxItem>();

        if (!pendingExists)
        {
            var pending = _pendingActions.CreateForDraftPickReady(
                registry,
                statusScenario,
                prospect.ProspectPersonId,
                $"{prospect.ProspectName} has a contract offer pending GM approval.");
            updatedScenario = pending.ScenarioSnapshot;
            inbox.AddRange(pending.InboxItems);
        }

        QueueProspectEvent(registry, updatedScenario, LegacyEventType.ProspectContractOffered, decision, prospect, $"{prospect.ProspectName} contract offer prepared.");
        inbox.AddRange(CreateDecisionInbox(updatedScenario, LegacyEventType.ProspectContractOffered, prospect, "Prospect contract offer", $"{prospect.ProspectName} has a pending signing decision."));
        return BuildResult(true, updatedScenario, updatedScenario.ProspectRights.Single(item => item.ProspectPersonId == prospect.ProspectPersonId), inbox, $"{prospect.ProspectName} contract decision is pending GM approval.");
    }

    private ProspectDecisionResult InviteToCamp(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        DraftRightsRecord prospect,
        ProspectDecision decision)
    {
        var invited = prospect with { Status = ProspectStatus.InvitedToCamp };
        var updatedScenario = ReplaceProspect(scenario, invited);
        var inbox = new List<AlphaInboxItem>();

        if (updatedScenario.TrainingCamp is not null)
        {
            var camp = _trainingCamp.InvitePlayer(
                registry,
                updatedScenario,
                prospect.ProspectPersonId,
                prospect.Position,
                TrainingCampInviteType.DraftedProspect,
                PlayerAcquisitionSource.Unknown);
            updatedScenario = camp.ScenarioSnapshot;
            inbox.AddRange(camp.InboxItems);
        }

        QueueProspectEvent(registry, updatedScenario, LegacyEventType.ProspectInvitedToCamp, decision, prospect, $"{prospect.ProspectName} was invited to training camp.");
        inbox.AddRange(CreateDecisionInbox(updatedScenario, LegacyEventType.ProspectInvitedToCamp, prospect, "Prospect invited to camp", $"{prospect.ProspectName} has been added to the camp invite list."));
        return BuildResult(true, updatedScenario, updatedScenario.ProspectRights.Single(item => item.ProspectPersonId == prospect.ProspectPersonId), inbox, $"{prospect.ProspectName} invited to training camp.");
    }

    private ProspectDecisionResult UpdateStatus(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        DraftRightsRecord prospect,
        ProspectDecision decision,
        ProspectStatus status)
    {
        var updatedProspect = prospect with { Status = status };
        var updatedScenario = ReplaceProspect(scenario, updatedProspect);
        var eventType = EventTypeFor(status);

        QueueProspectEvent(registry, updatedScenario, eventType, decision, prospect, $"{prospect.ProspectName} status changed to {status}.");
        var inbox = CreateDecisionInbox(
            updatedScenario,
            eventType,
            updatedProspect,
            TitleFor(status, prospect.ProspectName),
            SummaryFor(status, prospect.ProspectName));

        return BuildResult(true, updatedScenario, updatedProspect, inbox, $"{prospect.ProspectName} is now {status}.");
    }

    private static string? ValidateDecision(
        NewGmScenarioSnapshot scenario,
        Rulebook rulebook,
        DraftRightsRecord prospect,
        ProspectDecisionType decisionType)
    {
        if (rulebook.DraftRules is { DraftEnabled: false } && scenario.ProspectRights.Count == 0)
        {
            return "This league does not use the amateur draft flow.";
        }

        if (prospect.Status is ProspectStatus.Released or ProspectStatus.Declined)
        {
            return "Prospect rights have already been released or declined.";
        }

        if (decisionType is ProspectDecisionType.ReturnToJunior or ProspectDecisionType.ReturnToYouthTeam
            && !SupportsJuniorReturn(rulebook))
        {
            return "Return to junior/youth is unavailable for this rulebook.";
        }

        if (decisionType == ProspectDecisionType.AssignToAffiliate
            && !SupportsAffiliateAssignment(scenario, rulebook))
        {
            return "Assign to affiliate is unavailable for this organization/rulebook.";
        }

        return null;
    }

    private static bool SupportsJuniorReturn(Rulebook rulebook) =>
        rulebook.LeagueType.Contains("junior", StringComparison.OrdinalIgnoreCase)
        || rulebook.LeagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase);

    private static bool SupportsAffiliateAssignment(NewGmScenarioSnapshot scenario, Rulebook rulebook) =>
        !string.IsNullOrWhiteSpace(scenario.Organization.AffiliateOrganizationId)
        && (rulebook.AffiliateRules is { AffiliateEnabled: true }
            || rulebook.LeagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase));

    private static LegacyEventType EventTypeFor(ProspectStatus status) =>
        status switch
        {
            ProspectStatus.Signed => LegacyEventType.ProspectSigned,
            ProspectStatus.InvitedToCamp => LegacyEventType.ProspectInvitedToCamp,
            ProspectStatus.ReturnedToJunior or ProspectStatus.ReturnedToYouthTeam => LegacyEventType.ProspectReturned,
            ProspectStatus.AssignedToAffiliate => LegacyEventType.ProspectAssignedToAffiliate,
            ProspectStatus.Released or ProspectStatus.Declined => LegacyEventType.ProspectRightsReleased,
            _ => LegacyEventType.ProspectDecisionMade
        };

    private static string TitleFor(ProspectStatus status, string prospectName) =>
        status switch
        {
            ProspectStatus.ReturnedToJunior => $"Prospect returned to junior: {prospectName}",
            ProspectStatus.ReturnedToYouthTeam => $"Prospect returned to youth team: {prospectName}",
            ProspectStatus.AssignedToAffiliate => $"Prospect assigned to affiliate: {prospectName}",
            ProspectStatus.Released => $"Prospect rights released: {prospectName}",
            ProspectStatus.Declined => $"Prospect signing declined: {prospectName}",
            _ => $"Prospect decision: {prospectName}"
        };

    private static string SummaryFor(ProspectStatus status, string prospectName) =>
        status switch
        {
            ProspectStatus.ReturnedToJunior => $"{prospectName} was returned to junior while the club keeps rights where allowed.",
            ProspectStatus.ReturnedToYouthTeam => $"{prospectName} was returned to a youth/development team and removed from immediate camp consideration.",
            ProspectStatus.AssignedToAffiliate => $"{prospectName} was assigned to the affiliate track under the active rulebook.",
            ProspectStatus.Released => $"{prospectName}'s rights were released by the GM.",
            ProspectStatus.Declined => $"{prospectName}'s signing decision was declined by the GM.",
            _ => $"{prospectName} had a prospect decision recorded."
        };

    private static IReadOnlyList<AlphaInboxItem> CreateDecisionInbox(
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        DraftRightsRecord prospect,
        string title,
        string summary) =>
        new[]
        {
            Inbox(scenario, eventType, title, summary, prospect.ProspectPersonId),
            Inbox(
                scenario,
                LegacyEventType.OwnerDraftReaction,
                "Owner prospect reaction",
                $"{scenario.AlphaSnapshot.Owner.Name} wants {prospect.ProspectName}'s path handled with patience.",
                prospect.ProspectPersonId),
            Inbox(
                scenario,
                LegacyEventType.ScoutRecommendationUpdated,
                "Head scout prospect note",
                $"{scenario.AlphaSnapshot.Scout.Name}: {prospect.ProjectionText}",
                prospect.ProspectPersonId)
        };

    private static void QueueProspectEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        ProspectDecision decision,
        DraftRightsRecord prospect,
        string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            ToDateTimeOffset(decision.DecisionDate),
            eventType,
            eventType == LegacyEventType.ProspectRightsReleased ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            $"Prospect decision: {prospect.ProspectName}",
            description,
            new LegacyEventContext(PrimaryPersonId: prospect.ProspectPersonId, OrganizationId: scenario.Organization.OrganizationId),
            new Dictionary<string, object?>
            {
                ["prospect_person_id"] = prospect.ProspectPersonId,
                ["decision_type"] = decision.DecisionType.ToString(),
                ["prospect_status"] = prospect.Status.ToString()
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string summary,
        string personId) =>
        new(
            InboxItemId: $"inbox:prospect:{Guid.NewGuid():N}",
            Date: ToDateTimeOffset(scenario.CurrentDate),
            EventType: eventType,
            Severity: eventType == LegacyEventType.ProspectRightsReleased ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            Title: title,
            Summary: summary,
            PrimaryPersonId: personId);

    private static NewGmScenarioSnapshot ReplaceProspect(NewGmScenarioSnapshot scenario, DraftRightsRecord prospect) =>
        scenario with
        {
            ProspectRights = scenario.ProspectRights
                .Select(item => item.ProspectPersonId == prospect.ProspectPersonId ? prospect : item)
                .ToArray()
        };

    private static DraftRightsRecord FindProspect(NewGmScenarioSnapshot scenario, string prospectPersonId) =>
        scenario.ProspectRights.SingleOrDefault(item => item.ProspectPersonId == prospectPersonId)
        ?? throw new ArgumentException("Drafted prospect was not found.", nameof(prospectPersonId));

    private static Rulebook ResolveRulebook(EngineRegistry registry) =>
        registry.Rulebook ?? RulebookPresets.CreateJuniorMajor();

    private static ProspectDecisionResult BuildResult(
        bool success,
        NewGmScenarioSnapshot scenario,
        DraftRightsRecord prospect,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        string message)
    {
        var result = new ProspectDecisionResult(success, scenario, prospect, inboxItems, message);
        result.Validate();
        return result;
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 13, 0, 0, TimeSpan.Zero);
}
