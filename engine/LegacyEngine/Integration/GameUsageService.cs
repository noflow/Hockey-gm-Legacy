using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class GameUsageService
{
    public NewGmScenarioSnapshot EnsureGameUsage(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var lineup = scenario.CurrentLineup ?? new LineupService().BuildDefaultLineup(
            scenario,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.AlphaSnapshot.Roster.ActivePlayers);
        var usage = BuildDefaultGameUsage(scenario with { CurrentLineup = lineup });
        var updated = scenario with { CurrentLineup = lineup, CurrentGameUsage = usage };
        updated.Validate();
        return updated;
    }

    public GameUsage BuildDefaultGameUsage(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var lineup = scenario.CurrentLineup ?? new LineupService().BuildDefaultLineup(
            scenario,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.AlphaSnapshot.Roster.ActivePlayers);
        var forwards = lineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch && IsForward(assignment.Position))
            .ToArray();
        var defense = lineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch && assignment.Position == RosterPosition.Defense)
            .ToArray();

        var pp1 = new PowerPlayUnit(
            1,
            lineup.ForwardLines.ElementAtOrDefault(0)?.LeftWing,
            lineup.ForwardLines.ElementAtOrDefault(0)?.Center,
            lineup.ForwardLines.ElementAtOrDefault(0)?.RightWing,
            lineup.DefensePairs.ElementAtOrDefault(0)?.LeftDefense,
            lineup.DefensePairs.ElementAtOrDefault(0)?.RightDefense);
        var pp2 = new PowerPlayUnit(
            2,
            lineup.ForwardLines.ElementAtOrDefault(1)?.LeftWing,
            lineup.ForwardLines.ElementAtOrDefault(1)?.Center,
            lineup.ForwardLines.ElementAtOrDefault(1)?.RightWing,
            lineup.DefensePairs.ElementAtOrDefault(1)?.LeftDefense,
            lineup.DefensePairs.ElementAtOrDefault(1)?.RightDefense);
        var pk1 = new PenaltyKillUnit(
            1,
            lineup.ForwardLines.ElementAtOrDefault(2)?.LeftWing ?? forwards.FirstOrDefault(),
            lineup.ForwardLines.ElementAtOrDefault(2)?.RightWing ?? forwards.Skip(1).FirstOrDefault(),
            lineup.DefensePairs.ElementAtOrDefault(0)?.LeftDefense,
            lineup.DefensePairs.ElementAtOrDefault(0)?.RightDefense);
        var pk2 = new PenaltyKillUnit(
            2,
            lineup.ForwardLines.ElementAtOrDefault(3)?.LeftWing ?? forwards.Skip(2).FirstOrDefault(),
            lineup.ForwardLines.ElementAtOrDefault(3)?.RightWing ?? forwards.Skip(3).FirstOrDefault(),
            lineup.DefensePairs.ElementAtOrDefault(1)?.LeftDefense ?? defense.FirstOrDefault(),
            lineup.DefensePairs.ElementAtOrDefault(1)?.RightDefense ?? defense.Skip(1).FirstOrDefault());

        var extra = new ExtraAttackerUnit(
            "extra-attacker:default",
            new[]
            {
                pp1.LeftWing,
                pp1.Center,
                pp1.RightWing,
                pp1.QuarterbackDefense,
                pp1.NetFrontOrSecondDefense,
                pp2.Center
            }.Where(player => player is not null).Select(player => player!).DistinctBy(player => player.PersonId).Take(6).ToArray(),
            "Six-on-five group prioritizes top forwards and first-unit puck movers.");
        var threeOnThree = new ThreeOnThreeUnit(
            "three-on-three:default",
            "2F1D",
            new[] { pp1.Center, pp1.RightWing, pp1.QuarterbackDefense }
                .Where(player => player is not null)
                .Select(player => player!)
                .DistinctBy(player => player.PersonId)
                .Take(3)
                .ToArray(),
            "Three-on-three placeholder uses two forwards and one defenseman.");
        var shootout = new ShootoutOrder(
            forwards
                .OrderByDescending(assignment => ShootoutScore(assignment))
                .Take(5)
                .ToArray(),
            "Shootout order favors skilled forwards and top usage players.");

        var specialTeams = new SpecialTeams(new[] { pp1, pp2 }, new[] { pk1, pk2 }, extra, threeOnThree, shootout);
        var goalieUsage = BuildGoalieUsage(scenario, lineup);
        var profiles = BuildProfiles(scenario, lineup, specialTeams, goalieUsage);
        var recommendations = BuildCoachRecommendations(scenario, specialTeams, goalieUsage, profiles);
        var usage = new GameUsage(
            $"game-usage:{scenario.Organization.OrganizationId}:{scenario.CurrentDate:yyyyMMdd}",
            scenario.Organization.OrganizationId,
            scenario.CurrentDate,
            specialTeams,
            goalieUsage,
            profiles,
            recommendations,
            $"Game usage covers even-strength lineup, PP1/PP2, PK1/PK2, goalie workload, extra attacker, three-on-three, and shootout order for {scenario.Organization.Name}.");
        usage.Validate();
        return usage;
    }

    public GameUsageManagementResult AutoFillGameUsage(NewGmScenarioSnapshot scenario)
    {
        var updated = EnsureGameUsage(scenario with { CurrentGameUsage = null });
        return Result(true, updated, "Game usage auto-filled from current lineup.");
    }

    public GameUsageManagementResult AssignPowerPlaySlot(NewGmScenarioSnapshot scenario, int unitNumber, PowerPlaySlot slot, string personId)
    {
        var assignment = RequireSpecialTeamsSkater(scenario, personId);
        if (slot is PowerPlaySlot.QuarterbackDefense or PowerPlaySlot.NetFrontOrSecondDefense)
        {
            if (assignment.Position != RosterPosition.Defense)
            {
                return Result(false, scenario, $"{assignment.PlayerName} is not a defenseman for this PP slot.");
            }
        }
        else if (!IsForward(assignment.Position))
        {
            return Result(false, scenario, $"{assignment.PlayerName} is not a forward for this PP slot.");
        }

        var usage = CurrentUsage(scenario);
        var units = usage.SpecialTeams.PowerPlayUnits
            .Select(unit => unit.UnitNumber == unitNumber ? AssignPowerPlay(unit, slot, assignment) : unit)
            .ToArray();
        if (units.All(unit => unit.UnitNumber != unitNumber))
        {
            return Result(false, scenario, $"Power Play Unit {unitNumber} was not found.");
        }

        return UpdateUsage(scenario, usage.SpecialTeams with { PowerPlayUnits = units }, $"{assignment.PlayerName} assigned to PP{unitNumber} {slot}.");
    }

    public GameUsageManagementResult AssignPenaltyKillSlot(NewGmScenarioSnapshot scenario, int unitNumber, PenaltyKillSlot slot, string personId)
    {
        var assignment = RequireSpecialTeamsSkater(scenario, personId);
        if (slot is PenaltyKillSlot.LeftDefense or PenaltyKillSlot.RightDefense)
        {
            if (assignment.Position != RosterPosition.Defense)
            {
                return Result(false, scenario, $"{assignment.PlayerName} is not a defenseman for this PK slot.");
            }
        }
        else if (!IsForward(assignment.Position))
        {
            return Result(false, scenario, $"{assignment.PlayerName} is not a forward for this PK slot.");
        }

        var usage = CurrentUsage(scenario);
        var units = usage.SpecialTeams.PenaltyKillUnits
            .Select(unit => unit.UnitNumber == unitNumber ? AssignPenaltyKill(unit, slot, assignment) : unit)
            .ToArray();
        if (units.All(unit => unit.UnitNumber != unitNumber))
        {
            return Result(false, scenario, $"Penalty Kill Unit {unitNumber} was not found.");
        }

        return UpdateUsage(scenario, usage.SpecialTeams with { PenaltyKillUnits = units }, $"{assignment.PlayerName} assigned to PK{unitNumber} {slot}.");
    }

    public GameUsageManagementResult MoveShootoutPlayer(NewGmScenarioSnapshot scenario, string personId, int direction)
    {
        var usage = CurrentUsage(scenario);
        var shooters = usage.SpecialTeams.ShootoutOrder.Shooters.ToList();
        var index = shooters.FindIndex(player => player.PersonId == personId);
        if (index < 0)
        {
            shooters.Add(RequireSpecialTeamsSkater(scenario, personId));
            index = shooters.Count - 1;
        }

        var newIndex = Math.Clamp(index + direction, 0, shooters.Count - 1);
        (shooters[index], shooters[newIndex]) = (shooters[newIndex], shooters[index]);
        var specialTeams = usage.SpecialTeams with
        {
            ShootoutOrder = usage.SpecialTeams.ShootoutOrder with
            {
                Shooters = shooters.DistinctBy(player => player.PersonId).Take(8).ToArray(),
                Summary = "Shootout order was adjusted by the GM."
            }
        };
        return UpdateUsage(scenario, specialTeams, "Shootout order updated.");
    }

    public LineupDevelopmentImpact BuildDevelopmentImpact(NewGmScenarioSnapshot scenario, string personId)
    {
        var profile = CurrentUsage(scenario).PlayerProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (profile is null)
        {
            var impact = new LineupDevelopmentImpact(personId, LineupRole.DepthForward, 0, "No game usage modifier tracked for this player.");
            impact.Validate();
            return impact;
        }

        var role = profile.Role;
        var impactProfile = new LineupDevelopmentImpact(personId, role, profile.DevelopmentModifier, $"Game usage: {profile.UsageSummary}");
        impactProfile.Validate();
        return impactProfile;
    }

    public IReadOnlyList<string> BuildDossierUsageLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var usage = CurrentUsage(scenario);
        var profile = usage.PlayerProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (profile is null)
        {
            return new[] { "No game usage profile is currently tracked." };
        }

        var lines = new List<string>
        {
            $"Current line: {profile.CurrentLine}",
            $"Power play: {profile.PowerPlayUsage}",
            $"Penalty kill: {profile.PenaltyKillUsage}",
            $"Extra attacker: {profile.ExtraAttackerUsage}",
            $"Three-on-three: {profile.ThreeOnThreeUsage}",
            $"Shootout: {profile.ShootoutUsage}",
            $"Role: {LineupDisplay.Role(profile.Role)}",
            $"Usage summary: {profile.UsageSummary}",
            $"Development modifier: {profile.DevelopmentModifier:+#;-#;0}",
            $"Coach comment: {profile.CoachComment}"
        };
        var goalie = usage.GoalieUsage.FirstOrDefault(goalie => goalie.PersonId == personId);
        if (goalie is not null)
        {
            lines.Add($"Goalie usage: {goalie.UsageRole}, starts {goalie.GamesStarted}/{goalie.ExpectedStarts}, workload {goalie.Workload}. {goalie.RestRecommendation}");
        }

        return lines;
    }

    public GameUsage CurrentUsage(NewGmScenarioSnapshot scenario) =>
        scenario.CurrentGameUsage ?? BuildDefaultGameUsage(scenario);

    private GameUsageManagementResult UpdateUsage(NewGmScenarioSnapshot scenario, SpecialTeams specialTeams, string message)
    {
        var lineup = scenario.CurrentLineup ?? new LineupService().BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var goalieUsage = BuildGoalieUsage(scenario, lineup);
        var profiles = BuildProfiles(scenario, lineup, specialTeams, goalieUsage);
        var recommendations = BuildCoachRecommendations(scenario, specialTeams, goalieUsage, profiles);
        var usage = CurrentUsage(scenario) with
        {
            CreatedOn = scenario.CurrentDate,
            SpecialTeams = specialTeams,
            GoalieUsage = goalieUsage,
            PlayerProfiles = profiles,
            CoachRecommendations = recommendations,
            Summary = $"Game usage updated on {scenario.CurrentDate:yyyy-MM-dd}."
        };
        usage.Validate();
        var updated = scenario with { CurrentLineup = lineup, CurrentGameUsage = usage };
        updated.Validate();
        return Result(true, updated, message);
    }

    private static PowerPlayUnit AssignPowerPlay(PowerPlayUnit unit, PowerPlaySlot slot, LineupRoleAssignment assignment) =>
        slot switch
        {
            PowerPlaySlot.LeftWing => unit with { LeftWing = assignment },
            PowerPlaySlot.Center => unit with { Center = assignment },
            PowerPlaySlot.RightWing => unit with { RightWing = assignment },
            PowerPlaySlot.QuarterbackDefense => unit with { QuarterbackDefense = assignment },
            PowerPlaySlot.NetFrontOrSecondDefense => unit with { NetFrontOrSecondDefense = assignment },
            _ => unit
        };

    private static PenaltyKillUnit AssignPenaltyKill(PenaltyKillUnit unit, PenaltyKillSlot slot, LineupRoleAssignment assignment) =>
        slot switch
        {
            PenaltyKillSlot.LeftWing => unit with { LeftWing = assignment },
            PenaltyKillSlot.RightWing => unit with { RightWing = assignment },
            PenaltyKillSlot.LeftDefense => unit with { LeftDefense = assignment },
            PenaltyKillSlot.RightDefense => unit with { RightDefense = assignment },
            _ => unit
        };

    private static IReadOnlyList<GoalieUsageProfile> BuildGoalieUsage(NewGmScenarioSnapshot scenario, Lineup lineup)
    {
        var goalieStats = scenario.GoalieStats.ToDictionary(stat => stat.PersonId, StringComparer.Ordinal);
        var starter = lineup.Goalies.Starter;
        var backup = lineup.Goalies.Backup;
        return new[] { starter, backup }
            .Where(goalie => goalie is not null)
            .Select(goalie =>
            {
                var player = goalie!;
                var starts = goalieStats.TryGetValue(player.PersonId, out var stat) ? stat.GamesPlayed : 0;
                var expected = player.Slot == LineupSlot.Starter ? 55 : 25;
                var workload = starts > expected ? "Heavy" : player.Slot == LineupSlot.Starter ? "Starter workload" : "Backup workload";
                return new GoalieUsageProfile(
                    player.PersonId,
                    player.PlayerName,
                    player.Slot == LineupSlot.Starter ? "Starter" : "Backup",
                    starts,
                    expected,
                    workload,
                    starts > expected ? "Reduce goalie workload and give the backup a start soon." : "Workload is acceptable.");
            })
            .ToArray();
    }

    private static IReadOnlyList<GameUsageProfile> BuildProfiles(NewGmScenarioSnapshot scenario, Lineup lineup, SpecialTeams specialTeams, IReadOnlyList<GoalieUsageProfile> goalieUsage)
    {
        return lineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch)
            .Select(assignment =>
            {
                var pp = PowerPlayUsage(specialTeams, assignment.PersonId);
                var pk = PenaltyKillUsage(specialTeams, assignment.PersonId);
                var extra = specialTeams.ExtraAttacker.Players.Any(player => player.PersonId == assignment.PersonId) ? "Extra Attacker" : "No extra-attacker usage";
                var three = specialTeams.ThreeOnThree.Players.Any(player => player.PersonId == assignment.PersonId) ? $"3-on-3 {specialTeams.ThreeOnThree.Combination}" : "No 3-on-3 usage";
                var shootoutIndex = specialTeams.ShootoutOrder.Shooters.ToList().FindIndex(player => player.PersonId == assignment.PersonId);
                var shootout = shootoutIndex >= 0 ? $"Shootout #{shootoutIndex + 1}" : "No shootout role";
                var goalie = goalieUsage.FirstOrDefault(goalie => goalie.PersonId == assignment.PersonId);
                var modifier = DevelopmentModifier(assignment, pp, pk, goalie);
                return new GameUsageProfile(
                    assignment.PersonId,
                    assignment.PlayerName,
                    assignment.SlotLabel,
                    pp,
                    pk,
                    extra,
                    three,
                    shootout,
                    assignment.CurrentRole,
                    modifier,
                    UsageSummaryFor(assignment, pp, pk, goalie),
                    CoachCommentFor(assignment, pp, pk, goalie));
            })
            .ToArray();
    }

    private static IReadOnlyList<GameUsageCoachRecommendation> BuildCoachRecommendations(NewGmScenarioSnapshot scenario, SpecialTeams specialTeams, IReadOnlyList<GoalieUsageProfile> goalieUsage, IReadOnlyList<GameUsageProfile> profiles)
    {
        var recommendations = new List<GameUsageCoachRecommendation>();
        var pp1 = specialTeams.PowerPlayUnits.First();
        if (!pp1.Players.Any(player => ContainsPlaymaker(player.PlayerType)) || pp1.QuarterbackDefense is null)
        {
            recommendations.Add(new GameUsageCoachRecommendation(
                $"game-usage-rec:pp1:{scenario.CurrentDate:yyyyMMdd}",
                GameUsageRecommendationType.ImprovePowerPlayBalance,
                pp1.Center?.PersonId,
                pp1.Center?.PlayerName ?? "PP1",
                "Power Play Unit 1 lacks a clear playmaker or QB defense option.",
                "Review PP1 personnel and consider a puck distributor or QB defenseman.",
                true));
        }

        var youngPp2 = profiles.FirstOrDefault(profile => profile.PowerPlayUsage.Contains("PP2", StringComparison.Ordinal) && profile.Role.ToString().Contains("Prospect", StringComparison.OrdinalIgnoreCase));
        if (youngPp2 is not null)
        {
            recommendations.Add(new GameUsageCoachRecommendation(
                $"game-usage-rec:young-pp2:{youngPp2.PersonId}",
                GameUsageRecommendationType.PromoteYoungPlayerToPowerPlayTwo,
                youngPp2.PersonId,
                youngPp2.PlayerName,
                "Young player has sheltered offensive usage on PP2.",
                "Keep PP2 minutes manageable and monitor confidence.",
                false));
        }

        var pkVeteran = profiles.FirstOrDefault(profile => profile.PenaltyKillUsage.Contains("PK", StringComparison.Ordinal) && profile.Role is LineupRole.CheckingLineForward or LineupRole.SecondPairDefenseman or LineupRole.ThirdPairDefenseman);
        if (pkVeteran is null)
        {
            recommendations.Add(new GameUsageCoachRecommendation(
                $"game-usage-rec:pk-veteran:{scenario.CurrentDate:yyyyMMdd}",
                GameUsageRecommendationType.UseVeteranOnPenaltyKill,
                null,
                "Penalty Kill",
                "Penalty kill groups need a steadier veteran/checking profile.",
                "Use a veteran or checking forward on PK1/PK2.",
                true));
        }

        foreach (var goalie in goalieUsage.Where(goalie => goalie.Workload == "Heavy"))
        {
            recommendations.Add(new GameUsageCoachRecommendation(
                $"game-usage-rec:goalie:{goalie.PersonId}",
                GameUsageRecommendationType.ReduceGoalieWorkload,
                goalie.PersonId,
                goalie.PlayerName,
                "Goalie workload is trending heavy.",
                goalie.RestRecommendation,
                true));
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new GameUsageCoachRecommendation(
                $"game-usage-rec:stable:{scenario.CurrentDate:yyyyMMdd}",
                GameUsageRecommendationType.ImprovePowerPlayBalance,
                null,
                "Staff",
                "Special teams usage is stable for now.",
                "Keep monitoring game usage as results arrive.",
                false));
        }

        return recommendations.Take(5).ToArray();
    }

    private static string PowerPlayUsage(SpecialTeams specialTeams, string personId)
    {
        foreach (var unit in specialTeams.PowerPlayUnits)
        {
            if (unit.LeftWing?.PersonId == personId) return $"PP{unit.UnitNumber} LW";
            if (unit.Center?.PersonId == personId) return $"PP{unit.UnitNumber} C";
            if (unit.RightWing?.PersonId == personId) return $"PP{unit.UnitNumber} RW";
            if (unit.QuarterbackDefense?.PersonId == personId) return $"PP{unit.UnitNumber} QB";
            if (unit.NetFrontOrSecondDefense?.PersonId == personId) return $"PP{unit.UnitNumber} Net-front/second D";
        }

        return "No power-play usage";
    }

    private static string PenaltyKillUsage(SpecialTeams specialTeams, string personId)
    {
        foreach (var unit in specialTeams.PenaltyKillUnits)
        {
            if (unit.LeftWing?.PersonId == personId) return $"PK{unit.UnitNumber} LW";
            if (unit.RightWing?.PersonId == personId) return $"PK{unit.UnitNumber} RW";
            if (unit.LeftDefense?.PersonId == personId) return $"PK{unit.UnitNumber} LD";
            if (unit.RightDefense?.PersonId == personId) return $"PK{unit.UnitNumber} RD";
        }

        return "No penalty-kill usage";
    }

    private static int DevelopmentModifier(LineupRoleAssignment assignment, string pp, string pk, GoalieUsageProfile? goalie)
    {
        var modifier = 0;
        if (assignment.Age is <= 21 && pp.Contains("PP", StringComparison.Ordinal))
        {
            modifier += assignment.Position == RosterPosition.Defense ? 3 : 2;
        }

        if (pk.Contains("PK", StringComparison.Ordinal) && assignment.Position != RosterPosition.Goalie)
        {
            modifier += 1;
        }

        if (goalie is not null && goalie.UsageRole == "Backup" && goalie.GamesStarted < Math.Max(1, goalie.ExpectedStarts / 4))
        {
            modifier -= 2;
        }

        return Math.Clamp(modifier, -10, 10);
    }

    private static string UsageSummaryFor(LineupRoleAssignment assignment, string pp, string pk, GoalieUsageProfile? goalie)
    {
        if (goalie is not null)
        {
            return $"{goalie.UsageRole} goalie usage with {goalie.Workload.ToLowerInvariant()} and {goalie.GamesStarted} recorded starts.";
        }

        if (pp.Contains("PP", StringComparison.Ordinal) && pk.Contains("PK", StringComparison.Ordinal))
        {
            return "Used in all-situations personnel deployment.";
        }

        if (pp.Contains("PP", StringComparison.Ordinal))
        {
            return assignment.Position == RosterPosition.Defense
                ? "Power-play QB/defense usage can support offensive awareness."
                : "Power-play usage can support puck touches and confidence.";
        }

        if (pk.Contains("PK", StringComparison.Ordinal))
        {
            return "Penalty-kill usage can support defensive habits and trust.";
        }

        return "Even-strength usage only for now.";
    }

    private static string CoachCommentFor(LineupRoleAssignment assignment, string pp, string pk, GoalieUsageProfile? goalie)
    {
        if (goalie is not null)
        {
            return goalie.RestRecommendation;
        }

        if (assignment.Age is <= 21 && pp.Contains("PP1", StringComparison.Ordinal))
        {
            return "Young player has high offensive responsibility; monitor confidence.";
        }

        if (pk.Contains("PK", StringComparison.Ordinal))
        {
            return "Staff trusts this player in defensive game states.";
        }

        return "Usage is stable; revisit after special-teams results arrive.";
    }

    private LineupRoleAssignment RequireSpecialTeamsSkater(NewGmScenarioSnapshot scenario, string personId)
    {
        var lineup = scenario.CurrentLineup ?? new LineupService().BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var assignment = lineup.Assignments.FirstOrDefault(player => player.PersonId == personId && player.Slot != LineupSlot.HealthyScratch);
        if (assignment is null)
        {
            throw new ArgumentException("Player must be in the active lineup before receiving game usage.", nameof(personId));
        }

        if (assignment.Position == RosterPosition.Goalie)
        {
            throw new ArgumentException("Goalies cannot be assigned to skater special teams units.", nameof(personId));
        }

        return assignment;
    }

    private static bool IsForward(RosterPosition position) =>
        position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing;

    private static bool ContainsPlaymaker(string text) =>
        text.Contains("Playmaking", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Playmaker", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Two-Way Center", StringComparison.OrdinalIgnoreCase);

    private static int ShootoutScore(LineupRoleAssignment assignment)
    {
        var score = assignment.CurrentRole switch
        {
            LineupRole.FranchiseForward => 90,
            LineupRole.FirstLineForward => 82,
            LineupRole.TopSixForward => 74,
            LineupRole.MiddleSixForward => 62,
            _ => 50
        };
        if (assignment.PlayerType.Contains("Scoring", StringComparison.OrdinalIgnoreCase) || assignment.PlayerType.Contains("Shooter", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        return score;
    }

    private static GameUsageManagementResult Result(bool success, NewGmScenarioSnapshot scenario, string message)
    {
        var result = new GameUsageManagementResult(success, scenario, message);
        result.Validate();
        return result;
    }
}
