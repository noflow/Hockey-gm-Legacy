using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class GameSimulationService
{
    public GameSimulationContext CreateContext(NewGmScenarioSnapshot scenario, ScheduledGame game)
    {
        game.Validate();
        var context = new GameSimulationContext(
            game,
            BuildTeamProfile(scenario, game.HomeOrganizationId, true, game.GameId),
            BuildTeamProfile(scenario, game.AwayOrganizationId, false, game.GameId));
        context.Validate();
        return context;
    }

    public GameSimulationResultV2 Simulate(NewGmScenarioSnapshot scenario, ScheduledGame game) =>
        Simulate(CreateContext(scenario, game), scenario);

    public GameSimulationResultV2 Simulate(GameSimulationContext context, NewGmScenarioSnapshot scenario)
    {
        context.Validate();
        var seed = StableSeed(context.Game.GameId, context.Game.Date.DayNumber, context.HomeTeam.OrganizationId, context.AwayTeam.OrganizationId);
        var homeGoals = GoalsFor(context.HomeTeam, context.AwayTeam, true, seed);
        var awayGoals = GoalsFor(context.AwayTeam, context.HomeTeam, false, seed / 7);
        var overtime = false;

        if (homeGoals == awayGoals)
        {
            overtime = true;
            if ((seed + context.HomeTeam.GoaltendingScore + context.HomeTeam.ChemistryScore) >= (context.AwayTeam.GoaltendingScore + context.AwayTeam.ChemistryScore + 35))
            {
                homeGoals++;
            }
            else
            {
                awayGoals++;
            }
        }

        var winner = homeGoals > awayGoals ? context.HomeTeam.OrganizationId : context.AwayTeam.OrganizationId;
        var loser = homeGoals > awayGoals ? context.AwayTeam.OrganizationId : context.HomeTeam.OrganizationId;
        var result = new GameResult(context.Game.GameId, homeGoals, awayGoals, winner, loser, overtime);
        var homeChances = PowerPlayChances(context.HomeTeam, context.AwayTeam, seed);
        var awayChances = PowerPlayChances(context.AwayTeam, context.HomeTeam, seed / 11);
        var homePowerPlayGoals = PowerPlayGoals(homeGoals, context.HomeTeam, context.AwayTeam, homeChances, seed);
        var awayPowerPlayGoals = PowerPlayGoals(awayGoals, context.AwayTeam, context.HomeTeam, awayChances, seed / 13);
        var homeShots = ShotsFor(context.HomeTeam, context.AwayTeam, homeGoals, seed);
        var awayShots = ShotsFor(context.AwayTeam, context.HomeTeam, awayGoals, seed / 17);
        var playerTeam = PlayerTeamProfile(context, scenario.Organization.OrganizationId);
        var playerTeamGoals = playerTeam?.OrganizationId == context.HomeTeam.OrganizationId ? homeGoals : playerTeam?.OrganizationId == context.AwayTeam.OrganizationId ? awayGoals : 0;
        var playerTeamAgainst = playerTeam?.OrganizationId == context.HomeTeam.OrganizationId ? awayGoals : playerTeam?.OrganizationId == context.AwayTeam.OrganizationId ? homeGoals : 0;
        var playerTeamShotsAgainst = playerTeam?.OrganizationId == context.HomeTeam.OrganizationId ? awayShots : playerTeam?.OrganizationId == context.AwayTeam.OrganizationId ? homeShots : 0;
        var playerTeamPowerPlayGoals = playerTeam?.OrganizationId == context.HomeTeam.OrganizationId ? homePowerPlayGoals : playerTeam?.OrganizationId == context.AwayTeam.OrganizationId ? awayPowerPlayGoals : 0;
        var skaterStats = playerTeam is null
            ? Array.Empty<PlayerGameStatAllocation>()
            : AllocateSkaterStats(scenario, playerTeam, playerTeamGoals, playerTeamPowerPlayGoals, result.WinnerOrganizationId == playerTeam.OrganizationId, seed);
        var goalieStats = playerTeam is null
            ? null
            : AllocateGoalieStats(scenario, playerTeam, result.WinnerOrganizationId == playerTeam.OrganizationId, playerTeamAgainst, playerTeamShotsAgainst);
        var milestones = BuildMilestones(scenario, skaterStats, goalieStats, result, context.Game.Date).ToArray();
        var topLineSummary = TopLineSummary(playerTeam, skaterStats);
        var specialTeamsNote = $"{context.HomeTeam.TeamName} PP {homePowerPlayGoals}/{homeChances}; {context.AwayTeam.TeamName} PP {awayPowerPlayGoals}/{awayChances}. {SpecialTeamsSummary(context.HomeTeam, context.AwayTeam)}";
        var tacticalNote = $"{context.HomeTeam.TeamName}: {context.HomeTeam.TacticalProfile.Summary} {context.AwayTeam.TeamName}: {context.AwayTeam.TacticalProfile.Summary}";
        var chemistryNote = ChemistryNote(playerTeam);
        var goalieUsageNote = GoalieUsageNote(playerTeam, goalieStats);
        var injuryNote = InjuryNote(playerTeam);
        var developmentNote = DevelopmentNote(skaterStats);
        var keyConcern = KeyConcern(playerTeam, result, playerTeamAgainst);
        var narrative = BuildNarrative(context, result, skaterStats, goalieStats);

        var simulation = new GameSimulationResultV2(
            result,
            context,
            homeShots,
            awayShots,
            homePowerPlayGoals,
            homeChances,
            awayPowerPlayGoals,
            awayChances,
            skaterStats,
            goalieStats,
            milestones,
            topLineSummary,
            specialTeamsNote,
            tacticalNote,
            chemistryNote,
            goalieUsageNote,
            injuryNote,
            developmentNote,
            keyConcern,
            narrative);
        simulation.Validate();
        return simulation;
    }

    private static TeamSimulationProfile BuildTeamProfile(NewGmScenarioSnapshot scenario, string organizationId, bool isHome, string gameId)
    {
        if (organizationId != scenario.Organization.OrganizationId)
        {
            return BuildExternalProfile(scenario, organizationId, isHome, gameId);
        }

        var injuredIds = scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.IsActive)
            .Select(injury => injury.PersonId)
            .ToHashSet(StringComparer.Ordinal);
        var active = scenario.AlphaSnapshot.Roster.ActivePlayers
            .Where(player => !injuredIds.Contains(player.PersonId))
            .ToArray();
        var unavailable = scenario.AlphaSnapshot.Roster.ActivePlayers
            .Where(player => injuredIds.Contains(player.PersonId))
            .Select(player => player.PersonId)
            .ToArray();
        var lineup = scenario.CurrentLineup;
        var chemistry = scenario.CurrentLineChemistry;
        var usage = scenario.CurrentGameUsage;
        var tactics = scenario.CurrentTactics;
        var assignments = lineup?.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch && !injuredIds.Contains(assignment.PersonId))
            .ToArray()
            ?? active.Select((player, index) => FallbackAssignment(scenario, player, index)).ToArray();

        var forwardScore = AverageRoleScore(assignments.Where(IsForwardRole), active.Where(player => player.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing).Count());
        var defenseScore = AverageRoleScore(assignments.Where(IsDefenseRole), active.Count(player => player.Position == RosterPosition.Defense));
        var goalie = BuildGoalieProfile(scenario, active, lineup, usage);
        var specialTeams = BuildSpecialTeamsProfile(usage, tactics);
        var tactical = BuildTacticalProfile(tactics);
        var chemistryScore = chemistry?.Overall.Score.Value ?? 52;
        var coachingScore = CoachingScore(scenario);
        var offenseScore = ClampScore(forwardScore + tactical.GoalsForTendency + (specialTeams.PowerPlayScore - 55) / 5 + (chemistryScore - 50) / 8);
        var defenseScoreFinal = ClampScore(defenseScore - tactical.GoalsAgainstTendency + (specialTeams.PenaltyKillScore - 55) / 6 + (chemistryScore - 50) / 8);
        var specialTeamsScore = ClampScore((specialTeams.PowerPlayScore + specialTeams.PenaltyKillScore) / 2);
        var lines = BuildLineProfiles(lineup, chemistry, assignments).ToArray();
        var notes = new List<string>
        {
            $"{assignments.Length} skaters/goalies available from active lineup.",
            unavailable.Length == 0 ? "No active injury scratch in the simulation profile." : $"{unavailable.Length} injured player(s) excluded.",
            tactical.Summary,
            chemistry?.Overall.Recommendation ?? "Chemistry report is neutral."
        };

        return new TeamSimulationProfile(
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            isHome,
            Band(offenseScore),
            Band(defenseScoreFinal),
            goalie.Strength,
            Band(specialTeamsScore),
            Band(coachingScore),
            Band(chemistryScore),
            offenseScore,
            defenseScoreFinal,
            goalie.Score,
            specialTeamsScore,
            coachingScore,
            ClampScore(chemistryScore),
            active.Select(player => player.PersonId).ToArray(),
            unavailable,
            lines,
            goalie,
            specialTeams,
            tactical,
            notes);
    }

    private static TeamSimulationProfile BuildExternalProfile(NewGmScenarioSnapshot scenario, string organizationId, bool isHome, string gameId)
    {
        var teamName = SeasonFrameworkService.LeagueTeams(scenario).FirstOrDefault(team => team.OrganizationId == organizationId).TeamName;
        if (string.IsNullOrWhiteSpace(teamName))
        {
            teamName = organizationId;
        }

        var seed = StableSeed(gameId, organizationId, scenario.Season.Year);
        var baseScore = 48 + seed % 28;
        var offense = ClampScore(baseScore + seed % 9 - 4);
        var defense = ClampScore(baseScore + seed % 11 - 5);
        var goalieScore = ClampScore(baseScore + seed % 13 - 6);
        var specialScore = ClampScore(baseScore + seed % 7 - 3);
        var coaching = ClampScore(52 + seed % 20);
        var chemistry = ClampScore(48 + seed % 26);
        var specialTeams = new SpecialTeamsSimulationProfile(Band(specialScore), Band(specialScore), specialScore, specialScore, 3 + seed % 3, $"{teamName}'s special teams are estimated from league form.");
        var tactical = new TacticalSimulationProfile("League default", seed % 5 - 2, seed % 5 - 2, seed % 7 - 3, seed % 5 - 2, seed % 7 - 3, $"{teamName} uses a league-average tactical profile.");
        var goalie = new GoalieSimulationProfile(null, $"{teamName} starter", Band(goalieScore), goalieScore, seed % 30, false, $"{teamName}'s starter is estimated from public team form.");
        return new TeamSimulationProfile(
            organizationId,
            teamName,
            isHome,
            Band(offense),
            Band(defense),
            Band(goalieScore),
            Band(specialScore),
            Band(coaching),
            Band(chemistry),
            offense,
            defense,
            goalieScore,
            specialScore,
            coaching,
            chemistry,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { new LineSimulationProfile("Team profile", Array.Empty<string>(), 50, Band(baseScore), LineChemistryGrade.Neutral, $"{teamName} is estimated as {Band(baseScore)} overall.") },
            goalie,
            specialTeams,
            tactical,
            new[] { $"{teamName} profile uses league estimate because detailed roster usage is not loaded." });
    }

    private static LineupRoleAssignment FallbackAssignment(NewGmScenarioSnapshot scenario, RosterPlayer player, int index)
    {
        var name = PlayerName(scenario, player.PersonId);
        var role = player.Position switch
        {
            RosterPosition.Goalie => index == 0 ? LineupRole.StartingGoalie : LineupRole.BackupGoalie,
            RosterPosition.Defense => index < 2 ? LineupRole.TopPairDefenseman : index < 6 ? LineupRole.SecondPairDefenseman : LineupRole.ThirdPairDefenseman,
            _ => index < 3 ? LineupRole.FirstLineForward : index < 9 ? LineupRole.MiddleSixForward : LineupRole.CheckingLineForward
        };
        var slot = player.Position switch
        {
            RosterPosition.Goalie => index == 0 ? LineupSlot.Starter : LineupSlot.Backup,
            RosterPosition.Defense => index < 2 ? (index % 2 == 0 ? LineupSlot.Pair1LD : LineupSlot.Pair1RD) : LineupSlot.Pair3RD,
            _ => index % 3 == 0 ? LineupSlot.Line1LW : index % 3 == 1 ? LineupSlot.Line1C : LineupSlot.Line1RW
        };

        return new LineupRoleAssignment(player.PersonId, name, player.Position, "Unknown", player.Age, player.Position.ToString(), role, role, slot, null, "Signed", "Fallback lineup usage.");
    }

    private static IEnumerable<LineSimulationProfile> BuildLineProfiles(Lineup? lineup, LineChemistryReport? chemistry, IReadOnlyList<LineupRoleAssignment> assignments)
    {
        if (lineup is null)
        {
            yield return new LineSimulationProfile("Active roster", assignments.Select(item => item.PersonId).ToArray(), 50, Band(AverageRoleScore(assignments, assignments.Count)), LineChemistryGrade.Neutral, "Fallback active-roster unit.");
            yield break;
        }

        foreach (var line in lineup.ForwardLines.OrderBy(line => line.LineNumber))
        {
            var players = new[] { line.LeftWing, line.Center, line.RightWing }.Where(player => player is not null).Select(player => player!).ToArray();
            var unit = chemistry?.ForwardLines.FirstOrDefault(item => item.UnitId == $"forward-line:{line.LineNumber}");
            var score = AverageRoleScore(players, players.Length);
            yield return new LineSimulationProfile($"Forward line {line.LineNumber}", players.Select(player => player.PersonId).ToArray(), 90 - (line.LineNumber - 1) * 17, Band(score), unit?.Score.Grade ?? LineChemistryGrade.Neutral, unit?.Recommendation ?? $"Line {line.LineNumber} has a {Band(score)} usage profile.");
        }

        foreach (var pair in lineup.DefensePairs.OrderBy(pair => pair.PairNumber))
        {
            var players = new[] { pair.LeftDefense, pair.RightDefense }.Where(player => player is not null).Select(player => player!).ToArray();
            var unit = chemistry?.DefensePairs.FirstOrDefault(item => item.UnitId == $"defense-pair:{pair.PairNumber}");
            var score = AverageRoleScore(players, players.Length);
            yield return new LineSimulationProfile($"Defense pair {pair.PairNumber}", players.Select(player => player.PersonId).ToArray(), 82 - (pair.PairNumber - 1) * 18, Band(score), unit?.Score.Grade ?? LineChemistryGrade.Neutral, unit?.Recommendation ?? $"Pair {pair.PairNumber} has a {Band(score)} usage profile.");
        }
    }

    private static GoalieSimulationProfile BuildGoalieProfile(NewGmScenarioSnapshot scenario, IReadOnlyList<RosterPlayer> active, Lineup? lineup, GameUsage? usage)
    {
        var starter = lineup?.Goalies.Starter ?? active.Where(player => player.Position == RosterPosition.Goalie).Select((player, index) => FallbackAssignment(scenario, player, index)).FirstOrDefault();
        var usageProfile = usage?.GoalieUsage.FirstOrDefault(item => item.PersonId == starter?.PersonId);
        var roleScore = starter is null ? 45 : RoleScore(starter.CurrentRole);
        var fatigue = usageProfile?.Workload.Contains("over", StringComparison.OrdinalIgnoreCase) == true ? 76 : Math.Clamp(usageProfile?.GamesStarted * 4 ?? 24, 0, 100);
        var score = ClampScore(roleScore - Math.Max(0, fatigue - 70) / 2);
        var name = starter?.PlayerName ?? "No starter assigned";
        var overworked = fatigue >= 70;
        var summary = overworked
            ? $"{name} is carrying a heavy workload and may be volatile."
            : $"{name} gives the team a {Band(score)} goaltending profile.";
        return new GoalieSimulationProfile(starter?.PersonId, name, Band(score), score, fatigue, overworked, summary);
    }

    private static SpecialTeamsSimulationProfile BuildSpecialTeamsProfile(GameUsage? usage, TeamTactics? tactics)
    {
        var ppPlayers = usage?.SpecialTeams.PowerPlayUnits.SelectMany(unit => unit.Players).ToArray() ?? Array.Empty<LineupRoleAssignment>();
        var pkPlayers = usage?.SpecialTeams.PenaltyKillUnits.SelectMany(unit => unit.Players).ToArray() ?? Array.Empty<LineupRoleAssignment>();
        var pp = ClampScore(AverageRoleScore(ppPlayers, Math.Max(1, ppPlayers.Length)) + (tactics?.ModifierProfile.SpecialTeamsTendency ?? 0));
        var pk = ClampScore(AverageRoleScore(pkPlayers, Math.Max(1, pkPlayers.Length)) + (tactics?.ModifierProfile.SpecialTeamsTendency ?? 0));
        var chances = 3 + Math.Max(0, (tactics?.ModifierProfile.PaceTendency ?? 0) / 4);
        return new SpecialTeamsSimulationProfile(Band(pp), Band(pk), pp, pk, Math.Min(6, chances), $"Power play grades {Band(pp)}; penalty kill grades {Band(pk)}.");
    }

    private static TacticalSimulationProfile BuildTacticalProfile(TeamTactics? tactics)
    {
        if (tactics is null)
        {
            return new TacticalSimulationProfile("Balanced", 0, 0, 0, 0, 0, "Balanced default tactics with no major tilt.");
        }

        var modifier = tactics.ModifierProfile;
        var riskAgainst = tactics.RiskLevel == TacticalRiskLevel.High ? 3 : tactics.RiskLevel == TacticalRiskLevel.Low ? -2 : 0;
        var penalty = tactics.Settings.Physicality == TacticalIntensity.High ? 4 : tactics.Settings.Physicality == TacticalIntensity.Low ? -2 : 0;
        return new TacticalSimulationProfile(
            TacticsService.Display(tactics.Style),
            modifier.OffenseTendency,
            Math.Clamp(-modifier.DefenseTendency + riskAgainst, -20, 20),
            modifier.PaceTendency + modifier.OffenseTendency,
            penalty + modifier.RiskTendency / 2,
            modifier.PaceTendency,
            $"{TacticsService.Display(tactics.Style)} style nudges offense {modifier.OffenseTendency:+#;-#;0}, defense {-modifier.DefenseTendency + riskAgainst:+#;-#;0}, and pace {modifier.PaceTendency:+#;-#;0}.");
    }

    private static int GoalsFor(TeamSimulationProfile team, TeamSimulationProfile opponent, bool home, int seed)
    {
        var baseValue = 2 + seed % 3;
        var profileDelta = (team.OffenseScore - opponent.DefenseScore) / 14 + (team.SpecialTeamsScore - opponent.SpecialTeamsScore) / 24 + (team.ChemistryScore - 50) / 22 + team.TacticalProfile.GoalsForTendency / 5 - opponent.TacticalProfile.GoalsAgainstTendency / 7;
        var goalieDelta = (55 - opponent.GoaltendingScore) / 16;
        var homeBoost = home ? 1 : 0;
        var variance = seed % 5 == 0 ? 1 : seed % 11 == 0 ? -1 : 0;
        return Math.Clamp(baseValue + profileDelta + goalieDelta + homeBoost + variance, 0, 8);
    }

    private static int ShotsFor(TeamSimulationProfile team, TeamSimulationProfile opponent, int goals, int seed)
    {
        var shots = 25 + goals * 2 + (team.OffenseScore - 55) / 5 - (opponent.DefenseScore - 55) / 7 + team.TacticalProfile.ShotsTendency + seed % 8;
        return Math.Clamp(shots, 16, 48);
    }

    private static int PowerPlayChances(TeamSimulationProfile team, TeamSimulationProfile opponent, int seed)
    {
        var chances = team.SpecialTeamsProfile.ExpectedPowerPlayChances + opponent.TacticalProfile.PenaltyTendency / 3 + seed % 2;
        return Math.Clamp(chances, 1, 7);
    }

    private static int PowerPlayGoals(int goals, TeamSimulationProfile team, TeamSimulationProfile opponent, int chances, int seed)
    {
        if (goals == 0)
        {
            return 0;
        }

        var chanceScore = team.SpecialTeamsProfile.PowerPlayScore - opponent.SpecialTeamsProfile.PenaltyKillScore + seed % 30;
        var expected = chanceScore > 30 ? 2 : chanceScore > 8 ? 1 : 0;
        return Math.Clamp(Math.Min(goals, expected), 0, Math.Min(goals, chances));
    }

    private static IReadOnlyList<PlayerGameStatAllocation> AllocateSkaterStats(NewGmScenarioSnapshot scenario, TeamSimulationProfile team, int goals, int powerPlayGoals, bool won, int seed)
    {
        var assignments = scenario.CurrentLineup?.Assignments
            .Where(assignment => team.AvailablePlayerIds.Contains(assignment.PersonId, StringComparer.Ordinal) && !IsGoalieRole(assignment))
            .ToArray()
            ?? scenario.AlphaSnapshot.Roster.ActivePlayers
                .Where(player => player.Position != RosterPosition.Goalie && team.AvailablePlayerIds.Contains(player.PersonId, StringComparer.Ordinal))
                .Select((player, index) => FallbackAssignment(scenario, player, index))
                .ToArray();
        var ppIds = scenario.CurrentGameUsage?.SpecialTeams.PowerPlayUnits.SelectMany(unit => unit.Players).Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        var ordered = assignments
            .Select(assignment => (Assignment: assignment, Weight: OpportunityWeight(assignment, ppIds.Contains(assignment.PersonId))))
            .OrderByDescending(item => item.Weight)
            .ThenBy(item => item.Assignment.PlayerName, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
        {
            return Array.Empty<PlayerGameStatAllocation>();
        }

        var values = ordered.ToDictionary(item => item.Assignment.PersonId, item => new MutableSkater(item.Assignment, item.Weight), StringComparer.Ordinal);
        for (var goal = 0; goal < goals; goal++)
        {
            var scorer = ordered[(goal + seed) % Math.Min(ordered.Length, Math.Max(1, Math.Min(9, ordered.Length)))];
            values[scorer.Assignment.PersonId].Goals++;
            values[scorer.Assignment.PersonId].PlusMinus += won ? 1 : 0;
            var assistCount = ordered.Length > 2 ? 2 : Math.Max(0, ordered.Length - 1);
            for (var assist = 1; assist <= assistCount; assist++)
            {
                var assister = ordered[(goal + assist + seed / 3) % ordered.Length];
                if (assister.Assignment.PersonId != scorer.Assignment.PersonId)
                {
                    values[assister.Assignment.PersonId].Assists++;
                }
            }

            if (goal < powerPlayGoals)
            {
                values[scorer.Assignment.PersonId].IncludedPowerPlayPoint = true;
            }
        }

        return values.Values
            .Select(item => new PlayerGameStatAllocation(
                item.Assignment.PersonId,
                item.Assignment.PlayerName,
                item.Goals,
                item.Assists,
                item.PlusMinus,
                item.Weight < 35 ? seed % 2 : 0,
                item.IncludedPowerPlayPoint || (item.Assists > 0 && ppIds.Contains(item.Assignment.PersonId) && powerPlayGoals > 0),
                item.Weight,
                UsageNote(item.Assignment, item.Weight)))
            .Where(item => item.Goals > 0 || item.Assists > 0 || item.OpportunityWeight >= 45)
            .OrderByDescending(item => item.Points)
            .ThenByDescending(item => item.Goals)
            .ThenByDescending(item => item.OpportunityWeight)
            .ToArray();
    }

    private static GoalieGameStatAllocation? AllocateGoalieStats(NewGmScenarioSnapshot scenario, TeamSimulationProfile team, bool won, int goalsAgainst, int shotsAgainst)
    {
        var starterId = team.Goalie.StarterPersonId;
        if (string.IsNullOrWhiteSpace(starterId))
        {
            return null;
        }

        return new GoalieGameStatAllocation(
            starterId,
            PlayerName(scenario, starterId),
            won,
            Math.Max(0, shotsAgainst - goalsAgainst),
            goalsAgainst,
            team.Goalie.IsOverworked ? "Starter was flagged as overworked in the usage model." : "Starter handled the normal workload.");
    }

    private static IEnumerable<PlayerMilestone> BuildMilestones(NewGmScenarioSnapshot scenario, IReadOnlyList<PlayerGameStatAllocation> skaters, GoalieGameStatAllocation? goalie, GameResult result, DateOnly date)
    {
        foreach (var stat in skaters.Where(stat => stat.Goals > 0 || stat.Points > 0).Take(2))
        {
            var prior = scenario.PlayerStats.FirstOrDefault(line => line.PersonId == stat.PersonId);
            if (stat.Goals > 0 && (prior is null || prior.Goals == 0) && !scenario.PlayerMilestones.Any(item => item.PersonId == stat.PersonId && item.MilestoneType == PlayerMilestoneType.FirstNhlGoal))
            {
                yield return new PlayerMilestone($"milestone:game:first-goal:{stat.PersonId}:{date:yyyyMMdd}", stat.PersonId, stat.PlayerName, PlayerMilestoneType.FirstNhlGoal, date, scenario.Season.Year, $"{stat.PlayerName} scored a first tracked goal.", true);
            }
            else if (stat.Points > 0 && (prior is null || prior.Points == 0) && !scenario.PlayerMilestones.Any(item => item.PersonId == stat.PersonId && item.MilestoneType == PlayerMilestoneType.FirstNhlPoint))
            {
                yield return new PlayerMilestone($"milestone:game:first-point:{stat.PersonId}:{date:yyyyMMdd}", stat.PersonId, stat.PlayerName, PlayerMilestoneType.FirstNhlPoint, date, scenario.Season.Year, $"{stat.PlayerName} recorded a first tracked point.", true);
            }
        }

        if (goalie is not null && goalie.Won && goalie.GoalsAgainst == 0 && !scenario.PlayerMilestones.Any(item => item.PersonId == goalie.PersonId && item.MilestoneType == PlayerMilestoneType.FirstShutout))
        {
            yield return new PlayerMilestone($"milestone:game:first-shutout:{goalie.PersonId}:{date:yyyyMMdd}", goalie.PersonId, goalie.PlayerName, PlayerMilestoneType.FirstShutout, date, scenario.Season.Year, $"{goalie.PlayerName} recorded a first tracked shutout.", true);
        }
    }

    private static string BuildNarrative(GameSimulationContext context, GameResult result, IReadOnlyList<PlayerGameStatAllocation> skaters, GoalieGameStatAllocation? goalie)
    {
        var winner = result.WinnerOrganizationId == context.HomeTeam.OrganizationId ? context.HomeTeam : context.AwayTeam;
        var loser = result.LoserOrganizationId == context.HomeTeam.OrganizationId ? context.HomeTeam : context.AwayTeam;
        var score = $"{Math.Max(result.HomeGoals, result.AwayGoals)}-{Math.Min(result.HomeGoals, result.AwayGoals)}";
        var topSkater = skaters.OrderByDescending(stat => stat.Points).ThenByDescending(stat => stat.Goals).FirstOrDefault();
        var topText = topSkater is null ? "a balanced team effort" : $"{topSkater.PlayerName}'s {topSkater.Points}-point night";
        var goalieText = goalie is null ? "steady team defending" : $"{goalie.Saves} saves from {goalie.PlayerName}";
        return $"{winner.TeamName} defeated {loser.TeamName} {score} behind {topText} and {goalieText}.";
    }

    private static string TopLineSummary(TeamSimulationProfile? team, IReadOnlyList<PlayerGameStatAllocation> stats)
    {
        if (team is null)
        {
            return "No player-team line summary for this league game.";
        }

        var top = stats.OrderByDescending(stat => stat.OpportunityWeight).FirstOrDefault();
        return top is null
            ? $"{team.TeamName}'s top line did not create a tracked scoring note."
            : $"{top.PlayerName} drove the top usage group with {top.Points} point(s).";
    }

    private static string SpecialTeamsSummary(TeamSimulationProfile home, TeamSimulationProfile away) =>
        home.SpecialTeamsScore >= away.SpecialTeamsScore
            ? $"{home.TeamName} entered with the stronger special-teams profile."
            : $"{away.TeamName} entered with the stronger special-teams profile.";

    private static string ChemistryNote(TeamSimulationProfile? team)
    {
        if (team is null)
        {
            return "League game chemistry note unavailable.";
        }

        return team.Chemistry switch
        {
            TeamStrengthBand.Elite or TeamStrengthBand.Strong => $"{team.TeamName}'s chemistry helped stabilize the game.",
            TeamStrengthBand.Weak or TeamStrengthBand.BelowAverage => $"{team.TeamName}'s chemistry remains a coach concern.",
            _ => $"{team.TeamName}'s chemistry was neutral."
        };
    }

    private static string GoalieUsageNote(TeamSimulationProfile? team, GoalieGameStatAllocation? goalie) =>
        team is null
            ? "No player-team goalie usage note for this league game."
            : goalie is null
                ? $"{team.TeamName} did not have a tracked goalie allocation."
                : $"{goalie.PlayerName}: {goalie.UsageNote}";

    private static string InjuryNote(TeamSimulationProfile? team) =>
        team is null
            ? "No player-team injury note for this league game."
            : team.UnavailablePlayerIds.Count == 0
                ? "No injured player was used in the simulation profile."
                : $"{team.UnavailablePlayerIds.Count} injured player(s) were excluded from the game profile.";

    private static string DevelopmentNote(IReadOnlyList<PlayerGameStatAllocation> stats)
    {
        var pp = stats.FirstOrDefault(stat => stat.IncludedPowerPlayPoint);
        if (pp is not null)
        {
            return $"{pp.PlayerName}'s special-teams involvement should help confidence if the role continues.";
        }

        var top = stats.FirstOrDefault(stat => stat.OpportunityWeight >= 65);
        return top is null
            ? "No meaningful development note from this game."
            : $"{top.PlayerName}'s usage gave staff a clearer view of readiness.";
    }

    private static string KeyConcern(TeamSimulationProfile? team, GameResult result, int goalsAgainst)
    {
        if (team is null)
        {
            return "No player-team concern from this league game.";
        }

        if (goalsAgainst >= 5)
        {
            return "Defensive structure and goaltending support need review.";
        }

        if (team.Goalie.IsOverworked)
        {
            return "Goalie workload is worth monitoring.";
        }

        if (team.Chemistry is TeamStrengthBand.Weak or TeamStrengthBand.BelowAverage)
        {
            return "Line chemistry may be limiting consistency.";
        }

        return result.WinnerOrganizationId == team.OrganizationId ? "No major concern from this game." : "Staff will review chance quality and special teams.";
    }

    private static TeamSimulationProfile? PlayerTeamProfile(GameSimulationContext context, string organizationId) =>
        context.HomeTeam.OrganizationId == organizationId ? context.HomeTeam :
        context.AwayTeam.OrganizationId == organizationId ? context.AwayTeam :
        null;

    private static int OpportunityWeight(LineupRoleAssignment assignment, bool powerPlay)
    {
        var slotBoost = assignment.Slot switch
        {
            LineupSlot.Line1LW or LineupSlot.Line1C or LineupSlot.Line1RW => 35,
            LineupSlot.Line2LW or LineupSlot.Line2C or LineupSlot.Line2RW => 24,
            LineupSlot.Line3LW or LineupSlot.Line3C or LineupSlot.Line3RW => 13,
            LineupSlot.Line4LW or LineupSlot.Line4C or LineupSlot.Line4RW => 5,
            LineupSlot.Pair1LD or LineupSlot.Pair1RD => 19,
            LineupSlot.Pair2LD or LineupSlot.Pair2RD => 12,
            LineupSlot.Pair3LD or LineupSlot.Pair3RD => 7,
            _ => 0
        };
        return Math.Clamp(RoleScore(assignment.CurrentRole) / 2 + slotBoost + (powerPlay ? 12 : 0), 0, 100);
    }

    private static string UsageNote(LineupRoleAssignment assignment, int weight) =>
        weight >= 70 ? $"{assignment.PlayerName} was used as a primary driver." :
        weight >= 50 ? $"{assignment.PlayerName} had meaningful offensive usage." :
        weight >= 35 ? $"{assignment.PlayerName} had supporting usage." :
        $"{assignment.PlayerName} played limited minutes.";

    private static int AverageRoleScore(IEnumerable<LineupRoleAssignment> assignments, int fallbackCount)
    {
        var values = assignments.Select(assignment => RoleScore(assignment.CurrentRole)).ToArray();
        if (values.Length == 0)
        {
            return fallbackCount == 0 ? 45 : 50;
        }

        return ClampScore((int)Math.Round(values.Average()));
    }

    private static int RoleScore(LineupRole role) =>
        role switch
        {
            LineupRole.FranchiseForward or LineupRole.FranchiseDefenseman or LineupRole.FranchiseGoalie => 92,
            LineupRole.FirstLineForward or LineupRole.TopPairDefenseman or LineupRole.StartingGoalie => 78,
            LineupRole.TopSixForward or LineupRole.SecondPairDefenseman or LineupRole.TandemGoalie => 68,
            LineupRole.MiddleSixForward or LineupRole.ThirdPairDefenseman or LineupRole.BackupGoalie => 58,
            LineupRole.CheckingLineForward or LineupRole.FourthLineForward or LineupRole.DepthDefenseman or LineupRole.DepthGoalie => 48,
            LineupRole.ProspectForward or LineupRole.ProspectDefenseman or LineupRole.ProspectGoalie => 42,
            _ => 50
        };

    private static int CoachingScore(NewGmScenarioSnapshot scenario)
    {
        var coaches = scenario.StaffMembers
            .Where(member => member.EmploymentStatus == StaffEmploymentStatus.Employed && member.CurrentRole is StaffRole.HeadCoach or StaffRole.AssistantCoach or StaffRole.GoalieCoach or StaffRole.GoaltendingCoach)
            .ToArray();
        return coaches.Length == 0
            ? 50
            : ClampScore((int)Math.Round(coaches.Average(member => member.Profile.Reputation)));
    }

    private static bool IsForwardRole(LineupRoleAssignment assignment) =>
        assignment.CurrentRole is LineupRole.FranchiseForward or LineupRole.FirstLineForward or LineupRole.TopSixForward or LineupRole.MiddleSixForward or LineupRole.CheckingLineForward or LineupRole.FourthLineForward or LineupRole.DepthForward or LineupRole.ProspectForward;

    private static bool IsDefenseRole(LineupRoleAssignment assignment) =>
        assignment.CurrentRole is LineupRole.FranchiseDefenseman or LineupRole.TopPairDefenseman or LineupRole.SecondPairDefenseman or LineupRole.ThirdPairDefenseman or LineupRole.DepthDefenseman or LineupRole.ProspectDefenseman;

    private static bool IsGoalieRole(LineupRoleAssignment assignment) =>
        assignment.CurrentRole is LineupRole.FranchiseGoalie or LineupRole.StartingGoalie or LineupRole.TandemGoalie or LineupRole.BackupGoalie or LineupRole.DepthGoalie or LineupRole.ProspectGoalie;

    private static TeamStrengthBand Band(int score) =>
        score switch
        {
            < 40 => TeamStrengthBand.Weak,
            < 50 => TeamStrengthBand.BelowAverage,
            < 66 => TeamStrengthBand.Average,
            < 82 => TeamStrengthBand.Strong,
            _ => TeamStrengthBand.Elite
        };

    private static int ClampScore(int value) => Math.Clamp(value, 0, 100);

    private static int StableSeed(params object[] values)
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in values)
            {
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value?.ToString() ?? string.Empty);
            }

            return Math.Abs(hash);
        }
    }

    private static string PlayerName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private sealed class MutableSkater(LineupRoleAssignment assignment, int weight)
    {
        public LineupRoleAssignment Assignment { get; } = assignment;

        public int Weight { get; } = weight;

        public int Goals { get; set; }

        public int Assists { get; set; }

        public int PlusMinus { get; set; }

        public bool IncludedPowerPlayPoint { get; set; }
    }
}
