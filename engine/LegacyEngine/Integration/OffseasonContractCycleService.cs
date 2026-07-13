using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

/// <summary>
/// Runs the shared contract/rights/market pass once per world day. It prepares
/// information and deadlines but never approves a GM-controlled decision.
/// </summary>
public sealed class OffseasonContractCycleService
{
    private readonly ContractExpiryService _expiry = new();
    private readonly RfaUfaService _rights = new();
    private readonly ArbitrationService _arbitration = new();
    private readonly FreeAgencyV3Service _freeAgency = new();
    private readonly ContractMarketService _market = new();

    public OffseasonContractCycleResult Process(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var rulebook = registry.Rulebook ?? scenario.LeagueProfile.Rulebook;
        var beforeContracts = scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .DistinctBy(contract => contract.ContractId)
            .ToDictionary(contract => contract.ContractId, StringComparer.Ordinal);
        var prepared = _expiry.ProcessExpiredContracts(scenario, rulebook);
        prepared = _rights.EnsureRights(prepared, rulebook);
        prepared = _arbitration.EnsureArbitration(prepared, rulebook);

        var freeAgency = _freeAgency.AdvanceMarket(registry, prepared);
        prepared = freeAgency.ScenarioSnapshot;

        var afterContracts = prepared.Contracts
            .Concat(prepared.AlphaSnapshot.Contracts)
            .DistinctBy(contract => contract.ContractId)
            .ToDictionary(contract => contract.ContractId, StringComparer.Ordinal);
        var expired = afterContracts.Values
            .Where(contract => contract.Status == ContractStatus.Expired
                && beforeContracts.TryGetValue(contract.ContractId, out var previous)
                && previous.Status != ContractStatus.Expired)
            .OrderBy(contract => contract.PersonId, StringComparer.Ordinal)
            .ToArray();

        var state = prepared.OffseasonContractCycle;
        var notices = state.ProcessedExpiryNotices.ToHashSet(StringComparer.Ordinal);
        var inbox = new List<AlphaInboxItem>(freeAgency.InboxItems);
        foreach (var contract in expired.Take(3))
        {
            var noticeKey = $"{contract.ContractId}:{contract.Term.EndDate:yyyy-MM-dd}";
            if (!notices.Add(noticeKey))
            {
                continue;
            }

            var name = PersonName(prepared, contract.PersonId);
            inbox.Add(new AlphaInboxItem(
                $"inbox:contract-expired:{contract.ContractId}:{Guid.NewGuid():N}",
                At(prepared.CurrentDate, 12),
                LegacyEventType.ContractExpired,
                LegacyEventSeverity.Warning,
                $"Contract expired: {name}",
                $"{name}'s {contract.ContractType} expired on {contract.Term.EndDate:yyyy-MM-dd}. Salary is no longer counted; review RFA/UFA rights or begin a new market negotiation.",
                contract.PersonId));
        }

        var marketPhase = prepared.FreeAgencyMarketState?.Window.Phase;
        var phaseNotices = state.ProcessedMarketPhaseNotices.ToHashSet(StringComparer.Ordinal);
        if (marketPhase is FreeAgencyPhase.OpeningDay or FreeAgencyPhase.Closed)
        {
            var phaseKey = $"{marketPhase}:{prepared.CurrentDate:yyyy-MM-dd}";
            if (phaseNotices.Add(phaseKey))
            {
                var title = marketPhase == FreeAgencyPhase.OpeningDay ? "Free agency is open" : "Free agency is closed";
                var summary = marketPhase == FreeAgencyPhase.OpeningDay
                    ? "The free-agent market is open. Compare targets, competing offers, budget, cap room, and roster fit before approving a signing."
                    : "The free-agent market is closed. Existing offers and pending GM approvals remain visible in the Contract Market.";
                inbox.Add(new AlphaInboxItem(
                    $"inbox:free-agency-phase:{Guid.NewGuid():N}",
                    At(prepared.CurrentDate, 13),
                    marketPhase == FreeAgencyPhase.OpeningDay ? LegacyEventType.FreeAgencyOpened : LegacyEventType.FreeAgencyClosed,
                    LegacyEventSeverity.Notice,
                    title,
                    summary,
                    null));
            }
        }

        prepared = prepared with
        {
            OffseasonContractCycle = state with
            {
                LastProcessedDate = prepared.CurrentDate,
                ExpiryNotices = notices.OrderBy(item => item, StringComparer.Ordinal).Take(400).ToArray(),
                MarketPhaseNotices = phaseNotices.OrderBy(item => item, StringComparer.Ordinal).Take(200).ToArray()
            }
        };

        var market = _market.BuildSummary(prepared, rulebook);
        var logs = new List<string>
        {
            $"Contract cycle checked {afterContracts.Count} contract record(s).",
            expired.Length == 0 ? "No contracts expired today." : $"Expired {expired.Length} contract(s); salary commitments were removed from active payroll.",
            $"Rights refreshed for {prepared.PlayerRightsDecisions.Count} player(s).",
            $"Free agency phase: {prepared.FreeAgencyMarketState?.Window.Phase.ToString() ?? "not configured"}.",
            $"Contract Market: {market.OpenNegotiations} open negotiation(s), {market.Deadlines.Count(deadline => deadline.IsActionable)} actionable deadline(s)."
        };
        var pendingCount = prepared.PendingActions.Count(action => action.IsOpen)
            - scenario.PendingActions.Count(action => action.IsOpen);
        var result = new OffseasonContractCycleResult(
            true,
            prepared,
            market,
            logs,
            inbox,
            freeAgency.LeagueTransactions,
            expired.Length,
            Math.Max(0, pendingCount),
            expired.Length == 0
                ? $"Contract cycle complete. {market.OpenNegotiations} negotiation(s) remain under GM control."
                : $"Contract cycle complete. {expired.Length} contract(s) expired and the market was refreshed.");
        result.Validate();
        return result;
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.PlayerRightsDecisions.FirstOrDefault(item => item.PersonId == personId)?.PlayerName
        ?? personId;

    private static DateTimeOffset At(DateOnly date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.Zero);
}
