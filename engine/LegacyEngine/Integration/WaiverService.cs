using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class WaiverService
{
    public WaiverWire EnsureWire(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var priority = BuildPriority(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        var wire = scenario.WaiverWire.Priority.Count == 0
            ? scenario.WaiverWire with { Priority = priority }
            : scenario.WaiverWire;
        wire.Validate();
        return wire;
    }

    public WaiverEligibility EvaluateEligibility(NewGmScenarioSnapshot scenario, string personId, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        var resolvedRulebook = rulebook ?? scenario.LeagueProfile.Rulebook;
        var rules = resolvedRulebook.WaiverRules ?? new WaiverRules { WaiversEnabled = IsProfessional(resolvedRulebook) };
        var player = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        var pipeline = scenario.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId);
        var name = PersonName(scenario, personId);
        var position = player?.Position ?? pipelinePosition(scenario, personId);
        var age = player?.Age ?? scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate);
        var signed = scenario.Contracts.Any(contract => contract.PersonId == personId && contract.Status == LegacyEngine.Contracts.ContractStatus.Signed);
        var hasAffiliate = !string.IsNullOrWhiteSpace(scenario.Organization.AffiliateOrganizationId) || pipeline?.AffiliateOrganization is not null;
        var assigned = player?.Status == RosterStatus.AssignedToAffiliate || pipeline?.PipelineStatus is PlayerPipelineStatus.AssignedToAhl or PlayerPipelineStatus.SentDown;

        if (!rules.WaiversEnabled)
        {
            return Valid(new WaiverEligibility(personId, name, position, age, WaiverStatus.WaiverExempt, false, true, false, false, assigned, "Waivers are disabled by this league rulebook."));
        }

        if (player is null && pipeline is null)
        {
            return Valid(new WaiverEligibility(personId, name, position, age, WaiverStatus.WaiverExempt, true, true, false, false, false, "Player is not in this organization's roster or pipeline."));
        }

        if (!signed)
        {
            return Valid(new WaiverEligibility(personId, name, position, age, WaiverStatus.WaiverExempt, true, true, false, false, assigned, "Unsigned players do not require professional waivers."));
        }

        var seasons = scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == personId)?.Seasons ?? Math.Max(0, (age ?? 18) - 18);
        var games = scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == personId)?.GamesPlayed
            ?? scenario.PlayerStats.FirstOrDefault(stats => stats.PersonId == personId)?.GamesPlayed
            ?? 0;
        var ageExempt = age.HasValue && age.Value <= rules.ExemptAgeCutoff;
        var experienceExempt = seasons < rules.ExemptProfessionalSeasons && games < rules.ExemptGamesPlayed;
        var isExempt = ageExempt || experienceExempt;
        var reason = isExempt
            ? $"{name} is waiver exempt: age {(age?.ToString() ?? "unknown")}, {seasons} pro season(s), {games} tracked game(s). Rulebook exemption: age <= {rules.ExemptAgeCutoff} or under {rules.ExemptProfessionalSeasons} seasons and {rules.ExemptGamesPlayed} games."
            : $"{name} requires waivers: age {(age?.ToString() ?? "unknown")}, {seasons} pro season(s), {games} tracked game(s), signed contract.";

        return Valid(new WaiverEligibility(
            personId,
            name,
            position,
            age,
            isExempt ? WaiverStatus.WaiverExempt : WaiverStatus.RequiresWaivers,
            true,
            isExempt,
            !isExempt,
            hasAffiliate,
            assigned,
            reason));
    }

    public WaiverResult PlaceOnWaivers(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, string reason = "Assignment to affiliate requires waivers.")
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var rulebook = registry.Rulebook ?? scenario.LeagueProfile.Rulebook;
        var rules = rulebook.WaiverRules ?? new WaiverRules { WaiversEnabled = IsProfessional(rulebook) };
        var eligibility = EvaluateEligibility(scenario, personId, rulebook);
        if (!eligibility.WaiversEnabled)
        {
            return Result(false, scenario, eligibility, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Waivers are disabled by this rulebook.");
        }

        if (!eligibility.RequiresWaivers)
        {
            return AssignToAffiliate(registry, scenario, personId, $"Waiver exempt assignment: {eligibility.Reason}");
        }

        var existing = scenario.WaiverWire.OpenTransactions.FirstOrDefault(transaction => transaction.PersonId == personId);
        if (existing is not null)
        {
            return Result(false, scenario, eligibility, existing, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"{eligibility.PlayerName} is already on waivers.");
        }

        var deadline = ToDateTimeOffset(scenario.CurrentDate).AddHours(Math.Max(1, rules.ClaimWindowHours));
        var transaction = new WaiverTransaction(
            $"waiver:{Guid.NewGuid():N}",
            WaiverTransactionType.Placement,
            WaiverStatus.OnWaivers,
            scenario.CurrentDate,
            deadline,
            personId,
            eligibility.PlayerName,
            eligibility.Position,
            eligibility.Age,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.Organization.AffiliateOrganizationId,
            AffiliateName(scenario),
            reason,
            true);
        var wire = EnsureWire(scenario, rulebook) with { Transactions = scenario.WaiverWire.Transactions.Append(transaction).ToArray() };
        var updated = AddHistory(scenario with { WaiverWire = wire }, personId, eligibility.PlayerName, WaiverStatus.OnWaivers, scenario.Organization.OrganizationId, scenario.Organization.Name, $"{eligibility.PlayerName} was placed on waivers. Claim deadline: {deadline:yyyy-MM-dd HH:mm}.");
        QueueEvent(registry, updated, personId, LegacyEventType.PlayerPlacedOnWaivers, "Player placed on waivers", $"{eligibility.PlayerName} was placed on waivers.");
        var news = Transaction(updated, personId, eligibility.PlayerName, LeagueTransactionType.WaiverPlaced, $"{scenario.Organization.Name} placed {eligibility.PlayerName} on waivers. {reason}");
        return Result(true, updated, eligibility, transaction, null, Array.Empty<AlphaInboxItem>(), new[] { news }, $"{eligibility.PlayerName} placed on waivers until {deadline:yyyy-MM-dd HH:mm}.");
    }

    public WaiverResult SubmitClaim(NewGmScenarioSnapshot scenario, string personId, string claimingOrganizationId, string reason = "Claim submitted based on roster need.")
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var transaction = scenario.WaiverWire.OpenTransactions.FirstOrDefault(item => item.PersonId == personId);
        if (transaction is null)
        {
            return Result(false, scenario, null, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Player is not currently on waivers.");
        }

        var team = scenario.LeagueProfile.Teams.FirstOrDefault(item => item.OrganizationId == claimingOrganizationId)
            ?? throw new ArgumentException("Claiming organization was not found.", nameof(claimingOrganizationId));
        var priority = EnsureWire(scenario).Priority.FirstOrDefault(item => item.OrganizationId == claimingOrganizationId)?.Rank
            ?? scenario.LeagueProfile.Teams.TakeWhile(item => item.OrganizationId != claimingOrganizationId).Count() + 1;
        var claim = new WaiverClaim(
            $"waiver-claim:{Guid.NewGuid():N}",
            transaction.TransactionId,
            personId,
            transaction.PlayerName,
            team.OrganizationId,
            team.TeamName,
            Math.Max(1, priority),
            scenario.CurrentDate,
            reason);
        var claims = scenario.WaiverWire.Claims.Any(item => item.TransactionId == transaction.TransactionId && item.ClaimingOrganizationId == team.OrganizationId)
            ? scenario.WaiverWire.Claims
            : scenario.WaiverWire.Claims.Append(claim).ToArray();
        var updated = scenario with { WaiverWire = scenario.WaiverWire with { Claims = claims } };
        return Result(true, updated, null, transaction, claim, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"{team.TeamName} submitted a claim for {transaction.PlayerName}.");
    }

    public WaiverResult ProcessWaivers(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var due = scenario.WaiverWire.OpenTransactions
            .Where(transaction => transaction.ClaimDeadline is null || transaction.ClaimDeadline <= ToDateTimeOffset(scenario.CurrentDate.AddDays(1)))
            .OrderBy(transaction => transaction.ClaimDeadline)
            .ToArray();
        if (due.Length == 0)
        {
            return Result(true, scenario, null, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "No waiver decisions are due.");
        }

        var current = scenario;
        var news = new List<LeagueTransaction>();
        WaiverTransaction? last = null;
        WaiverClaim? lastClaim = null;
        foreach (var transaction in due)
        {
            var claim = current.WaiverWire.Claims
                .Where(item => item.TransactionId == transaction.TransactionId)
                .OrderBy(item => item.PriorityRank)
                .ThenBy(item => item.ClaimDate)
                .FirstOrDefault();
            if (claim is null)
            {
                var assigned = AssignClearedPlayer(registry, current, transaction);
                current = assigned.ScenarioSnapshot;
                news.AddRange(assigned.LeagueTransactions);
                last = assigned.Transaction ?? transaction;
            }
            else
            {
                var claimed = ClaimPlayer(registry, current, transaction, claim);
                current = claimed.ScenarioSnapshot;
                news.AddRange(claimed.LeagueTransactions);
                last = claimed.Transaction ?? transaction;
                lastClaim = claim;
            }
        }

        return Result(true, current, null, last, lastClaim, Array.Empty<AlphaInboxItem>(), news, $"{due.Length} waiver transaction(s) processed.");
    }

    public WaiverResult AssignToAffiliate(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, string reason = "Assigned to affiliate.")
    {
        ArgumentNullException.ThrowIfNull(registry);
        var eligibility = EvaluateEligibility(scenario, personId, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        if (!eligibility.CanAssignToAffiliate)
        {
            return Result(false, scenario, eligibility, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Player cannot be assigned: no affiliate is configured.");
        }

        if (eligibility.RequiresWaivers)
        {
            return PlaceOnWaivers(registry, scenario, personId, reason);
        }

        var transaction = new WaiverTransaction($"waiver-assign:{Guid.NewGuid():N}", WaiverTransactionType.Assignment, WaiverStatus.Assigned, scenario.CurrentDate, null, personId, eligibility.PlayerName, eligibility.Position, eligibility.Age, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.Organization.AffiliateOrganizationId, AffiliateName(scenario), reason, false);
        var updated = ApplyAssignment(scenario, personId, eligibility.PlayerName, transaction, WaiverStatus.Assigned, reason);
        QueueEvent(registry, updated, personId, LegacyEventType.PlayerAssignedToAffiliate, "Player assigned to affiliate", $"{eligibility.PlayerName} was assigned to {AffiliateName(scenario)}.");
        var news = Transaction(updated, personId, eligibility.PlayerName, LeagueTransactionType.PlayerAssigned, $"{scenario.Organization.Name} assigned {eligibility.PlayerName} to {AffiliateName(scenario)}. {reason}");
        return Result(true, updated, eligibility, transaction, null, Array.Empty<AlphaInboxItem>(), new[] { news }, $"{eligibility.PlayerName} assigned to affiliate.");
    }

    public WaiverResult RecallFromAffiliate(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, string reason = "Recalled from affiliate.")
    {
        ArgumentNullException.ThrowIfNull(registry);
        var eligibility = EvaluateEligibility(scenario, personId, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        if (!eligibility.CanRecallFromAffiliate)
        {
            return Result(false, scenario, eligibility, null, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Player is not currently assigned to an affiliate.");
        }

        var transaction = new WaiverTransaction($"waiver-recall:{Guid.NewGuid():N}", WaiverTransactionType.Recall, WaiverStatus.Recalled, scenario.CurrentDate, null, personId, eligibility.PlayerName, eligibility.Position, eligibility.Age, scenario.Organization.AffiliateOrganizationId ?? scenario.Organization.OrganizationId, AffiliateName(scenario), scenario.Organization.OrganizationId, scenario.Organization.Name, reason, false);
        var roster = scenario.AlphaSnapshot.Roster with
        {
            Players = scenario.AlphaSnapshot.Roster.Players
                .Select(player => player.PersonId == personId ? player with { Status = RosterStatus.Active } : player)
                .ToArray()
        };
        var pipeline = scenario.PlayerPipeline
            .Select(record => record.PersonId == personId ? record with
            {
                CurrentOrganizationId = scenario.Organization.OrganizationId,
                CurrentTeamName = scenario.Organization.Name,
                CurrentLevel = scenario.LeagueProfile.Experience == LeagueExperience.Nhl ? "NHL" : "Professional",
                PipelineStatus = PlayerPipelineStatus.CalledUp,
                AssignmentStatus = PlayerAssignmentStatus.NhlRoster,
                AssignmentHistory = record.AssignmentHistory.Append($"{scenario.CurrentDate:yyyy-MM-dd}: Recalled from affiliate. {reason}").ToArray()
            } : record)
            .ToArray();
        var updated = AddHistory(scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { Roster = roster }, PlayerPipeline = pipeline }, personId, eligibility.PlayerName, WaiverStatus.Recalled, scenario.Organization.OrganizationId, scenario.Organization.Name, $"{eligibility.PlayerName} was recalled from {AffiliateName(scenario)}.");
        QueueEvent(registry, updated, personId, LegacyEventType.PlayerRecalledFromAffiliate, "Player recalled from affiliate", $"{eligibility.PlayerName} was recalled from affiliate.");
        var news = Transaction(updated, personId, eligibility.PlayerName, LeagueTransactionType.PlayerRecalled, $"{scenario.Organization.Name} recalled {eligibility.PlayerName} from {AffiliateName(scenario)}.");
        return Result(true, updated, eligibility, transaction, null, Array.Empty<AlphaInboxItem>(), new[] { news }, $"{eligibility.PlayerName} recalled from affiliate.");
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId, Rulebook? rulebook = null)
    {
        var eligibility = EvaluateEligibility(scenario, personId, rulebook);
        var history = scenario.WaiverHistory.ForPlayer(personId);
        var lines = new List<string>
        {
            $"Waiver status: {eligibility.Status}",
            $"Waiver exempt: {(eligibility.IsWaiverExempt ? "Yes" : "No")}",
            $"Requires waivers: {(eligibility.RequiresWaivers ? "Yes" : "No")}",
            $"Can assign: {(eligibility.CanAssignToAffiliate ? "Yes" : "No")}",
            $"Can recall: {(eligibility.CanRecallFromAffiliate ? "Yes" : "No")}",
            $"Reason: {eligibility.Reason}"
        };
        lines.AddRange(history.Take(6).Select(entry => $"History: {entry.Date:yyyy-MM-dd} - {entry.Summary}"));
        if (history.Count == 0)
        {
            lines.Add("History: no waiver or assignment history yet.");
        }

        return lines;
    }

    private WaiverResult AssignClearedPlayer(EngineRegistry registry, NewGmScenarioSnapshot scenario, WaiverTransaction transaction)
    {
        var cleared = transaction with { Status = WaiverStatus.Cleared, TransactionType = WaiverTransactionType.Clear, IsOpen = false };
        var assigned = ApplyAssignment(ReplaceTransaction(scenario, cleared), transaction.PersonId, transaction.PlayerName, cleared, WaiverStatus.Cleared, "Cleared waivers and assigned to affiliate.");
        QueueEvent(registry, assigned, transaction.PersonId, LegacyEventType.PlayerClearedWaivers, "Player cleared waivers", $"{transaction.PlayerName} cleared waivers.");
        var news = Transaction(assigned, transaction.PersonId, transaction.PlayerName, LeagueTransactionType.WaiverCleared, $"{transaction.PlayerName} cleared waivers and was assigned to {AffiliateName(scenario)}.");
        return Result(true, assigned, null, cleared, null, Array.Empty<AlphaInboxItem>(), new[] { news }, $"{transaction.PlayerName} cleared waivers.");
    }

    private WaiverResult ClaimPlayer(EngineRegistry registry, NewGmScenarioSnapshot scenario, WaiverTransaction transaction, WaiverClaim claim)
    {
        var claimed = transaction with
        {
            Status = WaiverStatus.Claimed,
            TransactionType = WaiverTransactionType.Claim,
            DestinationOrganizationId = claim.ClaimingOrganizationId,
            DestinationTeamName = claim.ClaimingTeamName,
            IsOpen = false
        };
        var roster = scenario.AlphaSnapshot.Roster with
        {
            Players = scenario.AlphaSnapshot.Roster.Players
                .Select(player => player.PersonId == transaction.PersonId ? player with { Status = RosterStatus.Released, ReleasedDate = scenario.CurrentDate } : player)
                .ToArray()
        };
        var pipeline = scenario.PlayerPipeline.Select(record => record.PersonId == transaction.PersonId ? record with
        {
            CurrentOrganizationId = claim.ClaimingOrganizationId,
            CurrentTeamName = claim.ClaimingTeamName,
            CurrentLevel = "Claimed NHL roster",
            PipelineStatus = PlayerPipelineStatus.NhlRoster,
            AssignmentStatus = PlayerAssignmentStatus.NhlRoster,
            AssignmentHistory = record.AssignmentHistory.Append($"{scenario.CurrentDate:yyyy-MM-dd}: Claimed off waivers by {claim.ClaimingTeamName}.").ToArray()
        } : record).ToArray();
        var updated = AddHistory(ReplaceTransaction(scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { Roster = roster }, PlayerPipeline = pipeline }, claimed), transaction.PersonId, transaction.PlayerName, WaiverStatus.Claimed, claim.ClaimingOrganizationId, claim.ClaimingTeamName, $"{transaction.PlayerName} was claimed off waivers by {claim.ClaimingTeamName}.");
        QueueEvent(registry, updated, transaction.PersonId, LegacyEventType.PlayerClaimedOnWaivers, "Player claimed on waivers", $"{transaction.PlayerName} was claimed by {claim.ClaimingTeamName}.");
        var news = Transaction(updated, transaction.PersonId, transaction.PlayerName, LeagueTransactionType.WaiverClaimed, $"{claim.ClaimingTeamName} claimed {transaction.PlayerName} off waivers from {transaction.OriginTeamName}.");
        return Result(true, updated, null, claimed, claim, Array.Empty<AlphaInboxItem>(), new[] { news }, $"{transaction.PlayerName} claimed by {claim.ClaimingTeamName}.");
    }

    private NewGmScenarioSnapshot ApplyAssignment(NewGmScenarioSnapshot scenario, string personId, string playerName, WaiverTransaction transaction, WaiverStatus historyStatus, string reason)
    {
        var roster = scenario.AlphaSnapshot.Roster with
        {
            Players = scenario.AlphaSnapshot.Roster.Players
                .Select(player => player.PersonId == personId ? player with { Status = RosterStatus.AssignedToAffiliate } : player)
                .ToArray()
        };
        var affiliateId = scenario.Organization.AffiliateOrganizationId ?? transaction.DestinationOrganizationId ?? scenario.Organization.OrganizationId;
        var affiliateName = AffiliateName(scenario);
        var pipeline = scenario.PlayerPipeline.Select(record => record.PersonId == personId ? record with
        {
            CurrentOrganizationId = affiliateId,
            CurrentTeamName = affiliateName,
            CurrentLevel = "AHL Affiliate",
            PipelineStatus = PlayerPipelineStatus.AssignedToAhl,
            AssignmentStatus = PlayerAssignmentStatus.AssignedToAhl,
            AssignmentHistory = record.AssignmentHistory.Append($"{scenario.CurrentDate:yyyy-MM-dd}: Assigned to {affiliateName}. {reason}").ToArray()
        } : record).ToArray();
        var next = ReplaceTransaction(scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { Roster = roster }, PlayerPipeline = pipeline }, transaction);
        var activeAssignment = next.CurrentLineup?.Assignments
            .FirstOrDefault(assignment => assignment.PersonId == personId && assignment.Slot != LineupSlot.HealthyScratch);
        if (activeAssignment is not null)
        {
            var lineupResult = new LineupService().RemovePlayerFromSlot(next, activeAssignment.Slot);
            if (lineupResult.Success)
            {
                next = lineupResult.ScenarioSnapshot;
            }
        }

        return AddHistory(next, personId, playerName, historyStatus, affiliateId, affiliateName, $"{playerName} assigned to {affiliateName}. {reason}");
    }

    private static NewGmScenarioSnapshot ReplaceTransaction(NewGmScenarioSnapshot scenario, WaiverTransaction transaction)
    {
        var wire = scenario.WaiverWire with
        {
            Transactions = scenario.WaiverWire.Transactions
                .Where(item => item.TransactionId != transaction.TransactionId)
                .Append(transaction)
                .OrderBy(item => item.Date)
                .ThenBy(item => item.TransactionId, StringComparer.Ordinal)
                .ToArray()
        };
        return scenario with { WaiverWire = wire };
    }

    private NewGmScenarioSnapshot AddHistory(NewGmScenarioSnapshot scenario, string personId, string playerName, WaiverStatus status, string organizationId, string teamName, string summary)
    {
        var entry = new WaiverHistoryEntry($"waiver-history:{Guid.NewGuid():N}", scenario.CurrentDate, personId, playerName, status, organizationId, teamName, summary);
        var transaction = new TransactionHistoryRecord($"transaction:waiver:{Guid.NewGuid():N}", scenario.CurrentDate, scenario.Season.Year, status.ToString(), personId, playerName, organizationId, teamName, summary);
        var timelines = scenario.PlayerCareerTimelines.Any(timeline => timeline.PersonId == personId)
            ? scenario.PlayerCareerTimelines.Select(timeline => timeline.PersonId == personId ? timeline with { Entries = timeline.Entries.Append($"{scenario.CurrentDate:yyyy-MM-dd}: {summary}").ToArray() } : timeline).ToArray()
            : scenario.PlayerCareerTimelines.Append(new PlayerCareerTimeline(personId, playerName, new[] { $"{scenario.CurrentDate:yyyy-MM-dd}: {summary}" })).ToArray();
        return scenario with
        {
            WaiverHistory = scenario.WaiverHistory.Add(entry),
            TransactionHistory = scenario.TransactionHistory.Append(transaction).ToArray(),
            PlayerCareerTimelines = timelines
        };
    }

    private IReadOnlyList<WaiverPriority> BuildPriority(NewGmScenarioSnapshot scenario, Rulebook rulebook)
    {
        var ordered = string.Equals(rulebook.WaiverRules?.WaiverOrder, "reverse_standings", StringComparison.OrdinalIgnoreCase)
            ? scenario.LeagueProfile.Teams.OrderBy(team => PreviousWins(team.PreviousRecord)).ThenBy(team => team.TeamName, StringComparer.Ordinal).ToArray()
            : scenario.LeagueProfile.Teams.OrderBy(team => team.TeamName, StringComparer.Ordinal).ToArray();
        return ordered.Select((team, index) => new WaiverPriority(team.OrganizationId, team.TeamName, index + 1)).ToArray();
    }

    private static int PreviousWins(string record)
    {
        var first = record.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(first, out var wins) ? wins : 0;
    }

    private static RosterPosition pipelinePosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? RosterPosition.Unknown;

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId)?.PlayerName
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProspectName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? personId;

    private static string AffiliateName(NewGmScenarioSnapshot scenario) =>
        scenario.AffiliateLinks.FirstOrDefault(link => link.ParentOrganizationId == scenario.Organization.OrganizationId)?.AffiliateTeamName
        ?? scenario.LeagueProfile.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.AffiliateOrganizationId)?.TeamName
        ?? "affiliate";

    private static bool IsProfessional(Rulebook rulebook) =>
        rulebook.LeagueType.Contains("nhl", StringComparison.OrdinalIgnoreCase)
        || rulebook.LeagueType.Contains("ahl", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero);

    private static WaiverEligibility Valid(WaiverEligibility eligibility)
    {
        eligibility.Validate();
        return eligibility;
    }

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, LegacyEventType eventType, string title, string description)
    {
        registry.EventEngine.QueueEvent(new LegacyEvent(
            $"event-waiver:{Guid.NewGuid():N}",
            ToDateTimeOffset(scenario.CurrentDate),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            LegacyEventStatus.Queued,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: scenario.Organization.OrganizationId),
            new Dictionary<string, object?>
            {
                ["team_name"] = scenario.Organization.Name,
                ["player_name"] = PersonName(scenario, personId),
                ["reason"] = description
            }));
    }

    private static LeagueTransaction Transaction(NewGmScenarioSnapshot scenario, string personId, string playerName, LeagueTransactionType type, string description)
    {
        var transaction = new LeagueTransaction($"transaction:waiver:{Guid.NewGuid():N}", ToDateTimeOffset(scenario.CurrentDate), scenario.Organization.OrganizationId, scenario.Organization.Name, personId, playerName, type, LeagueTransactionWireService.CategoryFor(type), description);
        transaction.Validate();
        return transaction;
    }

    private static WaiverResult Result(bool success, NewGmScenarioSnapshot scenario, WaiverEligibility? eligibility, WaiverTransaction? transaction, WaiverClaim? claim, IReadOnlyList<AlphaInboxItem> inbox, IReadOnlyList<LeagueTransaction> news, string message)
    {
        var result = new WaiverResult(success, scenario, eligibility, transaction, claim, inbox, news, message);
        result.Validate();
        return result;
    }
}
