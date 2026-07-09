using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed class LeagueTransactionWireService
{
    public IReadOnlyList<LeagueTransaction> BuildTransactions(
        IEnumerable<LegacyEvent> events,
        Func<string, string>? organizationNameResolver = null,
        Func<string, string>? personNameResolver = null,
        LeagueNewsCategory category = LeagueNewsCategory.All)
    {
        ArgumentNullException.ThrowIfNull(events);

        var transactions = events
            .Select(item => TryCreate(item, organizationNameResolver, personNameResolver))
            .Where(item => item is not null)
            .Cast<LeagueTransaction>();

        if (category != LeagueNewsCategory.All)
        {
            transactions = transactions.Where(item => item.Category == category);
        }

        return transactions
            .OrderByDescending(item => item.Date)
            .ThenBy(item => item.TeamName, StringComparer.Ordinal)
            .ThenBy(item => item.PersonName, StringComparer.Ordinal)
            .ToArray();
    }

    public bool ShouldRouteToLeagueNews(LegacyEvent legacyEvent, string playerOrganizationId)
    {
        ArgumentNullException.ThrowIfNull(legacyEvent);

        return TryMapType(legacyEvent.EventType) is not null
            && !string.IsNullOrWhiteSpace(legacyEvent.Context.OrganizationId)
            && !string.Equals(legacyEvent.Context.OrganizationId, playerOrganizationId, StringComparison.Ordinal);
    }

    public bool ShouldRouteToInbox(LegacyEvent legacyEvent, string playerOrganizationId)
    {
        ArgumentNullException.ThrowIfNull(legacyEvent);

        if (ShouldRouteToLeagueNews(legacyEvent, playerOrganizationId))
        {
            return false;
        }

        if (IsVague(legacyEvent.Title) || IsVague(legacyEvent.Description))
        {
            return false;
        }

        if (legacyEvent.EventType == LegacyEventType.PlayerAddedToRoster
            && string.Equals(legacyEvent.Context.OrganizationId, playerOrganizationId, StringComparison.Ordinal)
            && legacyEvent.Severity == LegacyEventSeverity.Notice
            && !ContainsMeaningfulReason(legacyEvent))
        {
            return false;
        }

        return true;
    }

    public LeagueTransaction? TryCreate(
        LegacyEvent legacyEvent,
        Func<string, string>? organizationNameResolver = null,
        Func<string, string>? personNameResolver = null)
    {
        ArgumentNullException.ThrowIfNull(legacyEvent);

        var type = TryMapType(legacyEvent.EventType);
        if (type is null)
        {
            return null;
        }

        var organizationId = legacyEvent.Context.OrganizationId;
        var personId = legacyEvent.Context.PrimaryPersonId;
        var teamName = MetadataString(legacyEvent, "team_name")
            ?? MetadataString(legacyEvent, "organization_name")
            ?? (organizationId is not null && organizationNameResolver is not null ? organizationNameResolver(organizationId) : null)
            ?? organizationId
            ?? "League";
        var personName = MetadataString(legacyEvent, "player_name")
            ?? MetadataString(legacyEvent, "staff_name")
            ?? MetadataString(legacyEvent, "person_name")
            ?? (personId is not null && personNameResolver is not null ? personNameResolver(personId) : null)
            ?? personId
            ?? "Personnel";
        var transaction = new LeagueTransaction(
            TransactionId: $"transaction:{legacyEvent.EventId}",
            Date: legacyEvent.OccurredAt,
            OrganizationId: organizationId,
            TeamName: teamName,
            PersonId: personId,
            PersonName: personName,
            TransactionType: type.Value,
            Category: CategoryFor(type.Value),
            Description: BuildDescription(legacyEvent, teamName, personName, type.Value));
        transaction.Validate();
        return transaction;
    }

    public static LeagueNewsCategory CategoryFor(LeagueTransactionType type) =>
        type switch
        {
            LeagueTransactionType.PlayerSigned
                or LeagueTransactionType.ContractOffered
                or LeagueTransactionType.ContractSigned
                or LeagueTransactionType.RfaQualified
                or LeagueTransactionType.RfaNotQualified
                or LeagueTransactionType.PlayerBecameUfa
                or LeagueTransactionType.RightsReleased
                or LeagueTransactionType.ArbitrationFiled
                or LeagueTransactionType.ArbitrationAwardIssued
                or LeagueTransactionType.ArbitrationSettled
                or LeagueTransactionType.ArbitrationWalkAway
                or LeagueTransactionType.ContractBoughtOut
                or LeagueTransactionType.OfferSheetSubmitted
                or LeagueTransactionType.OfferSheetAccepted
                or LeagueTransactionType.OfferSheetMatched
                or LeagueTransactionType.OfferSheetDeclined
                or LeagueTransactionType.OfferSheetCompleted => LeagueNewsCategory.Signings,
            LeagueTransactionType.PlayerAddedToRoster
                or LeagueTransactionType.PlayerReleased
                or LeagueTransactionType.PlayerAssigned
                or LeagueTransactionType.PlayerRecalled
                or LeagueTransactionType.WaiverPlaced
                or LeagueTransactionType.WaiverClaimed
                or LeagueTransactionType.WaiverCleared => LeagueNewsCategory.RosterMoves,
            LeagueTransactionType.Injury => LeagueNewsCategory.Injuries,
            LeagueTransactionType.DraftPick => LeagueNewsCategory.Draft,
            LeagueTransactionType.StaffHired or LeagueTransactionType.StaffReleased => LeagueNewsCategory.Staff,
            LeagueTransactionType.TradeCompleted => LeagueNewsCategory.RosterMoves,
            LeagueTransactionType.TradeDeadline => LeagueNewsCategory.Deadline,
            LeagueTransactionType.SeasonCompleted => LeagueNewsCategory.League,
            LeagueTransactionType.TeamIdentityUpdate => LeagueNewsCategory.League,
            LeagueTransactionType.PlayerMilestone => LeagueNewsCategory.League,
            LeagueTransactionType.StaffMilestone => LeagueNewsCategory.Staff,
            LeagueTransactionType.OwnerMilestone => LeagueNewsCategory.League,
            _ => LeagueNewsCategory.All
        };

    private static LeagueTransactionType? TryMapType(LegacyEventType eventType) =>
        eventType switch
        {
            LegacyEventType.ContractOffered => LeagueTransactionType.ContractOffered,
            LegacyEventType.ContractSigned => LeagueTransactionType.ContractSigned,
            LegacyEventType.ContractOfferSubmitted => LeagueTransactionType.ContractOffered,
            LegacyEventType.ContractApprovedByGM => LeagueTransactionType.ContractSigned,
            LegacyEventType.PlayerQualifiedAsRfa => LeagueTransactionType.RfaQualified,
            LegacyEventType.PlayerNotQualified => LeagueTransactionType.RfaNotQualified,
            LegacyEventType.PlayerBecameUfa => LeagueTransactionType.PlayerBecameUfa,
            LegacyEventType.PlayerRightsReleased => LeagueTransactionType.RightsReleased,
            LegacyEventType.ArbitrationFiled or LegacyEventType.ArbitrationHearingScheduled => LeagueTransactionType.ArbitrationFiled,
            LegacyEventType.ArbitrationAwardIssued => LeagueTransactionType.ArbitrationAwardIssued,
            LegacyEventType.ArbitrationSettled => LeagueTransactionType.ArbitrationSettled,
            LegacyEventType.ArbitrationWalkAway => LeagueTransactionType.ArbitrationWalkAway,
            LegacyEventType.ContractBoughtOut => LeagueTransactionType.ContractBoughtOut,
            LegacyEventType.OfferSheetSubmitted => LeagueTransactionType.OfferSheetSubmitted,
            LegacyEventType.OfferSheetAccepted => LeagueTransactionType.OfferSheetAccepted,
            LegacyEventType.OfferSheetMatched => LeagueTransactionType.OfferSheetMatched,
            LegacyEventType.OfferSheetDeclined => LeagueTransactionType.OfferSheetDeclined,
            LegacyEventType.OfferSheetCompleted => LeagueTransactionType.OfferSheetCompleted,
            LegacyEventType.FreeAgentSigned => LeagueTransactionType.PlayerSigned,
            LegacyEventType.FreeAgentSignedElsewhere => LeagueTransactionType.PlayerSigned,
            LegacyEventType.ProspectSigned => LeagueTransactionType.PlayerSigned,
            LegacyEventType.PlayerAddedToRoster => LeagueTransactionType.PlayerAddedToRoster,
            LegacyEventType.PlayerRemovedFromRoster or LegacyEventType.PlayerReleased => LeagueTransactionType.PlayerReleased,
            LegacyEventType.PlayerMovedToInjuredReserve
                or LegacyEventType.TrainingCampPlayerAssigned
                or LegacyEventType.ProspectAssignedToAffiliate
                or LegacyEventType.PlayerAssignedToAffiliate => LeagueTransactionType.PlayerAssigned,
            LegacyEventType.PlayerRecalledFromAffiliate => LeagueTransactionType.PlayerRecalled,
            LegacyEventType.PlayerPlacedOnWaivers => LeagueTransactionType.WaiverPlaced,
            LegacyEventType.PlayerClaimedOnWaivers => LeagueTransactionType.WaiverClaimed,
            LegacyEventType.PlayerClearedWaivers => LeagueTransactionType.WaiverCleared,
            LegacyEventType.PlayerInjured or LegacyEventType.InjuryReAggravated or LegacyEventType.InjuryCareerThreatening => LeagueTransactionType.Injury,
            LegacyEventType.PlayerDrafted => LeagueTransactionType.DraftPick,
            LegacyEventType.StaffHired => LeagueTransactionType.StaffHired,
            LegacyEventType.StaffReleased => LeagueTransactionType.StaffReleased,
            LegacyEventType.TradeCompleted => LeagueTransactionType.TradeCompleted,
            LegacyEventType.TradeDeadlineClosed
                or LegacyEventType.DeadlineRumorCreated
                or LegacyEventType.DeadlineTradeBlockExpanded
                or LegacyEventType.TradeDeadlineApproaching
                or LegacyEventType.TradeDeadlineWeekStarted
                or LegacyEventType.TradeDeadlineDayStarted => LeagueTransactionType.TradeDeadline,
            LegacyEventType.SeasonEnded => LeagueTransactionType.SeasonCompleted,
            _ => null
        };

    private static string BuildDescription(LegacyEvent legacyEvent, string teamName, string personName, LeagueTransactionType type)
    {
        var reason = MetadataString(legacyEvent, "reason");
        var baseDescription = type switch
        {
            LeagueTransactionType.ContractSigned => $"{personName} signed a contract with {teamName}.",
            LeagueTransactionType.ContractOffered => $"{teamName} offered a contract to {personName}.",
            LeagueTransactionType.PlayerSigned => $"{teamName} signed {personName}.",
            LeagueTransactionType.PlayerAddedToRoster => $"{teamName} added {personName} to the roster.",
            LeagueTransactionType.PlayerReleased => $"{teamName} released {personName}.",
            LeagueTransactionType.PlayerAssigned => $"{teamName} assigned {personName}.",
            LeagueTransactionType.PlayerRecalled => $"{teamName} recalled {personName}.",
            LeagueTransactionType.WaiverPlaced => $"{teamName} placed {personName} on waivers.",
            LeagueTransactionType.WaiverClaimed => $"{teamName} waiver claim involving {personName}.",
            LeagueTransactionType.WaiverCleared => $"{personName} cleared waivers for {teamName}.",
            LeagueTransactionType.RfaQualified => $"{teamName} issued a qualifying offer to {personName}.",
            LeagueTransactionType.RfaNotQualified => $"{teamName} did not qualify {personName}; rights were released.",
            LeagueTransactionType.PlayerBecameUfa => $"{personName} became an unrestricted free agent.",
            LeagueTransactionType.RightsReleased => $"{teamName} released contract rights to {personName}.",
            LeagueTransactionType.ArbitrationFiled => $"{teamName} filed or received arbitration involving {personName}.",
            LeagueTransactionType.ArbitrationAwardIssued => $"{teamName} received an arbitration award for {personName}.",
            LeagueTransactionType.ArbitrationSettled => $"{teamName} settled arbitration with {personName}.",
            LeagueTransactionType.ArbitrationWalkAway => $"{teamName} walked away from arbitration with {personName}.",
            LeagueTransactionType.ContractBoughtOut => $"{teamName} bought out {personName}'s contract.",
            LeagueTransactionType.OfferSheetSubmitted => $"{teamName} received an offer-sheet filing involving {personName}.",
            LeagueTransactionType.OfferSheetAccepted => $"{personName} accepted an offer sheet; {teamName} must match or take compensation.",
            LeagueTransactionType.OfferSheetMatched => $"{teamName} matched the offer sheet for {personName}.",
            LeagueTransactionType.OfferSheetDeclined => $"{teamName} declined the offer sheet for {personName}.",
            LeagueTransactionType.OfferSheetCompleted => $"{personName}'s offer sheet was completed.",
            LeagueTransactionType.Injury => $"{teamName} reported an injury update for {personName}.",
            LeagueTransactionType.DraftPick => $"{teamName} drafted {personName}.",
            LeagueTransactionType.StaffHired => $"{teamName} hired {personName}.",
            LeagueTransactionType.StaffReleased => $"{teamName} released {personName}.",
            LeagueTransactionType.TradeCompleted => $"{teamName} completed a trade involving {personName}.",
            LeagueTransactionType.TradeDeadline => $"{teamName} trade deadline update: {personName}.",
            LeagueTransactionType.SeasonCompleted => $"{teamName} completed the season. Standings leader: {personName}.",
            LeagueTransactionType.TeamIdentityUpdate => $"{teamName} direction update: {personName}.",
            LeagueTransactionType.StaffMilestone => $"{teamName} staff milestone for {personName}.",
            LeagueTransactionType.OwnerMilestone => $"{teamName} ownership milestone for {personName}.",
            _ => $"{teamName} updated {personName}."
        };

        if (!string.IsNullOrWhiteSpace(reason))
        {
            return $"{baseDescription} Reason: {reason}";
        }

        return IsVague(legacyEvent.Description) ? baseDescription : $"{baseDescription} {legacyEvent.Description}";
    }

    private static bool ContainsMeaningfulReason(LegacyEvent legacyEvent) =>
        legacyEvent.Metadata.ContainsKey("reason")
        || legacyEvent.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical
        || legacyEvent.EventType is LegacyEventType.PendingGmActionCreated;

    private static bool IsVague(string text)
    {
        var normalized = text.Trim().TrimEnd('.').ToLowerInvariant();
        return normalized is "player added to roster"
            or "contract signed"
            or "contract offered"
            or "a contract was signed"
            or "a contract was offered";
    }

    private static string? MetadataString(LegacyEvent legacyEvent, string key) =>
        legacyEvent.Metadata.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
}
