using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Names;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class TradeService
{
    private readonly TradeStrategyService _strategy = new();

    public TradeBlock GenerateTradeBlock(NewGmScenarioSnapshot scenario, IReadOnlyList<Person> tradeBlockPeople)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(tradeBlockPeople);

        var teams = SeasonFrameworkService.LeagueTeams(scenario)
            .Where(team => team.OrganizationId != scenario.Organization.OrganizationId)
            .ToArray();
        var entries = tradeBlockPeople
            .Select((person, index) =>
            {
                var team = teams[index % teams.Length];
                return CreateBlockEntry(scenario, person, team.OrganizationId, team.TeamName, index);
            })
            .ToArray();
        var block = new TradeBlock($"trade-block:{scenario.Season.SeasonId}:{scenario.CurrentDate:yyyyMMdd}", scenario.CurrentDate, entries);
        block.Validate();
        return block;
    }

    public TradeOffer CreateOffer(
        NewGmScenarioSnapshot scenario,
        string otherOrganizationId,
        string otherOrganizationName,
        IReadOnlyList<TradeAsset> playerGives,
        IReadOnlyList<TradeAsset> playerReceives)
    {
        var offer = new TradeOffer(
            $"trade-offer:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            otherOrganizationId,
            otherOrganizationName,
            TradeOfferStatus.Drafted,
            playerGives,
            playerReceives);
        offer.Validate();
        return offer;
    }

    public TradeAsset CreateRosterPlayerAsset(NewGmScenarioSnapshot scenario, string personId, TradeSide side = TradeSide.PlayerOrganization)
    {
        if (side != TradeSide.PlayerOrganization)
        {
            var blockEntry = RequireBlockEntry(scenario, personId);
            return AssetFromBlockEntry(blockEntry, TradeAssetType.Player, side);
        }

        var player = scenario.AlphaSnapshot.Roster.FindPlayer(personId)
            ?? throw new ArgumentException("Roster player is not controlled by the player organization.", nameof(personId));
        var value = PlayerAssetValue(scenario, personId, player.Position, player.Age ?? PersonAge(scenario, personId));
        return new TradeAsset(
            TradeAssetType.Player,
            TradeSide.PlayerOrganization,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            personId,
            PersonName(scenario, personId),
            player.Position,
            player.Age ?? PersonAge(scenario, personId),
            SalaryFor(scenario, personId),
            value,
            $"{player.Position}, {LineupSummaryFor(scenario, personId)}, {ContractStatusText(scenario, personId)}");
    }

    public TradeAsset CreateProspectRightsAsset(NewGmScenarioSnapshot scenario, string prospectPersonId, TradeSide side = TradeSide.PlayerOrganization)
    {
        if (side != TradeSide.PlayerOrganization)
        {
            var blockEntry = RequireBlockEntry(scenario, prospectPersonId);
            return AssetFromBlockEntry(blockEntry, TradeAssetType.ProspectRights, side);
        }

        var prospect = scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == prospectPersonId)
            ?? throw new ArgumentException("Prospect rights are not controlled by the player organization.", nameof(prospectPersonId));
        return new TradeAsset(
            TradeAssetType.ProspectRights,
            TradeSide.PlayerOrganization,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            prospect.ProspectPersonId,
            prospect.ProspectName,
            prospect.Position,
            prospect.Age,
            0m,
            Math.Clamp(46 - prospect.RoundNumber * 2 + (prospect.ScoutingConfidence >= ScoutingConfidenceLevel.High ? 8 : 0), 18, 75),
            $"Rights held, round {prospect.RoundNumber}, pick {prospect.PickNumber}");
    }

    public IReadOnlyList<TradeAsset> BuildPlayerOrganizationAssets(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var assets = new List<TradeAsset>();
        assets.AddRange(scenario.AlphaSnapshot.Roster.ActivePlayers
            .Select(player => CreateRosterPlayerAsset(scenario, player.PersonId)));
        assets.AddRange(scenario.ProspectRights
            .Select(prospect => CreateProspectRightsAsset(scenario, prospect.ProspectPersonId)));
        assets.AddRange(CreateDraftPickAssets(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name));
        assets.Add(CreateFutureConsiderationAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name));
        return assets
            .GroupBy(asset => asset.AssetId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    public IReadOnlyList<TradeAsset> BuildOtherOrganizationAssets(NewGmScenarioSnapshot scenario, string organizationId, string organizationName)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(organizationName))
        {
            throw new ArgumentException("Other organization identity is required.");
        }

        var assets = new List<TradeAsset>();
        var blockEntries = scenario.TradeBlock?.Entries
            .Where(entry => entry.OrganizationId == organizationId || string.Equals(entry.TeamName, organizationName, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.AssetValue)
            .ToArray() ?? Array.Empty<TradeBlockEntry>();
        assets.AddRange(blockEntries
            .Take(6)
            .Select(entry => AssetFromBlockEntry(entry, TradeAssetType.Player, TradeSide.OtherOrganization)));
        assets.AddRange(SyntheticOtherRosterAssets(scenario, organizationId, organizationName, assets.Count, 18 - assets.Count));
        assets.AddRange(blockEntries
            .Skip(6)
            .Take(4)
            .Select(entry => AssetFromBlockEntry(entry, TradeAssetType.ProspectRights, TradeSide.OtherOrganization)));
        assets.AddRange(SyntheticOtherProspectAssets(scenario, organizationId, organizationName, assets.Count(asset => asset.AssetType == TradeAssetType.ProspectRights), 4 - assets.Count(asset => asset.AssetType == TradeAssetType.ProspectRights)));
        assets.AddRange(CreateDraftPickAssets(scenario, TradeSide.OtherOrganization, organizationId, organizationName));
        assets.Add(CreateFutureConsiderationAsset(scenario, TradeSide.OtherOrganization, organizationId, organizationName));
        return assets
            .GroupBy(asset => asset.AssetId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private IReadOnlyList<TradeAsset> CreateDraftPickAssets(NewGmScenarioSnapshot scenario, TradeSide side, string organizationId, string organizationName)
    {
        var rounds = scenario.LeagueProfile.Rulebook.DraftRules?.Rounds > 0
            ? Math.Min(7, scenario.LeagueProfile.Rulebook.DraftRules.Rounds)
            : 7;
        return Enumerable.Range(1, 3)
            .SelectMany(yearOffset => Enumerable.Range(1, rounds).Select(round =>
                CreateDraftPickAsset(scenario, side, organizationId, organizationName, round, scenario.Season.Year + yearOffset)))
            .ToArray();
    }

    public TradeAsset CreateDraftPickAsset(NewGmScenarioSnapshot scenario, TradeSide side, string organizationId, string organizationName, int round, int year)
    {
        var value = Math.Clamp(60 - round * 5 - Math.Max(0, year - scenario.Season.Year - 1) * 3, 10, 60);
        return new TradeAsset(
            TradeAssetType.DraftPick,
            side,
            organizationId,
            organizationName,
            $"draft-pick:{organizationId}:{year}:R{round}",
            $"{year} {Ordinal(round)} Round Pick (Round {round})",
            null,
            null,
            0m,
            value,
            $"Original owner: {organizationName}; current owner: {organizationName}; protected: no placeholder protection; estimated value {value}.");
    }

    public TradeAsset CreateFutureConsiderationAsset(NewGmScenarioSnapshot scenario, TradeSide side, string organizationId, string organizationName)
    {
        var asset = new TradeAsset(
            TradeAssetType.FutureConsideration,
            side,
            organizationId,
            organizationName,
            $"future-consideration:{organizationId}:{scenario.Season.Year}:{Guid.NewGuid():N}",
            "Future consideration placeholder",
            null,
            null,
            0m,
            8,
            "Future consideration placeholder; not conditional pick logic.");
        asset.Validate();
        return asset;
    }

    public TradeEvaluation EvaluateTrade(NewGmScenarioSnapshot scenario, TradeOffer offer) =>
        _strategy.EvaluateTrade(scenario, offer);

    public TradeDecisionResult ProposeTrade(EngineRegistry registry, NewGmScenarioSnapshot scenario, TradeOffer offer)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        offer.Validate();

        var window = new TradeDeadlineService().GetWindow(scenario, registry.Rulebook);
        if (!window.TradesAllowed)
        {
            var failed = offer with
            {
                Status = TradeOfferStatus.FailedValidation,
                Evaluation = new TradeEvaluation(TradeOfferStatus.FailedValidation, -100, "Trade deadline has passed.", new[] { "Trade deadline has passed." }, 0m, 0)
            };
            var failedScenario = UpsertOffer(scenario, failed);
            QueueTradeEvent(registry, failedScenario, LegacyEventType.TradeFailedValidation, "Trade failed validation", "Trade deadline has passed.", failed);
            return Result(false, failedScenario, failed, failed.Evaluation, new[] { Inbox(failedScenario, LegacyEventType.TradeFailedValidation, "Trade deadline has passed", "New trade proposals are closed after the deadline.", LegacyEventSeverity.Warning) }, Array.Empty<LeagueTransaction>(), "Trade deadline has passed.");
        }

        var validation = ValidateOffer(scenario, offer, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        if (validation is not null)
        {
            var failed = offer with
            {
                Status = TradeOfferStatus.FailedValidation,
                Evaluation = new TradeEvaluation(TradeOfferStatus.FailedValidation, -100, validation, new[] { validation }, 0m, 0)
            };
            var failedScenario = UpsertOffer(scenario, failed);
            QueueTradeEvent(registry, failedScenario, LegacyEventType.TradeFailedValidation, "Trade failed validation", validation, failed);
            return Result(false, failedScenario, failed, failed.Evaluation, new[] { Inbox(failedScenario, LegacyEventType.TradeFailedValidation, "Trade failed validation", validation, LegacyEventSeverity.Warning) }, Array.Empty<LeagueTransaction>(), validation);
        }

        var evaluation = EvaluateTrade(scenario, offer);
        var evaluated = offer with { Status = evaluation.Decision, Evaluation = evaluation };
        var next = UpsertOffer(scenario, evaluated);
        QueueTradeEvent(registry, next, LegacyEventType.TradeProposed, "Trade proposed", TradeSummary(evaluated), evaluated);

        var inbox = new List<AlphaInboxItem>
        {
            Inbox(next, LegacyEventType.TradeProposed, "Trade proposed", TradeSummary(evaluated))
        };

        if (evaluation.Decision == TradeOfferStatus.Accepted)
        {
            QueueTradeEvent(registry, next, LegacyEventType.TradeAccepted, "Trade accepted pending GM approval", evaluation.Explanation, evaluated);
            var pending = new PendingGmActionService().CreatePendingAction(
                registry,
                next,
                PendingGmActionType.ApproveTrade,
                evaluated.TradeOfferId,
                evaluation.Explanation,
                "Approve the accepted trade or decline it. No roster or rights change occurs until approval.");
            next = UpsertOffer(pending.ScenarioSnapshot, evaluated);
            inbox.AddRange(pending.InboxItems);
            inbox.Add(Inbox(next, LegacyEventType.TradeAccepted, "Trade accepted", evaluation.Explanation, LegacyEventSeverity.Warning));
            return Result(true, next, evaluated, evaluation, inbox, Array.Empty<LeagueTransaction>(), "Trade accepted by the other team and awaits GM approval.");
        }

        var eventType = evaluation.Decision == TradeOfferStatus.Countered ? LegacyEventType.TradeCountered : LegacyEventType.TradeRejected;
        QueueTradeEvent(registry, next, eventType, evaluation.Decision == TradeOfferStatus.Countered ? "Trade countered" : "Trade rejected", evaluation.Explanation, evaluated);
        inbox.Add(Inbox(next, eventType, evaluation.Decision == TradeOfferStatus.Countered ? "Trade countered" : "Trade rejected", evaluation.Explanation));
        return Result(true, next, evaluated, evaluation, inbox, Array.Empty<LeagueTransaction>(), evaluation.Explanation);
    }

    public TradeDecisionResult WithdrawTrade(EngineRegistry registry, NewGmScenarioSnapshot scenario, string tradeOfferId)
    {
        var offer = RequireOffer(scenario, tradeOfferId);
        var withdrawn = offer with { Status = TradeOfferStatus.Withdrawn };
        var next = UpsertOffer(scenario, withdrawn);
        QueueTradeEvent(registry, next, LegacyEventType.TradeWithdrawn, "Trade withdrawn", TradeSummary(withdrawn), withdrawn);
        return Result(true, next, withdrawn, withdrawn.Evaluation, new[] { Inbox(next, LegacyEventType.TradeWithdrawn, "Trade withdrawn", TradeSummary(withdrawn)) }, Array.Empty<LeagueTransaction>(), "Trade offer withdrawn.");
    }

    public TradeDecisionResult CompleteAcceptedTrade(EngineRegistry registry, NewGmScenarioSnapshot scenario, string tradeOfferId)
    {
        var offer = RequireOffer(scenario, tradeOfferId);
        if (offer.Status != TradeOfferStatus.Accepted)
        {
            return Result(false, scenario, offer, offer.Evaluation, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Only accepted trades can be completed.");
        }

        var roster = scenario.AlphaSnapshot.Roster;
        foreach (var asset in offer.PlayerGives.Where(asset => asset.AssetType == TradeAssetType.Player))
        {
            roster = roster with { Players = roster.Players.Where(player => player.PersonId != asset.AssetId).ToArray() };
        }

        foreach (var asset in offer.PlayerReceives.Where(asset => asset.AssetType == TradeAssetType.Player))
        {
            if (!roster.Players.Any(player => player.PersonId == asset.AssetId))
            {
                roster = roster with
                {
                    Players = roster.Players.Append(new RosterPlayer(
                        asset.AssetId,
                        asset.Position ?? RosterPosition.Unknown,
                        RosterStatus.Active,
                        scenario.CurrentDate,
                        Age: asset.Age,
                        AcquisitionSource: PlayerAcquisitionSource.Trade)).ToArray()
                };
            }
        }

        var prospects = scenario.ProspectRights
            .Where(prospect => offer.PlayerGives.All(asset => asset.AssetType != TradeAssetType.ProspectRights || asset.AssetId != prospect.ProspectPersonId))
            .ToList();
        foreach (var asset in offer.PlayerReceives.Where(asset => asset.AssetType == TradeAssetType.ProspectRights))
        {
            if (prospects.All(prospect => prospect.ProspectPersonId != asset.AssetId))
            {
                prospects.Add(new DraftRightsRecord(asset.AssetId, asset.DisplayName, asset.Age ?? 17, asset.Position ?? RosterPosition.Unknown, 99, 999, ProspectStatus.DraftRightsHeld, asset.Summary, ScoutingConfidenceLevel.Low, "Acquired by trade."));
            }
        }

        var completed = offer with { Status = TradeOfferStatus.Completed };
        var alpha = scenario.AlphaSnapshot with { Roster = roster };
        var next = UpsertOffer(scenario with { AlphaSnapshot = alpha, ProspectRights = prospects.ToArray() }, completed);
        next = new CareerHistoryService().RecordTradeCompleted(next, completed);
        QueueTradeEvent(registry, next, LegacyEventType.TradeCompleted, "Trade completed", TradeSummary(completed), completed);
        var transaction = new LeagueTransaction(
            $"transaction:trade:{Guid.NewGuid():N}",
            new DateTimeOffset(next.CurrentDate.Year, next.CurrentDate.Month, next.CurrentDate.Day, 16, 0, 0, TimeSpan.Zero),
            next.Organization.OrganizationId,
            next.Organization.Name,
            PrimaryPersonId(completed),
            PrimaryPersonName(completed),
            LeagueTransactionType.TradeCompleted,
            LeagueNewsCategory.RosterMoves,
            TradeSummary(completed));
        var completedInbox = new List<AlphaInboxItem>
        {
            Inbox(next, LegacyEventType.TradeCompleted, "Trade completed", TradeSummary(completed))
        };
        if (completed.Evaluation is not null)
        {
            var reactionSummary = string.Join(" ", completed.Evaluation.StaffReactionNotes.Concat(completed.Evaluation.PlayerReactionNotes).Take(3));
            completedInbox.Add(Inbox(next, LegacyEventType.TradeCompleted, "Trade reaction", reactionSummary));
        }

        return Result(true, next, completed, completed.Evaluation, completedInbox, new[] { transaction }, "Trade completed after GM approval.");
    }

    public TradeDecisionResult DeclineAcceptedTrade(EngineRegistry registry, NewGmScenarioSnapshot scenario, string tradeOfferId)
    {
        var offer = RequireOffer(scenario, tradeOfferId);
        var rejected = offer with { Status = TradeOfferStatus.Withdrawn };
        var next = UpsertOffer(scenario, rejected);
        QueueTradeEvent(registry, next, LegacyEventType.TradeWithdrawn, "Accepted trade declined", TradeSummary(rejected), rejected);
        return Result(true, next, rejected, rejected.Evaluation, new[] { Inbox(next, LegacyEventType.TradeWithdrawn, "Accepted trade declined", "GM declined the accepted trade. Rosters and rights are unchanged.") }, Array.Empty<LeagueTransaction>(), "Accepted trade declined. No roster or rights change was made.");
    }

    public TradeDecisionResult ProposeSimpleTradeForBlockEntry(EngineRegistry registry, NewGmScenarioSnapshot scenario, string blockPersonId)
    {
        var blockEntry = RequireBlockEntry(scenario, blockPersonId);
        var outgoing = scenario.AlphaSnapshot.Roster.ActivePlayers
            .OrderByDescending(player => PlayerAssetValue(scenario, player.PersonId, player.Position, player.Age ?? PersonAge(scenario, player.PersonId)))
            .FirstOrDefault(player => player.Position == blockEntry.Position)
            ?? scenario.AlphaSnapshot.Roster.ActivePlayers.OrderByDescending(player => PlayerAssetValue(scenario, player.PersonId, player.Position, player.Age ?? PersonAge(scenario, player.PersonId))).First();
        var offer = CreateOffer(
            scenario,
            blockEntry.OrganizationId,
            blockEntry.TeamName,
            new[] { CreateRosterPlayerAsset(scenario, outgoing.PersonId) },
            new[] { CreateRosterPlayerAsset(scenario, blockEntry.PersonId, TradeSide.OtherOrganization) });
        return ProposeTrade(registry, scenario, offer);
    }

    private static string? ValidateOffer(NewGmScenarioSnapshot scenario, TradeOffer offer, LegacyEngine.RuleEngine.Rulebook? rulebook)
    {
        if (offer.PlayerGives.Count == 0 || offer.PlayerReceives.Count == 0)
        {
            return "Trade must include assets on both sides.";
        }

        foreach (var asset in offer.PlayerGives)
        {
            if (asset.Side != TradeSide.PlayerOrganization)
            {
                return "Player-side outgoing assets must belong to the player organization.";
            }

            if (asset.AssetType == TradeAssetType.Player && scenario.AlphaSnapshot.Roster.FindPlayer(asset.AssetId) is null)
            {
                return $"{asset.DisplayName} is not on the player organization's roster.";
            }

            if (asset.AssetType == TradeAssetType.ProspectRights && scenario.ProspectRights.All(prospect => prospect.ProspectPersonId != asset.AssetId))
            {
                return $"{asset.DisplayName} is not controlled in player prospect rights.";
            }
        }

        foreach (var asset in offer.PlayerReceives)
        {
            if (asset.Side != TradeSide.OtherOrganization)
            {
                return "Incoming assets must belong to the other organization.";
            }

            if (!string.Equals(asset.OrganizationId, offer.OtherOrganizationId, StringComparison.Ordinal))
            {
                return $"{asset.DisplayName} is not controlled by {offer.OtherOrganizationName}.";
            }

            if (asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights)
            {
                var block = scenario.TradeBlock?.Find(asset.AssetId);
                if (block is not null && block.OrganizationId != offer.OtherOrganizationId)
                {
                    return $"{asset.DisplayName} is not available from {offer.OtherOrganizationName}.";
                }
            }
        }

        var cap = new SalaryCapService().ProjectAfterTrade(scenario, rulebook ?? scenario.LeagueProfile.Rulebook, offer);
        if (!cap.IsCompliant)
        {
            return cap.Reasons.FirstOrDefault(reason => reason.Contains("salary cap", StringComparison.OrdinalIgnoreCase))
                ?? cap.Reasons.FirstOrDefault(reason => reason.Contains("contract limit", StringComparison.OrdinalIgnoreCase))
                ?? "Trade would violate salary cap or contract rules.";
        }

        var profile = cap.Before.Profile;
        var activeAfter = scenario.AlphaSnapshot.Roster.ActivePlayers.Count
            - offer.PlayerGives.Count(asset => asset.AssetType == TradeAssetType.Player)
            + offer.PlayerReceives.Count(asset => asset.AssetType == TradeAssetType.Player);
        if (profile.IsEnabled && profile.MaximumRosterSize > 0 && activeAfter > profile.MaximumRosterSize)
        {
            return "Trade would leave roster invalid by exceeding the professional roster limit.";
        }

        return null;
    }

    private static TradeBlockEntry CreateBlockEntry(NewGmScenarioSnapshot scenario, Person person, string organizationId, string teamName, int index)
    {
        var position = PositionFor(index);
        var age = person.CalculateAge(scenario.CurrentDate);
        var value = Math.Clamp(38 + (index * 7 % 48), 25, 86);
        var entry = new TradeBlockEntry(
            $"trade-block-entry:{person.PersonId}",
            person.PersonId,
            organizationId,
            teamName,
            person.Identity.DisplayName,
            position,
            age,
            PlayerTypeFor(position, index),
            RoleFor(position, index),
            index % 4 == 0 ? "Expiring junior agreement" : "Signed through common pre-draft expiry",
            1_200m + index % 8 * 225m,
            AskingPrice(index, position),
            Reason(index),
            value >= 70 ? TradeInterest.High : value >= 48 ? TradeInterest.Medium : TradeInterest.Low,
            value);
        entry.Validate();
        return entry;
    }

    private static TradeAsset AssetFromBlockEntry(TradeBlockEntry entry, TradeAssetType assetType, TradeSide side) =>
        new(
            assetType,
            side,
            entry.OrganizationId,
            entry.TeamName,
            entry.PersonId,
            entry.Name,
            entry.Position,
            entry.Age,
            entry.SalaryImpact,
            entry.AssetValue,
            $"{entry.PlayerType}; {entry.CurrentRole}; asking price: {entry.AskingPriceSummary}");

    private static IReadOnlyList<TradeAsset> SyntheticOtherRosterAssets(NewGmScenarioSnapshot scenario, string organizationId, string organizationName, int startIndex, int count)
    {
        if (count <= 0)
        {
            return Array.Empty<TradeAsset>();
        }

        return Enumerable.Range(startIndex, count)
            .Select(index =>
            {
                var position = PositionFor(index);
                var age = index % 5 == 0 ? 21 : index % 4 == 0 ? 20 : 18 + index % 3;
                var value = Math.Clamp(34 + StableHash($"{organizationId}:roster:{index}") % 42, 24, 76);
                return new TradeAsset(
                    TradeAssetType.Player,
                    TradeSide.OtherOrganization,
                    organizationId,
                    organizationName,
                    $"trade-roster:{organizationId}:{index + 1:00}",
                    SyntheticName(organizationId, index),
                    position,
                    age,
                    1_600m + index % 9 * 300m,
                    value,
                    $"{PlayerTypeFor(position, index)}; {RoleFor(position, index)}; active roster player");
            })
            .ToArray();
    }

    private static IReadOnlyList<TradeAsset> SyntheticOtherProspectAssets(NewGmScenarioSnapshot scenario, string organizationId, string organizationName, int startIndex, int count)
    {
        if (count <= 0)
        {
            return Array.Empty<TradeAsset>();
        }

        return Enumerable.Range(startIndex, count)
            .Select(index =>
            {
                var position = PositionFor(index + 3);
                var value = Math.Clamp(30 + StableHash($"{organizationId}:prospect:{index}") % 36, 22, 66);
                return new TradeAsset(
                    TradeAssetType.ProspectRights,
                    TradeSide.OtherOrganization,
                    organizationId,
                    organizationName,
                    $"trade-prospect:{organizationId}:{index + 1:00}",
                    SyntheticName(organizationId, index + 37),
                    position,
                    17 + index % 3,
                    0m,
                    value,
                    $"{position} prospect rights; development path still forming");
            })
            .ToArray();
    }

    private static NewGmScenarioSnapshot UpsertOffer(NewGmScenarioSnapshot scenario, TradeOffer offer) =>
        scenario with
        {
            TradeOffers = scenario.TradeOffers.Any(item => item.TradeOfferId == offer.TradeOfferId)
                ? scenario.TradeOffers.Select(item => item.TradeOfferId == offer.TradeOfferId ? offer : item).ToArray()
                : scenario.TradeOffers.Append(offer).ToArray()
        };

    private static TradeOffer RequireOffer(NewGmScenarioSnapshot scenario, string tradeOfferId) =>
        scenario.TradeOffers.SingleOrDefault(offer => offer.TradeOfferId == tradeOfferId)
        ?? throw new ArgumentException("Trade offer was not found.", nameof(tradeOfferId));

    private static TradeBlockEntry RequireBlockEntry(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.TradeBlock?.Find(personId)
        ?? throw new ArgumentException("Trade block player was not found.", nameof(personId));

    private static int PlayerAssetValue(NewGmScenarioSnapshot scenario, string personId, RosterPosition position, int age)
    {
        var stat = scenario.CareerStatSummaries.FirstOrDefault(stat => stat.PersonId == personId);
        var points = stat?.Points ?? 20;
        var ageBonus = age <= 17 ? 12 : age >= 20 ? -4 : 4;
        var positionBonus = position == RosterPosition.Goalie ? 8 : position == RosterPosition.Defense ? 5 : 0;
        return Math.Clamp(30 + points / 5 + ageBonus + positionBonus, 20, 90);
    }

    private static decimal SalaryFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.PersonId == personId && contract.Status == ContractStatus.Signed)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .Select(contract => contract.Money.SalaryOrStipend)
            .FirstOrDefault();

    private static string ContractStatusText(NewGmScenarioSnapshot scenario, string personId)
    {
        var contract = scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.PersonId == personId)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .FirstOrDefault();
        return contract is null ? "No contract tracked" : $"{contract.Status} through {contract.Term.EndDate:yyyy-MM-dd}";
    }

    private static string TradeSummary(TradeOffer offer) =>
        $"{offer.OtherOrganizationName}: {string.Join(", ", offer.PlayerGives.Select(asset => asset.DisplayName))} for {string.Join(", ", offer.PlayerReceives.Select(asset => asset.DisplayName))}.";

    private static string? PrimaryPersonId(TradeOffer offer) =>
        offer.PlayerReceives.FirstOrDefault(asset => asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights)?.AssetId
        ?? offer.PlayerGives.FirstOrDefault()?.AssetId;

    private static string PrimaryPersonName(TradeOffer offer) =>
        offer.PlayerReceives.FirstOrDefault(asset => asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights)?.DisplayName
        ?? offer.PlayerGives.FirstOrDefault()?.DisplayName
        ?? "No player selected";

    private static void QueueTradeEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string description, TradeOffer offer)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 16, 0, 0, TimeSpan.Zero),
            eventType,
            eventType is LegacyEventType.TradeRejected or LegacyEventType.TradeFailedValidation ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: PrimaryPersonId(offer), OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["trade_offer_id"] = offer.TradeOfferId,
                ["team_name"] = scenario.Organization.Name,
                ["other_team_name"] = offer.OtherOrganizationName,
                ["person_name"] = PrimaryPersonName(offer),
                ["reason"] = offer.Evaluation?.Explanation
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, LegacyEventSeverity severity = LegacyEventSeverity.Notice) =>
        new($"inbox:trade:{Guid.NewGuid():N}", new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 16, 0, 0, TimeSpan.Zero), eventType, severity, title, summary, null);

    private static TradeDecisionResult Result(bool success, NewGmScenarioSnapshot scenario, TradeOffer? offer, TradeEvaluation? evaluation, IReadOnlyList<AlphaInboxItem> inbox, IReadOnlyList<LeagueTransaction> transactions, string message)
    {
        var result = new TradeDecisionResult(success, scenario, offer, evaluation, inbox, transactions, message);
        result.Validate();
        return result;
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.TradeBlock?.Find(personId)?.Name
        ?? personId;

    private static int PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.TradeBlock?.Find(personId)?.Age
        ?? 18;

    private static RosterPosition PositionFor(int index) =>
        index switch
        {
            0 or 11 => RosterPosition.Goalie,
            1 or 4 or 8 or 13 => RosterPosition.Defense,
            _ when index % 3 == 0 => RosterPosition.Center,
            _ when index % 3 == 1 => RosterPosition.LeftWing,
            _ => RosterPosition.RightWing
        };

    private static string PlayerTypeFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => "Goalie",
            RosterPosition.Defense => index % 2 == 0 ? "Mobile defense" : "Stay-at-home defense",
            RosterPosition.Center => "Two-way center",
            _ => index % 2 == 0 ? "Scoring winger" : "Energy winger"
        };

    private static string RoleFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => index % 5 == 0 ? "Starting Goalie" : index % 2 == 0 ? "Tandem Goalie" : "Backup Goalie",
            RosterPosition.Defense => index % 7 == 0 ? "Top Pair Defenseman" : index % 3 == 0 ? "Second Pair Defenseman" : "Depth Defenseman",
            RosterPosition.Center => index % 5 == 0 ? "Top Six Forward" : index % 3 == 0 ? "Middle Six Forward" : "Checking Line Forward",
            _ => index % 6 == 0 ? "First Line Forward" : index % 2 == 0 ? "Top Six Forward" : "Depth Forward"
        };

    private static string LineupSummaryFor(NewGmScenarioSnapshot scenario, string personId)
    {
        var assignment = (scenario.CurrentLineup ?? new LineupService().BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers))
            .Assignments.FirstOrDefault(assignment => assignment.PersonId == personId);
        return assignment is null
            ? "lineup role unassigned"
            : $"{LineupDisplay.Role(assignment.CurrentRole)} at {assignment.SlotLabel}; potential {LineupDisplay.Role(assignment.PotentialRole)}";
    }

    private static string Reason(int index) =>
        new[] { "rebuilding", "roster surplus", "budget pressure", "veteran available", "prospect blocked", "needs change", "poor fit" }[index % 7];

    private static string AskingPrice(int index, RosterPosition position) =>
        position == RosterPosition.Goalie
            ? "comparable goalie or second-round pick placeholder"
            : index % 4 == 0 ? "defense help or high pick placeholder" : "similar roster player or prospect rights";

    private static string Ordinal(int value) =>
        (value % 100) is 11 or 12 or 13
            ? $"{value}th"
            : (value % 10) switch
            {
                1 => $"{value}st",
                2 => $"{value}nd",
                3 => $"{value}rd",
                _ => $"{value}th"
            };

    private static string SyntheticName(string organizationId, int index)
    {
        var first = new[]
        {
            "Mason", "Ethan", "Caleb", "Noah", "Owen", "Lukas", "Adam", "Karel", "Felix", "Tomas",
            "Nolan", "Elias", "Cole", "Connor", "Julian", "Marek", "Simon", "Anton", "Peter", "Oscar"
        };
        var last = new[]
        {
            "Reid", "Clark", "Hayes", "Bishop", "Kovacs", "Lindholm", "Roy", "Stewart", "Novak", "Sullivan",
            "Berg", "Price", "Mercier", "Foster", "Larsson", "Morgan", "Fraser", "Havel", "Grant", "Nilsson"
        };
        var seed = StableHash($"{organizationId}:{index}");
        return $"{first[seed % first.Length]} {last[(seed / first.Length + index) % last.Length]}";
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in text)
            {
                hash = hash * 31 + character;
            }

            return Math.Abs(hash);
        }
    }

    public static IReadOnlyList<Person> CreateTradeBlockPeople(DateOnly startDate, int seasonYear, NameGenerator nameGenerator, NameUniquenessRegistry nameRegistry)
    {
        var origins = new[] { NameOrigin.CanadaEnglish, NameOrigin.CanadaFrench, NameOrigin.Usa, NameOrigin.Sweden, NameOrigin.Finland, NameOrigin.Czechia, NameOrigin.Slovakia, NameOrigin.Germany };
        return Enumerable.Range(0, 24)
            .Select(index =>
            {
                var name = nameGenerator.Generate(nameRegistry, $"new-gm-scenario:{seasonYear}:trade-block", origins);
                var age = index % 6 == 0 ? 20 : index % 5 == 0 ? 19 : index % 4 == 0 ? 16 : 17 + index % 2;
                return NewGmScenarioBootstrapper.CreateScenarioPersonForGeneratedSystems(
                    $"person-trade-block-{index + 1:000}",
                    name.FirstName,
                    name.LastName,
                    startDate.AddYears(-age).AddDays(-(index * 19 + 3)),
                    name.Nationality,
                    name.Birthplace,
                    "trade-block");
            })
            .ToArray();
    }
}
