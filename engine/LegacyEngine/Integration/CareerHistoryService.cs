using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class CareerHistoryService
{
    public CareerHistorySeed CreateInitialHistory(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var timeline = CareerTimeline.Empty
            .Add(new CareerTimelineEntry(
                $"career:gm-hired:{scenario.GeneralManagerProfile.Person.PersonId}:{scenario.CurrentDate:yyyyMMdd}",
                CareerTimelineEntryType.GMHired,
                scenario.CurrentDate,
                scenario.Season.Year,
                scenario.GeneralManagerProfile.Person.PersonId,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                "GM hired",
                $"{scenario.GeneralManagerProfile.Person.Identity.DisplayName} was hired as GM of {scenario.Organization.Name}.",
                null,
                HistoryImportance.Major));

        foreach (var timelineSeed in scenario.PlayerCareerTimelines)
        {
            var date = scenario.CurrentDate.AddDays(-Math.Min(120, Math.Abs(timelineSeed.PersonId.GetHashCode()) % 90));
            timeline = timeline.Add(new CareerTimelineEntry(
                $"career:seed:{timelineSeed.PersonId}",
                CareerTimelineEntryType.SeasonStarted,
                date,
                scenario.Season.Year - 1,
                timelineSeed.PersonId,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                "Pre-existing career history",
                string.Join(" ", timelineSeed.Entries.Take(2)),
                null,
                HistoryImportance.Normal));
        }

        var staffHistory = scenario.StaffMembers.Select(staff => BuildStaffHistory(scenario, staff)).ToArray();
        foreach (var staff in staffHistory)
        {
            timeline = timeline.Add(new CareerTimelineEntry(
                $"career:staff-current:{staff.PersonId}",
                CareerTimelineEntryType.StaffHired,
                scenario.CurrentDate,
                scenario.Season.Year,
                staff.PersonId,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                "Staff role active",
                $"{staff.StaffName} is serving as {StaffRoles.Title(staff.CurrentRole)} for {scenario.Organization.Name}.",
                null,
                HistoryImportance.Normal));
        }

        var organizationSeason = BuildOrganizationSeasonHistory(scenario);
        var gmHistory = BuildGmHistory(scenario, draftPicksMade: 0, tradesMade: 0, freeAgentsSigned: 0, staffHired: 0);
        var seed = new CareerHistorySeed(
            timeline,
            Array.Empty<DraftPickHistory>(),
            Array.Empty<DraftClassHistory>(),
            staffHistory,
            gmHistory,
            new[] { organizationSeason },
            Array.Empty<TransactionHistoryRecord>());
        seed.Validate();
        return seed;
    }

    public NewGmScenarioSnapshot RecordDraftCompleted(NewGmScenarioSnapshot scenario, IReadOnlyList<DraftPickSummary> selections)
    {
        var playerSelections = selections.Where(selection => selection.IsPlayerSelection).ToArray();
        if (playerSelections.Length == 0)
        {
            return scenario;
        }

        var picks = playerSelections.Select(selection => BuildDraftPickHistory(scenario, selection)).ToArray();
        var draftClass = new DraftClassHistory(
            scenario.Season.Year,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            picks,
            $"{scenario.Organization.Name} made {picks.Length} tracked pick(s) in the {scenario.Season.Year} draft.");
        var timeline = scenario.CareerTimeline;
        foreach (var pick in picks)
        {
            timeline = timeline.Add(new CareerTimelineEntry(
                $"career:drafted:{pick.PlayerPersonId}:{pick.Year}:{pick.Round}:{pick.OverallPick}",
                CareerTimelineEntryType.Drafted,
                scenario.CurrentDate,
                scenario.Season.Year,
                pick.PlayerPersonId,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                $"Drafted {pick.PlayerName}",
                $"{scenario.Organization.Name} drafted {pick.PlayerName} in round {pick.Round}, pick {pick.OverallPick}. Projection at draft: {pick.ScoutingProjectionAtDraft}",
                null,
                HistoryImportance.Important));
        }

        var updated = scenario with
        {
            CareerTimeline = timeline,
            DraftPickHistory = MergeDraftPicks(scenario.DraftPickHistory, picks),
            DraftClassHistory = scenario.DraftClassHistory.Where(item => item.Year != draftClass.Year).Append(draftClass).ToArray(),
            GmCareerHistory = scenario.GmCareerHistory is null
                ? BuildGmHistory(scenario, picks.Length, 0, 0, 0)
                : scenario.GmCareerHistory with { DraftPicksMade = scenario.GmCareerHistory.DraftPicksMade + picks.Length }
        };
        updated.Validate();
        return updated;
    }

    public NewGmScenarioSnapshot RecordTradeCompleted(NewGmScenarioSnapshot scenario, TradeOffer offer)
    {
        var timeline = scenario.CareerTimeline;
        foreach (var asset in offer.PlayerGives.Concat(offer.PlayerReceives).Where(asset => asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights))
        {
            timeline = timeline.Add(new CareerTimelineEntry(
                $"career:trade:{offer.TradeOfferId}:{asset.AssetId}:{asset.Side}",
                CareerTimelineEntryType.Traded,
                scenario.CurrentDate,
                scenario.Season.Year,
                asset.AssetId,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                $"Trade involving {asset.DisplayName}",
                $"{asset.DisplayName} was part of a completed trade with {offer.OtherOrganizationName}.",
                null,
                HistoryImportance.Important));
        }

        var transaction = new TransactionHistoryRecord(
            $"transaction-history:trade:{offer.TradeOfferId}",
            scenario.CurrentDate,
            scenario.Season.Year,
            "TradeCompleted",
            offer.PlayerReceives.FirstOrDefault()?.AssetId ?? offer.PlayerGives.FirstOrDefault()?.AssetId,
            offer.PlayerReceives.FirstOrDefault()?.DisplayName ?? offer.PlayerGives.FirstOrDefault()?.DisplayName ?? "Trade",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            $"{scenario.Organization.Name} completed a trade with {offer.OtherOrganizationName}: {string.Join(", ", offer.PlayerGives.Select(asset => asset.DisplayName))} for {string.Join(", ", offer.PlayerReceives.Select(asset => asset.DisplayName))}.");
        var updated = scenario with
        {
            CareerTimeline = timeline,
            TransactionHistory = UpsertTransaction(scenario.TransactionHistory, transaction),
            GmCareerHistory = scenario.GmCareerHistory is null
                ? BuildGmHistory(scenario, 0, 1, 0, 0)
                : scenario.GmCareerHistory with { TradesMade = scenario.GmCareerHistory.TradesMade + 1 }
        };
        updated.Validate();
        return updated;
    }

    public NewGmScenarioSnapshot RecordContractSigned(NewGmScenarioSnapshot scenario, Contract contract, string personName, PendingGmActionType sourceActionType)
    {
        var entryType = CareerTimelineEntryType.Signed;
        var transactionType = sourceActionType == PendingGmActionType.SignFreeAgent ? "FreeAgentSigned" : "ContractSigned";
        var timeline = scenario.CareerTimeline.Add(new CareerTimelineEntry(
            $"career:signed:{contract.ContractId}",
            entryType,
            scenario.CurrentDate,
            scenario.Season.Year,
            contract.PersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            $"{personName} signed",
            $"{personName} signed a {contract.ContractType} with {scenario.Organization.Name}.",
            null,
            HistoryImportance.Important));
        var transaction = new TransactionHistoryRecord(
            $"transaction-history:contract:{contract.ContractId}",
            scenario.CurrentDate,
            scenario.Season.Year,
            transactionType,
            contract.PersonId,
            personName,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            $"{personName} signed a {contract.ContractType}.");

        var freeAgentSigned = sourceActionType == PendingGmActionType.SignFreeAgent ? 1 : 0;
        var updated = scenario with
        {
            CareerTimeline = timeline,
            TransactionHistory = UpsertTransaction(scenario.TransactionHistory, transaction),
            GmCareerHistory = scenario.GmCareerHistory is null
                ? BuildGmHistory(scenario, 0, 0, freeAgentSigned, 0)
                : scenario.GmCareerHistory with { FreeAgentsSigned = scenario.GmCareerHistory.FreeAgentsSigned + freeAgentSigned }
        };
        updated.Validate();
        return updated;
    }

    public NewGmScenarioSnapshot RecordInjury(NewGmScenarioSnapshot scenario, string personId, string summary, string? eventId = null)
    {
        var name = PersonName(scenario, personId);
        var timeline = scenario.CareerTimeline.Add(new CareerTimelineEntry(
            $"career:injury:{personId}:{scenario.CurrentDate:yyyyMMdd}:{summary.GetHashCode()}",
            CareerTimelineEntryType.Injury,
            scenario.CurrentDate,
            scenario.Season.Year,
            personId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            $"Injury: {name}",
            summary,
            eventId,
            HistoryImportance.Normal));
        return scenario with { CareerTimeline = timeline };
    }

    public NewGmScenarioSnapshot RecordFromEvent(NewGmScenarioSnapshot scenario, LegacyEvent legacyEvent)
    {
        var type = legacyEvent.EventType switch
        {
            LegacyEventType.PlayerDrafted => CareerTimelineEntryType.Drafted,
            LegacyEventType.ContractSigned or LegacyEventType.FreeAgentSigned or LegacyEventType.ProspectSigned => CareerTimelineEntryType.Signed,
            LegacyEventType.TradeCompleted => CareerTimelineEntryType.Traded,
            LegacyEventType.PlayerInjured => CareerTimelineEntryType.Injury,
            LegacyEventType.PlayerBreakout => CareerTimelineEntryType.Breakout,
            LegacyEventType.PlayerRegression => CareerTimelineEntryType.Regression,
            LegacyEventType.StaffHired => CareerTimelineEntryType.StaffHired,
            LegacyEventType.StaffReleased => CareerTimelineEntryType.StaffReleased,
            LegacyEventType.SeasonStarted => CareerTimelineEntryType.SeasonStarted,
            LegacyEventType.SeasonEnded => CareerTimelineEntryType.SeasonEnded,
            _ => (CareerTimelineEntryType?)null
        };
        if (type is null)
        {
            return scenario;
        }

        var timeline = scenario.CareerTimeline.Add(new CareerTimelineEntry(
            $"career:event:{legacyEvent.EventId}",
            type.Value,
            DateOnly.FromDateTime(legacyEvent.OccurredAt.Date),
            scenario.Season.Year,
            legacyEvent.Context.PrimaryPersonId,
            legacyEvent.Context.OrganizationId,
            scenario.Organization.Name,
            legacyEvent.Title,
            legacyEvent.Description,
            legacyEvent.EventId,
            legacyEvent.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical ? HistoryImportance.Important : HistoryImportance.Normal));
        return scenario with { CareerTimeline = timeline };
    }

    public IReadOnlyList<WhereAreTheyNowRecord> BuildWhereAreTheyNow(NewGmScenarioSnapshot scenario) =>
        scenario.DraftPickHistory
            .OrderByDescending(pick => pick.Year)
            .ThenBy(pick => pick.Round)
            .ThenBy(pick => pick.OverallPick)
            .Select(pick => BuildWhereAreTheyNowRecord(scenario, pick))
            .ToArray();

    private static DraftPickHistory BuildDraftPickHistory(NewGmScenarioSnapshot scenario, DraftPickSummary selection)
    {
        var boardEntry = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == selection.ProspectPersonId);
        var prospect = scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == selection.ProspectPersonId);
        var position = boardEntry?.Bio?.Position ?? prospect?.Position ?? RosterPosition.Unknown;
        var team = boardEntry?.Bio is null ? "Unknown draft team" : $"{boardEntry.Bio.CurrentTeam} ({boardEntry.Bio.League})";
        var currentStatus = prospect?.Status.ToString() ?? "DraftRightsHeld";
        var stat = scenario.CareerStatSummaries.FirstOrDefault(item => item.PersonId == selection.ProspectPersonId);
        var outcome = currentStatus == "Signed" ? DraftPickOutcome.Developing : DraftPickOutcome.Unknown;
        return new DraftPickHistory(
            scenario.Season.Year,
            selection.RoundNumber,
            selection.PickNumber,
            selection.ProspectPersonId,
            selection.ProspectName,
            position,
            team,
            boardEntry?.ProjectionText ?? "Projection not recorded.",
            boardEntry?.ScoutingConfidence,
            boardEntry?.PersonalNotes is { Length: > 0 } ? boardEntry.PersonalNotes : "No GM draft note recorded.",
            currentStatus,
            stat?.GamesPlayed ?? 0,
            stat?.Points ?? 0,
            stat?.IsGoalie == true ? $"{stat.Wins}-{stat.Losses}, {stat.Shutouts} SO" : "n/a",
            outcome,
            outcome == DraftPickOutcome.Unknown ? "Too early to evaluate. Outcome remains Unknown/Developing." : "Developing in organization.");
    }

    private static WhereAreTheyNowRecord BuildWhereAreTheyNowRecord(NewGmScenarioSnapshot scenario, DraftPickHistory pick)
    {
        var rights = scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == pick.PlayerPersonId);
        var roster = scenario.AlphaSnapshot.Roster.FindPlayer(pick.PlayerPersonId);
        var stat = scenario.CareerStatSummaries.FirstOrDefault(item => item.PersonId == pick.PlayerPersonId);
        var injury = scenario.AlphaSnapshot.Injuries.FirstOrDefault(item => item.PersonId == pick.PlayerPersonId && item.IsActive);
        var currentTeam = roster is not null
            ? scenario.Organization.Name
            : rights is not null ? $"Rights held - {rights.Status}" : pick.CurrentStatus;
        var role = roster is null ? "Prospect/rights list" : $"{roster.Status} {roster.Position}";
        var latest = stat?.DisplaySummary ?? "No tracked career stats yet.";
        var development = scenario.AlphaSnapshot.DevelopmentProfiles.Any(profile => profile.PersonId == pick.PlayerPersonId)
            ? "Development profile tracked; current trend remains staff-facing."
            : "No current development profile.";
        var staff = pick.Outcome is DraftPickOutcome.Unknown or DraftPickOutcome.Developing
            ? "Staff says it is too early to judge the pick."
            : pick.OutcomeSummary;
        var record = new WhereAreTheyNowRecord(
            pick.PlayerPersonId,
            pick.PlayerName,
            pick.Year,
            pick.Round,
            pick.OverallPick,
            pick.Position,
            currentTeam,
            role,
            latest,
            development,
            injury is null ? "No active injury." : $"{injury.Severity} {injury.BodyPart}, expected return {injury.ExpectedReturnDate:yyyy-MM-dd}",
            staff,
            pick.Outcome);
        record.Validate();
        return record;
    }

    private static StaffCareerHistory BuildStaffHistory(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        var person = scenario.AlphaSnapshot.People.FirstOrDefault(item => item.PersonId == staff.PersonId);
        var name = person?.Identity.DisplayName ?? staff.PersonId;
        var prior = new[]
        {
            $"{scenario.Season.Year - 3}-{scenario.Season.Year - 1}: prior hockey operations role before current GM.",
            $"{scenario.Season.Year}: {StaffRoles.Title(staff.CurrentRole)} with {scenario.Organization.Name}."
        };
        var history = new StaffCareerHistory(
            staff.PersonId,
            name,
            staff.CurrentRole,
            scenario.Organization.Name,
            prior,
            new[] { "Notable scouted/developed players placeholder for future seasons." },
            "GM relationship tracked through Relationship Engine where available.",
            staff.PerformanceHistory.Count == 0 ? "No formal staff evaluation yet." : staff.PerformanceHistory.Last().Summary);
        history.Validate();
        return history;
    }

    private static GmCareerHistory BuildGmHistory(NewGmScenarioSnapshot scenario, int draftPicksMade, int tradesMade, int freeAgentsSigned, int staffHired)
    {
        var standing = scenario.Standings?.Teams.FirstOrDefault(item => item.OrganizationId == scenario.Organization.OrganizationId);
        var record = standing is null ? "0-0-0" : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}, {standing.Points} pts";
        var history = new GmCareerHistory(
            scenario.GeneralManagerProfile.Person.PersonId,
            scenario.GeneralManagerProfile.Person.Identity.DisplayName,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.CurrentDate,
            scenario.ExecutiveReports.Reports.Count(report => report.Kind == ExecutiveReportKind.EndOfSeasonExecutiveReview),
            record,
            "Playoff record placeholder.",
            draftPicksMade,
            tradesMade,
            freeAgentsSigned,
            staffHired,
            new[] { $"Owner trust {scenario.AlphaSnapshot.Owner.Trust}, confidence {scenario.AlphaSnapshot.Owner.Confidence} on hire." },
            new[] { "Career history is in-memory for Alpha and does not require save/load." });
        history.Validate();
        return history;
    }

    private static OrganizationSeasonHistory BuildOrganizationSeasonHistory(NewGmScenarioSnapshot scenario)
    {
        var previous = scenario.OrganizationHistory;
        var history = new OrganizationSeasonHistory(
            previous?.PriorSeasonYear ?? scenario.Season.Year - 1,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            previous?.RecordText ?? "No prior season record available.",
            previous?.PlayoffResult ?? "Playoff result placeholder.",
            scenario.DraftHistory.Count == 0 ? "No current GM draft class yet." : $"{scenario.DraftHistory.Count} prior tracked draft record(s).",
            scenario.CareerStatSummaries.OrderByDescending(item => item.Points).FirstOrDefault()?.PlayerName ?? "Notable players pending.",
            $"{scenario.StaffMembers.Count} staff member(s) tracked.",
            "Owner changes placeholder.",
            previous?.PreviousLeagueChampion ?? "Championship placeholder.",
            previous?.Summary ?? $"{scenario.Organization.Name} history tracking has started.");
        history.Validate();
        return history;
    }

    private static IReadOnlyList<DraftPickHistory> MergeDraftPicks(IReadOnlyList<DraftPickHistory> existing, IReadOnlyList<DraftPickHistory> additions) =>
        existing.Concat(additions)
            .GroupBy(item => $"{item.Year}:{item.PlayerPersonId}", StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderByDescending(item => item.Year)
            .ThenBy(item => item.Round)
            .ThenBy(item => item.OverallPick)
            .ToArray();

    private static IReadOnlyList<TransactionHistoryRecord> UpsertTransaction(IReadOnlyList<TransactionHistoryRecord> existing, TransactionHistoryRecord transaction) =>
        existing.Where(item => item.TransactionHistoryId != transaction.TransactionHistoryId)
            .Append(transaction)
            .OrderByDescending(item => item.Date)
            .ThenBy(item => item.TransactionHistoryId, StringComparer.Ordinal)
            .ToArray();

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(item => item.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(item => item.PersonId == personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.TradeBlock?.Find(personId)?.Name
        ?? scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == personId)?.ProspectName
        ?? personId;
}
