using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Names;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class TradeService
{
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
            $"{player.Position}, {ContractStatusText(scenario, personId)}");
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

    public TradeAsset CreateDraftPickAsset(NewGmScenarioSnapshot scenario, TradeSide side, string organizationId, string organizationName, int round, int year)
    {
        var value = Math.Clamp(48 - round * 4, 10, 55);
        return new TradeAsset(
            TradeAssetType.DraftPick,
            side,
            organizationId,
            organizationName,
            $"draft-pick:{organizationId}:{year}:R{round}",
            $"{year} Round {round} pick",
            null,
            null,
            0m,
            value,
            "Draft pick placeholder");
    }

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

        var validation = ValidateOffer(scenario, offer);
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
        return Result(true, next, completed, completed.Evaluation, new[] { Inbox(next, LegacyEventType.TradeCompleted, "Trade completed", TradeSummary(completed)) }, new[] { transaction }, "Trade completed after GM approval.");
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

    private static string? ValidateOffer(NewGmScenarioSnapshot scenario, TradeOffer offer)
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

        foreach (var asset in offer.PlayerReceives.Where(asset => asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights))
        {
            var block = scenario.TradeBlock?.Find(asset.AssetId);
            if (block is null || block.OrganizationId != offer.OtherOrganizationId)
            {
                return $"{asset.DisplayName} is not available from {offer.OtherOrganizationName}.";
            }
        }

        return null;
    }

    private static TradeEvaluation EvaluateTrade(NewGmScenarioSnapshot scenario, TradeOffer offer)
    {
        var giveValue = offer.PlayerGives.Sum(asset => asset.Value);
        var receiveValue = offer.PlayerReceives.Sum(asset => asset.Value);
        var otherTeamScore = giveValue - receiveValue;
        var budgetImpact = offer.PlayerReceives.Sum(asset => asset.SalaryImpact) - offer.PlayerGives.Sum(asset => asset.SalaryImpact);
        if (budgetImpact < 0)
        {
            otherTeamScore -= 4;
        }

        if (offer.PlayerGives.Any(asset => asset.Position == RosterPosition.Defense))
        {
            otherTeamScore += 4;
        }

        var decision = otherTeamScore >= -5
            ? TradeOfferStatus.Accepted
            : otherTeamScore >= -22
                ? TradeOfferStatus.Countered
                : TradeOfferStatus.Rejected;
        var otherName = offer.OtherOrganizationName;
        var reasons = new List<string>
        {
            $"Asset value balance from {otherName}'s view: {otherTeamScore}.",
            budgetImpact > 0 ? "The deal adds budget pressure for the player organization." : "The deal is budget-neutral or saves money for the player organization."
        };
        if (decision == TradeOfferStatus.Rejected)
        {
            reasons.Add("The offered package does not meet the asking price or roster need.");
        }
        else if (decision == TradeOfferStatus.Countered)
        {
            reasons.Add($"{otherName} would need a better fit, pick, or prospect added.");
        }
        else
        {
            reasons.Add($"{otherName} believes the offer fits their direction.");
        }

        var explanation = decision switch
        {
            TradeOfferStatus.Accepted => $"{otherName} accepted the framework because the value and roster fit are close enough. GM approval is still required.",
            TradeOfferStatus.Countered => $"{otherName} countered in principle because the value is close, but they want a stronger asset or draft pick.",
            _ => $"{otherName} rejected the offer because the package does not match their needs or asking price."
        };
        return new TradeEvaluation(decision, otherTeamScore, explanation, reasons, budgetImpact, offer.PlayerReceives.Count(asset => asset.AssetType == TradeAssetType.Player) - offer.PlayerGives.Count(asset => asset.AssetType == TradeAssetType.Player));
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
            RosterPosition.Goalie => "Goalie competition",
            RosterPosition.Defense => index % 2 == 0 ? "Second-pair option" : "Depth defense",
            RosterPosition.Center => "Middle-six center",
            _ => "Depth winger"
        };

    private static string Reason(int index) =>
        new[] { "rebuilding", "roster surplus", "budget pressure", "veteran available", "prospect blocked", "needs change", "poor fit" }[index % 7];

    private static string AskingPrice(int index, RosterPosition position) =>
        position == RosterPosition.Goalie
            ? "comparable goalie or second-round pick placeholder"
            : index % 4 == 0 ? "defense help or high pick placeholder" : "similar roster player or prospect rights";

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
