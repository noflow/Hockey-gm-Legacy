using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class PendingGmActionService
{
    public PendingGmActionResult CreatePendingAction(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        PendingGmActionType actionType,
        string personId,
        string reason,
        string recommendedAction,
        RosterPosition? position = null,
        PlayerAcquisitionSource acquisitionSource = PlayerAcquisitionSource.Unknown,
        ContractType? contractType = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var personName = ResolvePersonName(scenario, personId);
        var action = new PendingGmAction(
            ActionId: $"pending-gm:{Guid.NewGuid():N}",
            ActionType: actionType,
            Status: PendingGmActionStatus.Pending,
            CreatedOn: scenario.CurrentDate,
            PersonId: personId,
            PersonName: personName,
            OrganizationId: scenario.Organization.OrganizationId,
            Title: BuildTitle(actionType, personName),
            Reason: reason,
            RecommendedAction: recommendedAction,
            Position: position,
            AcquisitionSource: acquisitionSource,
            ContractType: contractType ?? DefaultContractType(actionType));
        action.Validate();

        var updatedScenario = scenario with
        {
            PendingActions = scenario.PendingActions.Append(action).ToArray()
        };

        QueuePendingEvent(registry, action, scenario.CurrentDate, "Pending GM action created", action.Title);
        var inbox = new[] { ToInboxItem(action, "GM approval needed", $"{action.Reason} Recommended: {action.RecommendedAction}") };
        return BuildResult(true, updatedScenario, action, inbox, $"{action.Title} is waiting for GM approval.");
    }

    public PendingGmActionResult CreateForRecruitCommitment(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string recruitPersonId,
        string reason = "Recruit has committed and needs GM approval before any agreement is signed.") =>
        CreatePendingAction(
            registry,
            scenario,
            PendingGmActionType.SignRecruit,
            recruitPersonId,
            reason,
            "Approve a junior player agreement or decline the signing.",
            GuessPosition(scenario, recruitPersonId),
            PlayerAcquisitionSource.FreeAgentSigning,
            ContractType.JuniorPlayerAgreement);

    public PendingGmActionResult CreateForDraftPickReady(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string prospectPersonId,
        string reason = "Drafted prospect is ready for a GM signing decision.") =>
        CreatePendingAction(
            registry,
            scenario,
            PendingGmActionType.SignDraftPick,
            prospectPersonId,
            reason,
            "Approve a junior player agreement or decline the signing.",
            GuessPosition(scenario, prospectPersonId),
            PlayerAcquisitionSource.Unknown,
            ContractType.JuniorPlayerAgreement);

    public PendingGmActionResult Approve(EngineRegistry registry, NewGmScenarioSnapshot scenario, string actionId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var action = FindOpenAction(scenario, actionId);

        return action.ActionType switch
        {
            PendingGmActionType.SignRecruit or PendingGmActionType.SignDraftPick or PendingGmActionType.SignFreeAgent or PendingGmActionType.ApproveContract =>
                ApproveContractAction(registry, scenario, action),
            PendingGmActionType.AddToRoster =>
                ApproveRosterAdd(registry, scenario, action),
            PendingGmActionType.InviteToCamp =>
                CompleteWithoutDomainMutation(registry, scenario, action, "Camp invite approved."),
            PendingGmActionType.ApproveTrade =>
                ApproveTradeAction(registry, scenario, action),
            PendingGmActionType.ReleasePlayer or PendingGmActionType.CutPlayer or PendingGmActionType.ReturnToJuniorTeam or PendingGmActionType.AssignToAffiliate or PendingGmActionType.ReturnToParent or PendingGmActionType.PlaceOnWaivers =>
                CompleteWithoutDomainMutation(registry, scenario, action, "GM-controlled camp/roster action approved for manual processing."),
            PendingGmActionType.DeclineContract or PendingGmActionType.DeclineTrade =>
                Decline(registry, scenario, actionId),
            _ => BuildResult(false, scenario, action with { Status = PendingGmActionStatus.Failed }, Array.Empty<AlphaInboxItem>(), "Unsupported pending GM action.")
        };
    }

    public PendingGmActionResult Decline(EngineRegistry registry, NewGmScenarioSnapshot scenario, string actionId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var action = FindOpenAction(scenario, actionId);
        if (action.ActionType == PendingGmActionType.ApproveTrade)
        {
            return DeclineTradeAction(registry, scenario, action);
        }

        var declined = action with { Status = PendingGmActionStatus.Declined };
        var updatedScenario = UpdateFreeAgentStatus(
            UpdateProspectStatus(ReplaceAction(scenario, declined), action, ProspectStatus.Declined),
            action,
            FreeAgentStatus.Rejected);

        QueuePendingEvent(registry, declined, scenario.CurrentDate, "Pending GM action declined", $"{declined.Title} was declined by the GM.");
        if (action.ActionType is PendingGmActionType.ApproveContract or PendingGmActionType.DeclineContract)
        {
            QueueContractDecisionEvent(registry, declined, scenario.CurrentDate, LegacyEventType.ContractDeclinedByGM, "Contract declined by GM", $"{declined.PersonName}'s contract path was declined by the GM.");
        }

        var inbox = new[] { ToInboxItem(declined, "Pending action declined", $"{declined.Title} was declined. No contract or roster change was made.") };
        return BuildResult(true, updatedScenario, declined, inbox, $"{declined.Title} declined. No roster or contract change was made.");
    }

    private PendingGmActionResult ApproveTradeAction(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        PendingGmAction action)
    {
        var trade = new TradeService().CompleteAcceptedTrade(registry, scenario, action.PersonId);
        if (!trade.Success)
        {
            var failed = action with { Status = PendingGmActionStatus.Failed };
            return BuildResult(false, ReplaceAction(scenario, failed), failed, trade.InboxItems, trade.Message);
        }

        var completed = action with { Status = PendingGmActionStatus.Completed };
        var updated = ReplaceAction(trade.ScenarioSnapshot, completed);
        var inbox = trade.InboxItems
            .Append(ToInboxItem(completed, "Pending action approved", trade.Message))
            .ToArray();
        return BuildResult(true, updated, completed, inbox, trade.Message, trade.LeagueTransactions);
    }

    private PendingGmActionResult DeclineTradeAction(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        PendingGmAction action)
    {
        var trade = new TradeService().DeclineAcceptedTrade(registry, scenario, action.PersonId);
        var declined = action with { Status = PendingGmActionStatus.Declined };
        var updated = ReplaceAction(trade.ScenarioSnapshot, declined);
        var inbox = trade.InboxItems
            .Append(ToInboxItem(declined, "Pending action declined", trade.Message))
            .ToArray();
        return BuildResult(true, updated, declined, inbox, trade.Message, trade.LeagueTransactions);
    }

    private PendingGmActionResult ApproveContractAction(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        PendingGmAction action)
    {
        var offered = registry.ContractEngine.CreateOffer(
            BuildContractOffer(scenario, action),
            registry.Rulebook is null ? null : new ContractRuleValidator(registry.Rulebook));
        var signed = registry.ContractEngine.SignContract(offered, scenario.CurrentDate).Contract;
        var completed = action with { Status = PendingGmActionStatus.Completed };
        var contracts = scenario.Contracts.Append(signed).ToArray();
        var alphaSnapshot = scenario.AlphaSnapshot with { Contracts = contracts };
        var updatedScenario = UpdateFreeAgentStatus(UpdateProspectStatus(ReplaceAction(scenario, completed), action, ProspectStatus.Signed), action, FreeAgentStatus.Signed) with
        {
            Contracts = contracts,
            AlphaSnapshot = alphaSnapshot
        };
        updatedScenario = new CareerHistoryService().RecordContractSigned(updatedScenario, signed, completed.PersonName, action.ActionType);
        if (action.ActionType == PendingGmActionType.SignDraftPick)
        {
            var prospect = updatedScenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == action.PersonId);
            if (prospect is not null)
            {
                updatedScenario = new PlayerPipelineService().UpsertProspect(updatedScenario, prospect, $"{completed.PersonName} signed an entry-level contract.");
            }
        }

        QueueContractDecisionEvent(registry, completed, scenario.CurrentDate, LegacyEventType.ContractApprovedByGM, "Contract approved by GM", $"{completed.PersonName}'s contract was approved and signed by the GM.");
        var inbox = new[] { ToInboxItem(completed, "Pending action approved", $"{completed.PersonName} signed a {signed.ContractType} after GM approval.") };
        return BuildResult(true, updatedScenario, completed, inbox, $"{completed.PersonName} contract approved and signed.");
    }

    private PendingGmActionResult ApproveRosterAdd(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        PendingGmAction action)
    {
        var move = new RosterMove(
            RosterMoveType.Add,
            action.PersonId,
            scenario.CurrentDate,
            action.Position ?? GuessPosition(scenario, action.PersonId),
            RosterStatus.Active,
            AcquisitionSource: action.AcquisitionSource,
            Reason: action.Reason);
        var result = registry.RosterEngine.AddPlayer(
            scenario.AlphaSnapshot.Roster,
            move,
            registry.Rulebook is null ? null : new RosterRuleValidator(registry.Rulebook));
        if (!result.Success)
        {
            var failed = action with { Status = PendingGmActionStatus.Failed };
            var updatedScenario = ReplaceAction(scenario, failed);
            return BuildResult(false, updatedScenario, failed, Array.Empty<AlphaInboxItem>(), result.Message);
        }

        var completed = action with { Status = PendingGmActionStatus.Completed };
        var alphaSnapshot = scenario.AlphaSnapshot with { Roster = result.Roster };
        var updated = ReplaceAction(scenario, completed) with { AlphaSnapshot = alphaSnapshot };
        var inbox = new[] { ToInboxItem(completed, "Pending action approved", $"{completed.PersonName} was added to the roster after GM approval.") };
        return BuildResult(true, updated, completed, inbox, $"{completed.PersonName} added to roster.");
    }

    private PendingGmActionResult CompleteWithoutDomainMutation(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        PendingGmAction action,
        string message)
    {
        var completed = action with { Status = PendingGmActionStatus.Completed };
        var updatedScenario = ReplaceAction(scenario, completed);
        QueuePendingEvent(registry, completed, scenario.CurrentDate, "Pending GM action approved", message);
        var inbox = new[] { ToInboxItem(completed, "Pending action approved", message) };
        return BuildResult(true, updatedScenario, completed, inbox, message);
    }

    private static ContractOffer BuildContractOffer(NewGmScenarioSnapshot scenario, PendingGmAction action) =>
        new(
            OfferId: $"pending-{action.ActionId.Replace(':', '-')}",
            PersonId: action.PersonId,
            OrganizationId: action.OrganizationId,
            ContractType: action.ContractType ?? ContractType.JuniorPlayerAgreement,
            Term: ContractExpiryCalendar.TermForYears(scenario.CurrentDate, scenario.Season.Settings, action.OfferedTermYears ?? scenario.FreeAgentMarket?.Find(action.PersonId)?.ContractAsk.TermYears ?? 1),
            Money: new ContractMoney(
                SalaryOrStipend: action.OfferedSalary ?? scenario.FreeAgentMarket?.Find(action.PersonId)?.ContractAsk.AnnualAmount ?? 1_500m,
                Currency: scenario.FreeAgentMarket?.Find(action.PersonId)?.ContractAsk.Currency ?? "CAD"),
            Clauses: Array.Empty<ContractClause>(),
            OfferedOn: scenario.CurrentDate,
            Notes: string.Join(" ", new[] { action.Reason, action.RolePromise, action.DevelopmentPromise, action.ContractNotes }.Where(note => !string.IsNullOrWhiteSpace(note))));

    private static PendingGmAction FindOpenAction(NewGmScenarioSnapshot scenario, string actionId)
    {
        var action = scenario.PendingActions.SingleOrDefault(item => item.ActionId == actionId)
            ?? throw new ArgumentException("Pending GM action was not found.", nameof(actionId));
        if (!action.IsOpen)
        {
            throw new InvalidOperationException("Only pending GM actions can be approved or declined.");
        }

        return action;
    }

    private static NewGmScenarioSnapshot ReplaceAction(NewGmScenarioSnapshot scenario, PendingGmAction action) =>
        scenario with
        {
            PendingActions = scenario.PendingActions
                .Select(item => item.ActionId == action.ActionId ? action : item)
                .ToArray()
        };

    private static NewGmScenarioSnapshot UpdateProspectStatus(
        NewGmScenarioSnapshot scenario,
        PendingGmAction action,
        ProspectStatus status)
    {
        if (action.ActionType != PendingGmActionType.SignDraftPick
            || scenario.ProspectRights.All(record => record.ProspectPersonId != action.PersonId))
        {
            return scenario;
        }

        return scenario with
        {
            ProspectRights = scenario.ProspectRights
                .Select(record => record.ProspectPersonId == action.PersonId ? record with { Status = status } : record)
                .ToArray()
        };
    }

    private static NewGmScenarioSnapshot UpdateFreeAgentStatus(
        NewGmScenarioSnapshot scenario,
        PendingGmAction action,
        FreeAgentStatus status)
    {
        if (action.ActionType != PendingGmActionType.SignFreeAgent
            && (action.ActionType != PendingGmActionType.ApproveContract || scenario.FreeAgentMarket?.Find(action.PersonId) is null))
        {
            return scenario;
        }

        return FreeAgentMarketService.MarkStatus(scenario, action.PersonId, status);
    }

    private static PendingGmActionResult BuildResult(
        bool success,
        NewGmScenarioSnapshot scenario,
        PendingGmAction action,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        string message,
        IReadOnlyList<LeagueTransaction>? leagueTransactions = null)
    {
        var result = new PendingGmActionResult(success, scenario, action, inboxItems, message, leagueTransactions);
        result.Validate();
        return result;
    }

    private static AlphaInboxItem ToInboxItem(PendingGmAction action, string title, string summary) =>
        new(
            InboxItemId: $"inbox:pending-gm:{Guid.NewGuid():N}",
            Date: new DateTimeOffset(action.CreatedOn.Year, action.CreatedOn.Month, action.CreatedOn.Day, 14, 0, 0, TimeSpan.Zero),
            EventType: LegacyEventType.PendingGmActionCreated,
            Severity: action.Status == PendingGmActionStatus.Pending ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            Title: $"{title}: {action.PersonName}",
            Summary: summary,
            PrimaryPersonId: action.PersonId);

    private static void QueuePendingEvent(
        EngineRegistry registry,
        PendingGmAction action,
        DateOnly date,
        string title,
        string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 14, 0, 0, TimeSpan.Zero),
            LegacyEventType.PendingGmActionCreated,
            action.Status == PendingGmActionStatus.Pending ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: action.PersonId, OrganizationId: action.OrganizationId),
            new Dictionary<string, object?>
            {
                ["pending_gm_action_id"] = action.ActionId,
                ["pending_gm_action_type"] = action.ActionType.ToString(),
                ["pending_gm_action_status"] = action.Status.ToString()
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static void QueueContractDecisionEvent(
        EngineRegistry registry,
        PendingGmAction action,
        DateOnly date,
        LegacyEventType eventType,
        string title,
        string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 14, 30, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: action.PersonId, OrganizationId: action.OrganizationId),
            new Dictionary<string, object?>
            {
                ["person_name"] = action.PersonName,
                ["team_name"] = action.OrganizationId,
                ["reason"] = action.Reason,
                ["annual_salary"] = action.OfferedSalary,
                ["term_years"] = action.OfferedTermYears
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static string BuildTitle(PendingGmActionType actionType, string personName) =>
        actionType switch
        {
            PendingGmActionType.SignRecruit => $"Sign recruit: {personName}",
            PendingGmActionType.SignDraftPick => $"Sign draft pick: {personName}",
            PendingGmActionType.SignFreeAgent => $"Sign free agent: {personName}",
            PendingGmActionType.InviteToCamp => $"Invite to camp: {personName}",
            PendingGmActionType.AddToRoster => $"Add to roster: {personName}",
            PendingGmActionType.ReleasePlayer => $"Release player: {personName}",
            PendingGmActionType.CutPlayer => $"Cut player: {personName}",
            PendingGmActionType.ReturnToJuniorTeam => $"Return to junior/youth team: {personName}",
            PendingGmActionType.AssignToAffiliate => $"Assign to affiliate: {personName}",
            PendingGmActionType.ReturnToParent => $"Return to parent: {personName}",
            PendingGmActionType.PlaceOnWaivers => $"Place on waivers: {personName}",
            PendingGmActionType.ApproveContract => $"Approve contract: {personName}",
            PendingGmActionType.DeclineContract => $"Decline contract: {personName}",
            PendingGmActionType.ApproveTrade => $"Approve trade: {personName}",
            PendingGmActionType.DeclineTrade => $"Decline trade: {personName}",
            _ => $"Pending GM action: {personName}"
        };

    private static ContractType? DefaultContractType(PendingGmActionType actionType) =>
        actionType is PendingGmActionType.SignRecruit or PendingGmActionType.SignDraftPick or PendingGmActionType.SignFreeAgent or PendingGmActionType.ApproveContract
            ? ContractType.JuniorPlayerAgreement
            : null;

    private static RosterPosition GuessPosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? RosterPosition.Unknown;

    private static string ResolvePersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.TradeOffers.FirstOrDefault(offer => offer.TradeOfferId == personId)?.OtherOrganizationName
        ?? personId;
}
