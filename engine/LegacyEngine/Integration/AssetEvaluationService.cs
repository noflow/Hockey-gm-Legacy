using LegacyEngine.Contracts;
using LegacyEngine.Draft;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class PositionScarcityService
{
    public PositionScarcityProfile BuildProfile(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var contexts = Enum.GetValues<PositionMarketPosition>()
            .Select(position => BuildContext(scenario, position))
            .ToArray();
        var thinnest = contexts.OrderByDescending(context => context.ScarcityScore).First();
        var deepest = contexts.OrderBy(context => context.ScarcityScore).First();
        var profile = new PositionScarcityProfile(
            scenario.LeagueProfile.Identity.LeagueId,
            scenario.Season.Year,
            contexts,
            $"Position Market: {Label(thinnest.Position)} is {thinnest.ScarcityLevel}; {Label(deepest.Position)} is {deepest.ScarcityLevel}.");
        profile.Validate();
        return profile;
    }

    private static PositionMarketContext BuildContext(NewGmScenarioSnapshot scenario, PositionMarketPosition position)
    {
        var currentLeagueDepth = CountCurrentLeagueDepth(scenario, position);
        var freeAgents = scenario.FreeAgentMarket?.FreeAgents.Count(agent => MarketPositionFor(agent.Position, agent.ShootsCatches, agent.PersonId) == position) ?? 0;
        var draftDepth = scenario.AlphaSnapshot.DraftBoard.Entries.Count(entry => MarketPositionFor(entry) == position);
        var injuryDrag = scenario.AlphaSnapshot.Injuries.Count(injury => injury.IsActive && MarketPositionForRosterPerson(scenario, injury.PersonId) == position);
        var tradeSupply = scenario.TradeBlock?.Entries.Count(entry => MarketPositionFor(entry.Position, null, entry.PersonId) == position) ?? 0;
        var pipeline = scenario.ProspectRights.Count(prospect => MarketPositionFor(prospect.Position, null, prospect.ProspectPersonId) == position);
        var targetDepth = position switch
        {
            PositionMarketPosition.G => 10,
            PositionMarketPosition.RD or PositionMarketPosition.LD => 18,
            _ => 22
        };
        var supply = currentLeagueDepth + freeAgents * 2 + draftDepth + tradeSupply * 2 + pipeline - injuryDrag * 2;
        var scarcityScore = Math.Clamp(50 + (targetDepth - supply) * 3 + ScarcityBias(position), 0, 100);
        var level = scarcityScore switch
        {
            >= 82 => PositionScarcityLevel.Critical,
            >= 68 => PositionScarcityLevel.Scarce,
            >= 56 => PositionScarcityLevel.Thin,
            <= 32 => PositionScarcityLevel.Oversupplied,
            _ => PositionScarcityLevel.Normal
        };
        var summary = $"{Label(position)} market is {level}: league depth {currentLeagueDepth}, free agents {freeAgents}, draft depth {draftDepth}, trade supply {tradeSupply}, pipeline {pipeline}, injuries {injuryDrag}.";
        var context = new PositionMarketContext(position, level, currentLeagueDepth, freeAgents, draftDepth, injuryDrag, tradeSupply, pipeline, scarcityScore, summary);
        context.Validate();
        return context;
    }

    private static int CountCurrentLeagueDepth(NewGmScenarioSnapshot scenario, PositionMarketPosition position)
    {
        var playerTeamDepth = scenario.AlphaSnapshot.Roster.ActivePlayers.Count(player => MarketPositionFor(player.Position, null, player.PersonId) == position);
        var syntheticLeagueDepth = SeasonFrameworkService.LeagueTeams(scenario).Count(team => team.OrganizationId != scenario.Organization.OrganizationId) * (position switch
        {
            PositionMarketPosition.G => 2,
            PositionMarketPosition.RD or PositionMarketPosition.LD => 3,
            _ => 4
        });
        return playerTeamDepth + syntheticLeagueDepth;
    }

    private static int ScarcityBias(PositionMarketPosition position) =>
        position switch
        {
            PositionMarketPosition.RD => 8,
            PositionMarketPosition.G => 6,
            PositionMarketPosition.C => 3,
            PositionMarketPosition.LW or PositionMarketPosition.RW => -4,
            _ => 0
        };

    private static PositionMarketPosition? MarketPositionForRosterPerson(NewGmScenarioSnapshot scenario, string personId)
    {
        var roster = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        if (roster is not null)
        {
            return MarketPositionFor(roster.Position, null, personId);
        }

        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId);
        return draft is null ? null : MarketPositionFor(draft);
    }

    internal static PositionMarketPosition MarketPositionFor(DraftBoardEntry entry) =>
        MarketPositionFor(entry.Bio?.Position ?? RosterPosition.Unknown, entry.Bio?.ShootsCatches, entry.ProspectPersonId);

    internal static PositionMarketPosition MarketPositionFor(RosterPosition position, string? shootsCatches, string seed) =>
        position switch
        {
            RosterPosition.Center => PositionMarketPosition.C,
            RosterPosition.LeftWing => PositionMarketPosition.LW,
            RosterPosition.RightWing => PositionMarketPosition.RW,
            RosterPosition.Goalie => PositionMarketPosition.G,
            RosterPosition.Defense when shootsCatches?.Contains("R", StringComparison.OrdinalIgnoreCase) == true => PositionMarketPosition.RD,
            RosterPosition.Defense when shootsCatches?.Contains("L", StringComparison.OrdinalIgnoreCase) == true => PositionMarketPosition.LD,
            RosterPosition.Defense => StableHash(seed) % 3 == 0 ? PositionMarketPosition.RD : PositionMarketPosition.LD,
            _ => PositionMarketPosition.C
        };

    public static string Label(PositionMarketPosition position) =>
        position switch
        {
            PositionMarketPosition.C => "C",
            PositionMarketPosition.LW => "LW",
            PositionMarketPosition.RW => "RW",
            PositionMarketPosition.LD => "LD",
            PositionMarketPosition.RD => "RD",
            PositionMarketPosition.G => "G",
            _ => position.ToString()
        };

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value)
            {
                hash = hash * 31 + ch;
            }

            return Math.Abs(hash);
        }
    }
}

public sealed class AssetEvaluationService
{
    private readonly PositionScarcityService _scarcity = new();

    public NewGmScenarioSnapshot EnsureEvaluations(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = scenario.PlayerRatings.Count == 0
            ? new PlayerRatingService().EnsureRatings(new HockeyIntelligenceRatingService().EnsureRatings(scenario))
            : scenario;
        var market = _scarcity.BuildProfile(prepared);
        var playerValues = BuildPlayerValues(prepared, market);
        var draftPickValues = BuildDraftPickValues(prepared, market);
        var evaluations = playerValues
            .Select(value => new AssetEvaluation(value.PersonId, value.PlayerName, value.EvaluationType, value.Trade.Score, value.Trade.Band, value.DisplaySummary, value.Reasons))
            .Concat(draftPickValues.Select(pick => new AssetEvaluation(pick.PickId, pick.DisplayName, AssetEvaluationType.DraftPick, pick.AssetScore, pick.Band, $"{pick.FutureProjection} {pick.RiskSummary}", new[] { pick.FutureProjection, pick.RiskSummary })))
            .ToArray();

        return prepared with
        {
            PositionScarcity = market,
            PlayerAssetValues = playerValues,
            DraftPickValues = draftPickValues,
            AssetEvaluations = evaluations
        };
    }

    public PositionScarcityProfile BuildPositionScarcity(NewGmScenarioSnapshot scenario) =>
        _scarcity.BuildProfile(scenario);

    public IReadOnlyList<PlayerAssetValue> BuildPlayerValues(NewGmScenarioSnapshot scenario, PositionScarcityProfile? scarcity = null)
    {
        scarcity ??= _scarcity.BuildProfile(scenario);
        var ids = scenario.AlphaSnapshot.Roster.ActivePlayers.Select(player => player.PersonId)
            .Concat(scenario.ProspectRights.Select(prospect => prospect.ProspectPersonId))
            .Concat(scenario.FreeAgentMarket?.FreeAgents.Select(agent => agent.PersonId) ?? Array.Empty<string>())
            .Concat(scenario.TradeBlock?.Entries.Select(entry => entry.PersonId) ?? Array.Empty<string>())
            .Concat(scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Distinct(StringComparer.Ordinal)
            .Take(260)
            .ToArray();

        return ids.Select(id => BuildPlayerValue(scenario, id, scenario.Organization.OrganizationId, scenario.Organization.Name, scarcity)).ToArray();
    }

    public PlayerAssetValue BuildPlayerValue(NewGmScenarioSnapshot scenario, string personId, string organizationId, string organizationName, PositionScarcityProfile? scarcity = null)
    {
        scarcity ??= scenario.PositionScarcity ?? _scarcity.BuildProfile(scenario);
        var context = PlayerContext(scenario, personId);
        var marketPosition = context.MarketPosition;
        var market = scarcity.For(marketPosition);
        var currentScore = CurrentScore(scenario, context);
        var futureScore = FutureScore(scenario, context);
        var contractScore = ContractScore(scenario, personId, context);
        var organizationalScore = OrganizationalScore(scenario, organizationId, context, currentScore, futureScore, market);
        var tradeScore = Math.Clamp((currentScore + futureScore + contractScore + organizationalScore) / 4 + ScarcityBonus(market.ScarcityLevel), -20, 120);
        var type = context.IsProspect ? AssetEvaluationType.Prospect : AssetEvaluationType.Player;
        var reasons = new[]
        {
            $"Current value: {context.CurrentSummary}",
            $"Future value: {context.FutureSummary}",
            $"Contract value: {ContractSummary(scenario, personId, contractScore)}",
            $"Market context: {market.Summary}",
            $"Organization fit: {OrganizationFitReason(scenario, organizationId, context)}"
        };
        var value = new PlayerAssetValue(
            personId,
            context.Name,
            type,
            new CurrentValue(currentScore, Band(currentScore), context.CurrentSummary),
            new FutureValue(futureScore, Band(futureScore), context.FutureSummary),
            new ContractValue(contractScore, Band(contractScore), ContractSummary(scenario, personId, contractScore)),
            new TradeValue(tradeScore, Band(tradeScore), $"Trade value blends current impact, future value, contract, team fit, and {market.ScarcityLevel} {PositionScarcityService.Label(marketPosition)} market."),
            new OrganizationalValue(organizationalScore, Band(organizationalScore), organizationId, organizationName, OrganizationFitReason(scenario, organizationId, context)),
            new PlayerMarketValue(personId, context.Name, context.Position, marketPosition, market.ScarcityLevel, market.ScarcityScore, market.Summary),
            reasons);
        value.Validate();
        return value;
    }

    public IReadOnlyList<DraftPickValue> BuildDraftPickValues(NewGmScenarioSnapshot scenario, PositionScarcityProfile? scarcity = null)
    {
        scarcity ??= scenario.PositionScarcity ?? _scarcity.BuildProfile(scenario);
        var rounds = scenario.LeagueProfile.Rulebook.DraftRules?.Rounds > 0
            ? Math.Min(7, scenario.LeagueProfile.Rulebook.DraftRules.Rounds)
            : 7;
        var deepestNeed = scarcity.Positions.OrderByDescending(position => position.ScarcityScore).First();
        return Enumerable.Range(1, 3)
            .SelectMany(offset => Enumerable.Range(1, rounds).Select(round =>
            {
                var year = scenario.Season.Year + offset;
                var score = Math.Clamp(64 - round * 6 - (offset - 1) * 3 + deepestNeed.ScarcityScore / 12, 12, 82);
                var pick = new DraftPickValue(
                    $"draft-pick:{scenario.Organization.OrganizationId}:{year}:R{round}",
                    $"{year} {Ordinal(round)} Round Pick",
                    year,
                    round,
                    scenario.Organization.Name,
                    scenario.Organization.Name,
                    score,
                    Band(score),
                    $"Future projection reflects a {deepestNeed.ScarcityLevel} {PositionScarcityService.Label(deepestNeed.Position)} market and class depth.",
                    round <= 2 ? "Premium pick risk is tied to draft-class volatility." : "Later pick carries wider uncertainty.");
                pick.Validate();
                return pick;
            }))
            .ToArray();
    }

    public string BuildPositionMarketText(NewGmScenarioSnapshot scenario)
    {
        var profile = scenario.PositionScarcity ?? _scarcity.BuildProfile(scenario);
        return profile.Summary + Environment.NewLine + string.Join(Environment.NewLine, profile.Positions.Select(position => $"- {PositionScarcityService.Label(position.Position)}: {position.ScarcityLevel} ({position.ScarcityScore}/100). {position.Summary}"));
    }

    public string BuildAssetValueText(NewGmScenarioSnapshot scenario, string personId)
    {
        var value = scenario.PlayerAssetValues.FirstOrDefault(item => item.PersonId == personId)
            ?? BuildPlayerValue(scenario, personId, scenario.Organization.OrganizationId, scenario.Organization.Name);
        return $"{value.PlayerName}\n"
            + $"Current Value: {value.Current.Band} ({value.Current.Score}) - {value.Current.Summary}\n"
            + $"Future Value: {value.Future.Band} ({value.Future.Score}) - {value.Future.Summary}\n"
            + $"Contract Value: {value.Contract.Band} ({value.Contract.Score}) - {value.Contract.Summary}\n"
            + $"Trade Value: {value.Trade.Band} ({value.Trade.Score}) - {value.Trade.Summary}\n"
            + $"Organization Fit: {value.Organizational.Band} ({value.Organizational.Score}) - {value.Organizational.Summary}\n"
            + $"Position Market: {PositionScarcityService.Label(value.Market.MarketPosition)} {value.Market.ScarcityLevel} - {value.Market.Summary}\n"
            + $"Reasons: {string.Join(" ", value.Reasons)}";
    }

    private PlayerValueContext PlayerContext(NewGmScenarioSnapshot scenario, string personId)
    {
        var rating = scenario.PlayerRatings.FirstOrDefault(item => item.PersonId == personId);
        var scouted = scenario.ScoutedRatings.FirstOrDefault(item => item.PersonId == personId);
        var roster = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        var prospect = scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == personId);
        var freeAgent = scenario.FreeAgentMarket?.FreeAgents.FirstOrDefault(item => item.PersonId == personId);
        var trade = scenario.TradeBlock?.Find(personId);
        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == personId);
        var position = rating?.Position ?? roster?.Position ?? prospect?.Position ?? freeAgent?.Position ?? trade?.Position ?? draft?.Bio?.Position ?? RosterPosition.Unknown;
        var name = rating?.PlayerName ?? prospect?.ProspectName ?? freeAgent?.Name ?? trade?.Name ?? PersonName(scenario, personId);
        var age = rating?.Age ?? roster?.Age ?? prospect?.Age ?? freeAgent?.Age ?? trade?.Age ?? (draft?.Bio is null ? (int?)null : scenario.CurrentDate.Year - draft.Bio.BirthYear) ?? PersonAge(scenario, personId);
        var overall = rating?.Overall.Midpoint ?? scouted?.Overall.Midpoint ?? EstimateOverall(scenario, personId, position, age);
        var potential = rating?.Potential.Midpoint ?? scouted?.Potential.Midpoint ?? Math.Clamp(overall + (age <= 20 ? 12 : 4), overall, 98);
        var confidence = rating?.Confidence ?? ConfidenceFrom(scouted?.ConfidenceColor);
        var curve = scenario.DevelopmentCurves.FirstOrDefault(item => item.PersonId == personId);
        var stat = scenario.CareerStatSummaries.FirstOrDefault(item => item.PersonId == personId);
        var marketPosition = draft is not null
            ? PositionScarcityService.MarketPositionFor(draft)
            : PositionScarcityService.MarketPositionFor(position, freeAgent?.ShootsCatches, personId);
        var activeInjury = scenario.AlphaSnapshot.Injuries.FirstOrDefault(injury => injury.PersonId == personId && injury.IsActive);
        var currentSummary = $"{position} age {age}, visible OVR {overall}, role {rating?.RoleLabel ?? trade?.CurrentRole ?? prospect?.Status.ToString() ?? "tracked player"}; production {stat?.Points ?? 0} career points; health {(activeInjury is null ? "available" : activeInjury.Severity.ToString())}.";
        var futureSummary = $"Visible POT {potential}, confidence {confidence}, curve {curve?.CurveType.ToString() ?? "unknown"}; scouting confidence affects how certain this value is.";
        return new PlayerValueContext(personId, name, position, marketPosition, age, overall, potential, confidence, stat?.Points ?? 0, activeInjury is not null, curve?.CurveType.ToString() ?? "Unknown", prospect is not null || draft is not null || age <= 20, currentSummary, futureSummary);
    }

    private static int CurrentScore(NewGmScenarioSnapshot scenario, PlayerValueContext context)
    {
        var production = Math.Clamp(context.CareerPoints / 18, 0, 12);
        var ageAdjust = context.Age switch
        {
            <= 18 => -6,
            <= 22 => 2,
            <= 29 => 6,
            >= 35 => -8,
            _ => 0
        };
        var health = context.IsInjured ? -10 : 2;
        return Math.Clamp(context.Overall + production + ageAdjust + health, 0, 100);
    }

    private static int FutureScore(NewGmScenarioSnapshot scenario, PlayerValueContext context)
    {
        var ageUpside = context.Age switch
        {
            <= 18 => 12,
            <= 21 => 8,
            <= 24 => 3,
            >= 31 => -12,
            _ => -2
        };
        var confidencePenalty = context.Confidence is PlayerRatingConfidence.Low or PlayerRatingConfidence.Unknown ? -3 : 1;
        var curveBonus = context.Curve.Contains("Late", StringComparison.OrdinalIgnoreCase) || context.Curve.Contains("Slow", StringComparison.OrdinalIgnoreCase) ? 2
            : context.Curve.Contains("Boom", StringComparison.OrdinalIgnoreCase) ? 4
            : 0;
        var injury = context.IsInjured ? -7 : 0;
        return Math.Clamp(context.Potential + ageUpside + confidencePenalty + curveBonus + injury, 0, 100);
    }

    private static int ContractScore(NewGmScenarioSnapshot scenario, string personId, PlayerValueContext context)
    {
        var contract = scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.PersonId == personId && contract.Status == ContractStatus.Signed)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .FirstOrDefault();
        if (contract is null)
        {
            return context.IsProspect ? 58 : 42;
        }

        var salary = contract.Money.SalaryOrStipend;
        var years = Math.Max(1, contract.Term.EndDate.Year - scenario.CurrentDate.Year);
        var level = scenario.LeagueProfile.Experience;
        var expected = level switch
        {
            LeagueExperience.Nhl => context.Overall * 90_000m,
            LeagueExperience.Ahl => context.Overall * 2_500m,
            _ => context.Overall * 900m
        };
        var surplus = expected - salary;
        var score = 50 + (int)Math.Clamp(surplus / Math.Max(1m, expected) * 30m, -30m, 30m) + Math.Min(8, years);
        return Math.Clamp(score, -10, 100);
    }

    private static int OrganizationalScore(NewGmScenarioSnapshot scenario, string organizationId, PlayerValueContext context, int current, int future, PositionMarketContext market)
    {
        var profile = new TradeStrategyService().BuildTeamNeedsProfile(scenario, organizationId, organizationId == scenario.Organization.OrganizationId ? scenario.Organization.Name : organizationId);
        var directionBonus = profile.Direction switch
        {
            TeamDirection.Rebuilder or TeamDirection.ProspectBuild when context.IsProspect => 12,
            TeamDirection.WinNow when !context.IsProspect && current >= 70 => 10,
            TeamDirection.BudgetReset when context.Age <= 23 => 5,
            _ => 0
        };
        var needBonus = profile.Needs.Any(need => NeedMatches(need.Need, context.Position)) ? 10 : 0;
        return Math.Clamp((current + future) / 2 + directionBonus + needBonus + ScarcityBonus(market.ScarcityLevel), 0, 105);
    }

    private static bool NeedMatches(PositionNeed need, RosterPosition position) =>
        need switch
        {
            PositionNeed.StartingGoalie => position == RosterPosition.Goalie,
            PositionNeed.DefensiveDefenseman => position == RosterPosition.Defense,
            PositionNeed.TopSixForward => position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing,
            PositionNeed.ProspectDepth => true,
            _ => false
        };

    private static string ContractSummary(NewGmScenarioSnapshot scenario, string personId, int score)
    {
        var contract = scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.PersonId == personId && contract.Status == ContractStatus.Signed)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .FirstOrDefault();
        if (contract is null)
        {
            return score >= 55 ? "Rights or unsigned status creates flexibility but requires a future decision." : "No signed contract tracked; value depends on rights and signing risk.";
        }

        return $"{contract.Money.SalaryOrStipend:C0} through {contract.Term.EndDate:yyyy-MM-dd}; score reflects cost against expected role.";
    }

    private static string OrganizationFitReason(NewGmScenarioSnapshot scenario, string organizationId, PlayerValueContext context) =>
        organizationId == scenario.Organization.OrganizationId
            ? $"{context.Name} is valued against the player's roster, prospect pipeline, owner expectations, and current team direction."
            : $"{context.Name} is valued through the other club's needs, direction, and asset preferences.";

    private static int EstimateOverall(NewGmScenarioSnapshot scenario, string personId, RosterPosition position, int age)
    {
        var baseValue = scenario.LeagueProfile.Experience switch
        {
            LeagueExperience.Nhl => 74,
            LeagueExperience.Ahl => 64,
            _ => 56
        };
        var ageBonus = age is >= 22 and <= 30 ? 8 : age <= 18 ? -4 : 0;
        var positionBonus = position == RosterPosition.Goalie ? 2 : 0;
        return Math.Clamp(baseValue + ageBonus + positionBonus + StableHash(personId) % 8, 38, 92);
    }

    private static PlayerRatingConfidence ConfidenceFrom(PlayerRatingColor? color) =>
        color switch
        {
            PlayerRatingColor.Black => PlayerRatingConfidence.VeryHigh,
            PlayerRatingColor.Blue => PlayerRatingConfidence.High,
            PlayerRatingColor.Green => PlayerRatingConfidence.Medium,
            PlayerRatingColor.Red => PlayerRatingConfidence.Low,
            _ => PlayerRatingConfidence.Unknown
        };

    private static int ScarcityBonus(PositionScarcityLevel level) =>
        level switch
        {
            PositionScarcityLevel.Critical => 14,
            PositionScarcityLevel.Scarce => 9,
            PositionScarcityLevel.Thin => 5,
            PositionScarcityLevel.Oversupplied => -7,
            _ => 0
        };

    private static AssetValueBand Band(int score) =>
        score switch
        {
            < 10 => AssetValueBand.Negative,
            < 35 => AssetValueBand.Low,
            < 55 => AssetValueBand.Moderate,
            < 75 => AssetValueBand.Strong,
            < 90 => AssetValueBand.Premium,
            _ => AssetValueBand.Elite
        };

    private static string Ordinal(int number) =>
        number switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{number}th"
        };

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? scenario.FreeAgentMarket?.FreeAgents.FirstOrDefault(agent => agent.PersonId == personId)?.Name
        ?? scenario.TradeBlock?.Find(personId)?.Name
        ?? personId;

    private static int PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? (scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio is { } bio ? scenario.CurrentDate.Year - bio.BirthYear : (int?)null)
        ?? 18;

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = hash * 31 + ch;
            }

            return Math.Abs(hash);
        }
    }

    private sealed record PlayerValueContext(
        string PersonId,
        string Name,
        RosterPosition Position,
        PositionMarketPosition MarketPosition,
        int Age,
        int Overall,
        int Potential,
        PlayerRatingConfidence Confidence,
        int CareerPoints,
        bool IsInjured,
        string Curve,
        bool IsProspect,
        string CurrentSummary,
        string FutureSummary);
}
