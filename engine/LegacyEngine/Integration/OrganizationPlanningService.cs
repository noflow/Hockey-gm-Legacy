using LegacyEngine.Contracts;
using LegacyEngine.Draft;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class OrganizationPlanningService
{
    private readonly TradeStrategyService _tradeStrategy = new();
    private readonly OrganizationAiService _organizationAi = new();
    private readonly AssetEvaluationService _assetEvaluation = new();

    public NewGmScenarioSnapshot EnsurePlans(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = scenario.OrganizationAiProfiles.Count == 0
            ? _organizationAi.EnsureProfiles(scenario)
            : scenario;
        prepared = prepared.AssetEvaluations.Count == 0
            ? _assetEvaluation.EnsureEvaluations(prepared)
            : prepared;

        var plans = BuildLeaguePlans(prepared);
        var playerPlan = plans.First(plan => plan.OrganizationId == prepared.Organization.OrganizationId);
        var updated = prepared with
        {
            OrganizationPlans = plans,
            CurrentOrganizationPlan = playerPlan
        };
        updated.Validate();
        return updated;
    }

    public IReadOnlyList<OrganizationPlan> BuildLeaguePlans(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var teams = SeasonFrameworkService.LeagueTeams(scenario).ToArray();
        var plans = teams
            .Select(team => BuildPlan(scenario, team.OrganizationId, team.TeamName, PlanningHorizon.FiveYears))
            .ToArray();

        foreach (var plan in plans)
        {
            plan.Validate();
        }

        return plans;
    }

    public OrganizationPlan BuildPlan(
        NewGmScenarioSnapshot scenario,
        string organizationId,
        string organizationName,
        PlanningHorizon horizon = PlanningHorizon.FiveYears)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var isPlayerTeam = organizationId == scenario.Organization.OrganizationId;
        var ai = scenario.OrganizationAiProfiles.FirstOrDefault(profile => profile.OrganizationId == organizationId)
            ?? _organizationAi.BuildProfile(
                scenario,
                new LeagueAiService().BuildOrganizationProfile(scenario, organizationId, organizationName));
        var needs = _tradeStrategy.BuildTeamNeedsProfile(scenario, organizationId, organizationName);
        var window = WindowFor(ai.Strategy.Phase, scenario, organizationId);
        var roster = isPlayerTeam
            ? BuildPlayerRosterPlan(scenario, needs, window)
            : BuildSyntheticRosterPlan(scenario, ai, needs, window);
        var prospects = isPlayerTeam
            ? BuildPlayerProspectPlan(scenario)
            : BuildSyntheticProspectPlan(scenario, ai, organizationId, organizationName);
        var depth = isPlayerTeam
            ? BuildPlayerDepthPlan(scenario, prospects)
            : BuildSyntheticDepthPlan(scenario, ai, organizationId);
        var contracts = isPlayerTeam
            ? BuildPlayerContractPlan(scenario)
            : BuildSyntheticContractPlan(scenario, ai);
        var freeAgency = BuildFreeAgencyTargets(needs, window, contracts);
        var trades = BuildTradeTargets(needs, window, prospects, roster);
        var reports = BuildReports(window, roster, prospects, depth, contracts, freeAgency, trades);
        var summary = $"{organizationName} is planning as {Readable(window).ToLowerInvariant()} over a {Readable(horizon).ToLowerInvariant()} horizon. {roster.Summary}";

        var plan = new OrganizationPlan(
            organizationId,
            organizationName,
            horizon,
            window,
            roster,
            prospects,
            depth,
            contracts,
            freeAgency,
            trades,
            reports,
            scenario.CurrentDate,
            summary);
        plan.Validate();
        return plan;
    }

    public string BuildPlanningReport(NewGmScenarioSnapshot scenario, string? organizationId = null)
    {
        var prepared = scenario.CurrentOrganizationPlan is null && scenario.OrganizationPlans.Count == 0
            ? EnsurePlans(scenario)
            : scenario;
        var plan = organizationId is null
            ? prepared.CurrentOrganizationPlan ?? prepared.OrganizationPlans.First(plan => plan.OrganizationId == prepared.Organization.OrganizationId)
            : prepared.OrganizationPlans.FirstOrDefault(plan => plan.OrganizationId == organizationId)
                ?? BuildPlan(prepared, organizationId, TeamName(prepared, organizationId));

        var lines = new List<string>
        {
            $"Organization Planning Report - {plan.OrganizationName}",
            $"Updated: {plan.LastUpdated:yyyy-MM-dd}",
            $"Window: {Readable(plan.Window)}",
            $"Horizon: {Readable(plan.Horizon)}",
            string.Empty,
            "Top Needs:",
        };
        lines.AddRange(plan.RosterPlan.FutureNeeds.Take(6).Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("Future Depth:");
        lines.AddRange(plan.DepthPlan.FutureDepth.Take(12).Select(slot => $"- {slot.Year} {slot.Slot}: {slot.PlayerName} ({slot.Position}) - {slot.Summary}"));
        lines.Add(string.Empty);
        lines.Add("Prospect Pipeline:");
        lines.AddRange(plan.ProspectPlan.Prospects.Take(8).Select(path => $"- {path.PlayerName}: {string.Join(" -> ", path.Path)} | ETA {path.ExpectedArrivalYear} | {path.Recommendation}"));
        lines.Add(string.Empty);
        lines.Add("Contracts:");
        lines.Add(plan.ContractPlan.Summary);
        lines.AddRange(plan.ContractPlan.ExpiringContracts.Take(6).Select(item => $"- {item.PlayerName}: expires {item.ExpiryYear}, {item.Recommendation}"));
        lines.Add(string.Empty);
        lines.Add("Free Agency Plan:");
        lines.AddRange(plan.FreeAgencyTargets.DefaultIfEmpty("No external signing pressure right now.").Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("Trade Plan:");
        lines.AddRange(plan.TradeTargets.DefaultIfEmpty("No urgent trade path right now.").Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("Planning Notes:");
        lines.AddRange(plan.Reports.Select(item => $"- {item}"));
        return string.Join(Environment.NewLine, lines);
    }

    private RosterPlan BuildPlayerRosterPlan(NewGmScenarioSnapshot scenario, TeamNeedsProfile needs, CompetitiveWindow window)
    {
        var active = scenario.AlphaSnapshot.Roster.ActivePlayers.ToArray();
        var currentNeeds = needs.Needs.Select(NeedText).ToArray();
        var futureNeeds = new List<string>(currentNeeds);
        var succession = new List<string>();
        var promotions = new List<string>();
        var blocked = new List<string>();
        var prospects = ProspectsForPlanning(scenario);
        var olderPlayers = active.Where(player => (player.Age ?? 0) >= 32).OrderByDescending(player => player.Age).Take(5).ToArray();
        foreach (var player in olderPlayers)
        {
            var replacement = BestProspectForPosition(scenario, player.Position);
            succession.Add(replacement is null
                ? $"{PositionText(player.Position)} succession risk: {PersonName(scenario, player.PersonId)} is {player.Age}; no clear internal replacement."
                : $"{PersonName(scenario, player.PersonId)} ({player.Age}) can be succeeded by {replacement.ProspectName} around {scenario.Season.Year + ArrivalYears(replacement.Age)}.");
        }

        foreach (var prospect in prospects.OrderBy(prospect => ArrivalYears(prospect.Age)).Take(5))
        {
            promotions.Add($"{prospect.ProspectName}: projected {PositionText(prospect.Position)} call-up path around {scenario.Season.Year + ArrivalYears(prospect.Age)}.");
            var veterans = active.Count(player => SamePositionGroup(player.Position, prospect.Position) && (player.Age ?? 0) >= 28);
            if (veterans >= 3)
            {
                blocked.Add($"{prospect.ProspectName} is blocked by {veterans} veteran(s) at {PositionText(prospect.Position)}.");
            }
        }

        if (futureNeeds.Count == 0)
        {
            futureNeeds.Add(window is CompetitiveWindow.Rebuild or CompetitiveWindow.Developing
                ? "Keep accumulating draft picks and patient development paths."
                : "Maintain veteran leadership while protecting prospect runway.");
        }

        var summary = $"Roster plan tracks {active.Length} active player(s), {prospects.Length} prospect planning record(s), and a {Readable(window).ToLowerInvariant()} window.";
        return new RosterPlan(currentNeeds, futureNeeds.Distinct(StringComparer.Ordinal).ToArray(), succession, promotions, blocked, summary);
    }

    private static RosterPlan BuildSyntheticRosterPlan(NewGmScenarioSnapshot scenario, OrganizationAiProfile ai, TeamNeedsProfile needs, CompetitiveWindow window)
    {
        var current = needs.Needs.Select(NeedText).ToArray();
        var future = current.Concat(ai.CurrentNeeds.Take(3).Select(need => $"{Readable(need.NeedType)}: {need.Reason}")).Distinct(StringComparer.Ordinal).ToArray();
        var succession = new[] { $"{ai.TeamName} has a synthetic succession review tied to {Readable(ai.Strategy.Phase).ToLowerInvariant()} strategy." };
        var promotions = new[] { "AI will promote prospects when projected role and window align." };
        var blocked = window is CompetitiveWindow.Contending or CompetitiveWindow.AllIn
            ? new[] { "Contender veteran depth may block at least one prospect unless a trade clears space." }
            : Array.Empty<string>();
        return new RosterPlan(current, future, succession, promotions, blocked, $"{ai.TeamName} roster plan follows {ai.Strategy.Summary}");
    }

    private ProspectPlan BuildPlayerProspectPlan(NewGmScenarioSnapshot scenario)
    {
        var paths = ProspectsForPlanning(scenario)
            .OrderBy(prospect => prospect.PickNumber)
            .Take(24)
            .Select(prospect => BuildPath(scenario, prospect))
            .ToArray();
        var strengths = paths.GroupBy(path => path.Position).OrderByDescending(group => group.Count()).Take(3)
            .Select(group => $"{PositionText(group.Key)} depth: {group.Count()} notable prospect(s).")
            .ToArray();
        var risks = new List<string>();
        if (paths.Count(path => path.Position == RosterPosition.Goalie) == 0)
        {
            risks.Add("No clear goalie prospect in the tracked rights list.");
        }

        if (paths.Count(path => path.IsBlocked) > 0)
        {
            risks.Add($"{paths.Count(path => path.IsBlocked)} prospect(s) may be blocked by current roster veterans.");
        }

        var summary = paths.Length == 0
            ? "Prospect plan has no tracked rights yet."
            : $"Prospect plan covers {paths.Length} prospect(s), led by {paths[0].PlayerName}.";
        return new ProspectPlan(paths, strengths, risks, summary);
    }

    private ProspectPlan BuildSyntheticProspectPlan(NewGmScenarioSnapshot scenario, OrganizationAiProfile ai, string organizationId, string organizationName)
    {
        var seed = StableHash($"{organizationId}:{scenario.Season.Year}:prospects");
        var positions = new[] { RosterPosition.Center, RosterPosition.Defense, RosterPosition.Goalie, RosterPosition.LeftWing };
        var paths = Enumerable.Range(0, 4)
            .Select(index =>
            {
                var position = positions[(seed + index) % positions.Length];
                var name = $"{organizationName} Prospect {index + 1}";
                return new ProspectDevelopmentPath(
                    $"synthetic-prospect:{organizationId}:{index}",
                    name,
                    position,
                    18 + index,
                    scenario.Season.Year + 2 + index,
                    PathFor(position, 18 + index),
                    ProjectedRole(position, 18 + index),
                    $"Develop toward {ProjectedRole(position, 18 + index).ToLowerInvariant()} without rushing.",
                    false,
                    "No direct blocking signal in synthetic AI plan.");
            })
            .ToArray();
        return new ProspectPlan(
            paths,
            new[] { $"{ai.TeamName} maintains a synthetic prospect runway for {Readable(ai.Strategy.Phase).ToLowerInvariant()} planning." },
            Array.Empty<string>(),
            $"{ai.TeamName} synthetic pipeline has {paths.Length} tracked planning prospects.");
    }

    private DepthPlan BuildPlayerDepthPlan(NewGmScenarioSnapshot scenario, ProspectPlan prospectPlan)
    {
        var current = scenario.AlphaSnapshot.Roster.ActivePlayers
            .Where(player => player.Status == RosterStatus.Active)
            .OrderBy(player => PositionOrder(player.Position))
            .ThenByDescending(player => CurrentScore(scenario, player.PersonId))
            .Take(23)
            .Select((player, index) => new DepthChartSlot(
                SlotFor(player.Position, index),
                player.PersonId,
                PersonName(scenario, player.PersonId),
                player.Position,
                CurrentRole(scenario, player.PersonId),
                player.Age,
                scenario.Season.Year,
                "Current roster",
                $"{CurrentRole(scenario, player.PersonId)} with current score {CurrentScore(scenario, player.PersonId)}."))
            .ToArray();

        var future = prospectPlan.Prospects
            .OrderBy(path => path.ExpectedArrivalYear)
            .Take(14)
            .Select((path, index) => new DepthChartSlot(
                SlotFor(path.Position, index),
                path.PersonId,
                path.PlayerName,
                path.Position,
                path.ProjectedRole,
                path.Age,
                path.ExpectedArrivalYear,
                "Prospect pipeline",
                $"{path.ProjectedRole}; {path.Recommendation}"))
            .ToArray();
        var weaknesses = MissingDepthGroups(current, future).ToArray();
        return new DepthPlan(current, future, weaknesses, $"Depth chart includes {current.Length} current slot(s) and {future.Length} future slot(s).");
    }

    private static DepthPlan BuildSyntheticDepthPlan(NewGmScenarioSnapshot scenario, OrganizationAiProfile ai, string organizationId)
    {
        var positions = new[] { RosterPosition.Center, RosterPosition.LeftWing, RosterPosition.Defense, RosterPosition.Goalie };
        var future = positions.Select((position, index) => new DepthChartSlot(
            SlotFor(position, index),
            $"synthetic-depth:{organizationId}:{index}",
            $"{ai.TeamName} {PositionText(position)} Plan",
            position,
            ProjectedRole(position, 20 + index),
            20 + index,
            scenario.Season.Year + 1 + index,
            "AI organization plan",
            $"Future slot aligned to {Readable(ai.Strategy.Phase).ToLowerInvariant()} strategy."))
            .ToArray();
        return new DepthPlan(future, future, Array.Empty<string>(), $"{ai.TeamName} synthetic depth plan projects {future.Length} future slot(s).");
    }

    private ContractPlan BuildPlayerContractPlan(NewGmScenarioSnapshot scenario)
    {
        var playerContracts = scenario.Contracts
            .Where(contract => contract.OrganizationId == scenario.Organization.OrganizationId && contract.Status == ContractStatus.Signed)
            .OrderBy(contract => contract.Term.EndDate)
            .ToArray();
        var expiring = playerContracts
            .Where(contract => contract.Term.EndDate.Year <= scenario.Season.Year + 1)
            .Take(12)
            .Select(contract => ContractItem(scenario, contract))
            .ToArray();
        var extensionTargets = playerContracts
            .OrderByDescending(contract => CurrentScore(scenario, contract.PersonId) + FutureScore(scenario, contract.PersonId))
            .Take(6)
            .Select(contract => ContractItem(scenario, contract))
            .ToArray();
        var current = playerContracts.Sum(contract => contract.Money.SalaryOrStipend);
        var future = playerContracts.Where(contract => contract.Term.EndDate.Year > scenario.Season.Year).Sum(contract => contract.Money.SalaryOrStipend);
        var cap = new SalaryCapService().BuildSnapshot(scenario, scenario.LeagueProfile.Rulebook);
        var summary = $"Contract plan tracks {playerContracts.Length} signed deal(s), {expiring.Length} near expiry, and {future:C0} in future commitments.";
        return new ContractPlan(expiring, extensionTargets, current, future, $"Cap/budget: {cap.Status}, remaining {cap.CapRemaining:C0}, future commitments {cap.CommittedFutureSalary:C0}.", summary);
    }

    private static ContractPlan BuildSyntheticContractPlan(NewGmScenarioSnapshot scenario, OrganizationAiProfile ai)
    {
        var future = ai.Strategy.Phase is OrganizationStrategyPhase.AllIn or OrganizationStrategyPhase.Contending ? 6_000_000m : 2_500_000m;
        return new ContractPlan(
            Array.Empty<ContractPlanningItem>(),
            Array.Empty<ContractPlanningItem>(),
            future / 2m,
            future,
            $"{ai.TeamName} synthetic cap plan follows {ai.Strategy.BudgetBehavior}",
            $"{ai.TeamName} contract planning favors {ai.Strategy.FreeAgencyBehavior.ToLowerInvariant()}");
    }

    private static IReadOnlyList<string> BuildFreeAgencyTargets(TeamNeedsProfile needs, CompetitiveWindow window, ContractPlan contracts)
    {
        var targets = needs.Needs
            .Where(need => need.Need is PositionNeed.StartingGoalie or PositionNeed.DefensiveDefenseman or PositionNeed.TopSixForward or PositionNeed.VeteranLeadership or PositionNeed.BudgetRelief)
            .Select(need => need.Need switch
            {
                PositionNeed.StartingGoalie => "Target goalie support only if internal depth lacks starter path.",
                PositionNeed.DefensiveDefenseman => "Seek short-term defensive defenseman with clean contract.",
                PositionNeed.TopSixForward => "Add scoring winger/center if cost does not block prospects.",
                PositionNeed.VeteranLeadership => "Add one-year veteran leadership for room balance.",
                PositionNeed.BudgetRelief => "Avoid expensive term; prefer low-cost depth or internal promotion.",
                _ => NeedText(need)
            })
            .ToList();
        if (window is CompetitiveWindow.Rebuild or CompetitiveWindow.Developing)
        {
            targets.Add("Prioritize flexible short-term signings that protect development minutes.");
        }

        if (contracts.FutureCommittedSalary > contracts.CurrentCommittedSalary)
        {
            targets.Add("Future commitments are rising; free agency should be selective.");
        }

        return targets.Distinct(StringComparer.Ordinal).Take(6).ToArray();
    }

    private static IReadOnlyList<string> BuildTradeTargets(TeamNeedsProfile needs, CompetitiveWindow window, ProspectPlan prospects, RosterPlan roster)
    {
        var targets = new List<string>();
        if (window is CompetitiveWindow.Rebuild or CompetitiveWindow.Developing)
        {
            targets.Add("Shop expendable veterans for picks or prospect rights when value is strong.");
        }
        else if (window is CompetitiveWindow.Contending or CompetitiveWindow.AllIn)
        {
            targets.Add("Use surplus picks/prospects only for clear lineup upgrades.");
        }

        targets.AddRange(needs.Needs.Take(3).Select(need => $"Trade market watch: {NeedText(need)}"));
        targets.AddRange(roster.BlockedProspects.Take(2).Select(item => $"Resolve blockage: {item}"));
        if (prospects.PipelineRisks.Any(risk => risk.Contains("goalie", StringComparison.OrdinalIgnoreCase)))
        {
            targets.Add("Explore goalie prospect acquisition before pipeline thins further.");
        }

        return targets.Distinct(StringComparer.Ordinal).Take(8).ToArray();
    }

    private static IReadOnlyList<string> BuildReports(
        CompetitiveWindow window,
        RosterPlan roster,
        ProspectPlan prospects,
        DepthPlan depth,
        ContractPlan contracts,
        IReadOnlyList<string> freeAgency,
        IReadOnlyList<string> trades)
    {
        return new[]
        {
            $"Championship Window: {Readable(window)}.",
            $"Prospect Pipeline: {prospects.Summary}",
            $"Future Lineup: {depth.Summary}",
            $"Future Contracts: {contracts.Summary}",
            $"Top Needs: {string.Join("; ", roster.FutureNeeds.Take(3))}",
            $"Free Agency Planning: {freeAgency.FirstOrDefault() ?? "No urgent signing pressure."}",
            $"Trade Planning: {trades.FirstOrDefault() ?? "No urgent trade pressure."}"
        };
    }

    private ProspectDevelopmentPath BuildPath(NewGmScenarioSnapshot scenario, DraftRightsRecord prospect)
    {
        var arrivalYears = ArrivalYears(prospect.Age);
        var projectedRole = ProjectedRole(prospect.Position, prospect.Age);
        var path = PathFor(prospect.Position, prospect.Age);
        var blocked = scenario.AlphaSnapshot.Roster.ActivePlayers.Count(player => SamePositionGroup(player.Position, prospect.Position) && (player.Age ?? 0) >= 28) >= 3;
        var recommendation = blocked
            ? $"Delay promotion or clear a veteran path before asking for {projectedRole.ToLowerInvariant()} minutes."
            : $"Keep on {path.First()} path until ready for {projectedRole.ToLowerInvariant()} usage.";
        var blocking = blocked
            ? $"Veteran depth currently blocks {PositionText(prospect.Position)} minutes."
            : "No major veteran blockage detected.";
        return new ProspectDevelopmentPath(
            prospect.ProspectPersonId,
            prospect.ProspectName,
            prospect.Position,
            prospect.Age,
            scenario.Season.Year + arrivalYears,
            path,
            projectedRole,
            recommendation,
            blocked,
            blocking);
    }

    private ContractPlanningItem ContractItem(NewGmScenarioSnapshot scenario, Contract contract)
    {
        var score = CurrentScore(scenario, contract.PersonId) + FutureScore(scenario, contract.PersonId);
        var recommendation = score >= 145
            ? "Prioritize extension; player supports long-term plan."
            : contract.Term.EndDate.Year <= scenario.Season.Year + 1
                ? "Review before walk-away deadline."
                : "Monitor value versus future cap/budget pressure.";
        var risk = contract.Money.SalaryOrStipend > 4_000_000m
            ? "High salary commitment; ensure role justifies term."
            : "Moderate contract risk.";
        return new ContractPlanningItem(
            contract.PersonId,
            PersonName(scenario, contract.PersonId),
            contract.Status.ToString(),
            contract.Term.EndDate.Year,
            contract.Money.SalaryOrStipend,
            recommendation,
            risk);
    }

    private CompetitiveWindow WindowFor(OrganizationStrategyPhase phase, NewGmScenarioSnapshot scenario, string organizationId)
    {
        if (phase == OrganizationStrategyPhase.AllIn)
        {
            return CompetitiveWindow.AllIn;
        }

        if (phase == OrganizationStrategyPhase.Contending)
        {
            return CompetitiveWindow.Contending;
        }

        if (phase == OrganizationStrategyPhase.Competing)
        {
            return CompetitiveWindow.Competing;
        }

        if (phase is OrganizationStrategyPhase.Rebuilding or OrganizationStrategyPhase.BudgetReset)
        {
            return CompetitiveWindow.Rebuild;
        }

        var standing = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == organizationId);
        if (standing is not null && standing.GamesPlayed >= 20 && standing.Wins < standing.Losses / 2)
        {
            return CompetitiveWindow.Declining;
        }

        return CompetitiveWindow.Developing;
    }

    private int CurrentScore(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerAssetValues.FirstOrDefault(value => value.PersonId == personId)?.Current.Score
        ?? scenario.PlayerRatings.FirstOrDefault(value => value.PersonId == personId)?.Overall.Low
        ?? 55;

    private int FutureScore(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerAssetValues.FirstOrDefault(value => value.PersonId == personId)?.Future.Score
        ?? scenario.PlayerRatings.FirstOrDefault(value => value.PersonId == personId)?.Potential.Low
        ?? 55;

    private string CurrentRole(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.CurrentLineup?.Assignments.FirstOrDefault(item => item.PersonId == personId)?.CurrentRole.ToString()
        ?? scenario.PlayerAssetValues.FirstOrDefault(value => value.PersonId == personId)?.Organizational.Band.ToString()
        ?? "Depth";

    private static IEnumerable<string> MissingDepthGroups(IReadOnlyList<DepthChartSlot> current, IReadOnlyList<DepthChartSlot> future)
    {
        var all = current.Concat(future).ToArray();
        if (all.Count(slot => slot.Position == RosterPosition.Goalie) < 2)
        {
            yield return "Goalie depth is thin across current and future chart.";
        }

        if (all.Count(slot => slot.Position == RosterPosition.Defense) < 6)
        {
            yield return "Defense pipeline needs more volume.";
        }

        if (all.Count(slot => slot.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing) < 12)
        {
            yield return "Forward depth needs more scoring and role variety.";
        }
    }

    private static DraftRightsRecord? BestProspectForPosition(NewGmScenarioSnapshot scenario, RosterPosition position) =>
        ProspectsForPlanning(scenario)
            .Where(prospect => SamePositionGroup(prospect.Position, position))
            .OrderBy(prospect => prospect.PickNumber)
            .FirstOrDefault();

    private static DraftRightsRecord[] ProspectsForPlanning(NewGmScenarioSnapshot scenario)
    {
        if (scenario.ProspectRights.Count > 0)
        {
            return scenario.ProspectRights.ToArray();
        }

        return scenario.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Take(18)
            .Select(ToPlanningProspect)
            .ToArray();

        DraftRightsRecord ToPlanningProspect(DraftBoardEntry entry)
        {
            var bio = entry.Bio;
            var position = bio?.Position ?? RosterPosition.Unknown;
            var age = bio is null ? 18 : Math.Max(15, scenario.CurrentDate.Year - bio.BirthYear);
            var name = PersonName(scenario, entry.ProspectPersonId);
            return new DraftRightsRecord(
                entry.ProspectPersonId,
                name,
                age,
                position,
                Math.Max(1, ((entry.Rank - 1) / 32) + 1),
                entry.Rank,
                ProspectStatus.DraftRightsHeld,
                entry.ProjectionText,
                entry.ScoutingConfidence ?? ScoutingConfidenceLevel.Low,
                entry.PersonalNotes,
                PlayerDevelopmentLevel.Junior,
                bio?.CurrentTeam ?? "",
                bio?.League ?? "",
                bio?.League.Contains("CHL", StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    private static IReadOnlyList<DevelopmentPathStep> PathFor(RosterPosition position, int age)
    {
        var path = new List<DevelopmentPathStep> { age <= 18 ? DevelopmentPathStep.Junior : DevelopmentPathStep.Ahl };
        if (age <= 18)
        {
            path.Add(DevelopmentPathStep.Ahl);
        }

        path.Add(DevelopmentPathStep.Nhl);
        path.Add(position switch
        {
            RosterPosition.Goalie => DevelopmentPathStep.StarterGoalie,
            RosterPosition.Defense => DevelopmentPathStep.TopPair,
            RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing => DevelopmentPathStep.TopSix,
            _ => DevelopmentPathStep.DepthRole
        });
        return path;
    }

    private static int ArrivalYears(int age) =>
        age switch
        {
            <= 17 => 4,
            18 => 3,
            19 => 2,
            <= 21 => 1,
            _ => 1
        };

    private static string ProjectedRole(RosterPosition position, int age) =>
        position switch
        {
            RosterPosition.Goalie => age <= 19 ? "starter goalie upside" : "tandem goalie path",
            RosterPosition.Defense => age <= 19 ? "top-pair defense upside" : "second-pair defense path",
            RosterPosition.Center => "top-six center path",
            RosterPosition.LeftWing or RosterPosition.RightWing => "scoring winger path",
            _ => "depth role path"
        };

    private static bool SamePositionGroup(RosterPosition left, RosterPosition right)
    {
        if (left == right)
        {
            return true;
        }

        return IsForward(left) && IsForward(right);
    }

    private static bool IsForward(RosterPosition position) =>
        position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing;

    private static int PositionOrder(RosterPosition position) =>
        position switch
        {
            RosterPosition.Center => 0,
            RosterPosition.LeftWing => 1,
            RosterPosition.RightWing => 2,
            RosterPosition.Defense => 3,
            RosterPosition.Goalie => 4,
            _ => 5
        };

    private static string SlotFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => index == 0 ? "Starter" : $"Goalie depth {index + 1}",
            RosterPosition.Defense => index < 2 ? $"Pair 1 D{index + 1}" : $"Defense depth {index + 1}",
            RosterPosition.Center => index < 4 ? $"Line {index + 1} C" : $"Center depth {index + 1}",
            RosterPosition.LeftWing => index < 4 ? $"Line {index + 1} LW" : $"LW depth {index + 1}",
            RosterPosition.RightWing => index < 4 ? $"Line {index + 1} RW" : $"RW depth {index + 1}",
            _ => $"Depth {index + 1}"
        };

    private static string NeedText(TeamNeed need) =>
        $"{Readable(need.Need)} ({need.Priority}): {need.Reason}";

    private static string TeamName(NewGmScenarioSnapshot scenario, string organizationId)
    {
        var team = SeasonFrameworkService.LeagueTeams(scenario).FirstOrDefault(team => team.OrganizationId == organizationId);
        return string.IsNullOrWhiteSpace(team.TeamName) ? scenario.Organization.Name : team.TeamName;
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? personId;

    private static string PositionText(RosterPosition position) =>
        position switch
        {
            RosterPosition.Center => "center",
            RosterPosition.LeftWing => "left wing",
            RosterPosition.RightWing => "right wing",
            RosterPosition.Defense => "defense",
            RosterPosition.Goalie => "goalie",
            _ => "unknown"
        };

    public static string Readable(object value)
    {
        var text = value.ToString() ?? string.Empty;
        return string.Concat(text.SelectMany((ch, index) => index > 0 && char.IsUpper(ch) ? new[] { ' ', ch } : new[] { ch }));
    }

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
