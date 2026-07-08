using LegacyEngine.Development;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class LineupService
{
    public NewGmScenarioSnapshot EnsureLineup(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var lineup = BuildDefaultLineup(
            scenario,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.AlphaSnapshot.Roster.ActivePlayers);
        var updated = scenario with { CurrentLineup = lineup };
        updated.Validate();
        return updated;
    }

    public Lineup BuildDefaultLineup(
        NewGmScenarioSnapshot scenario,
        string organizationId,
        string organizationName,
        IReadOnlyList<RosterPlayer> rosterPlayers)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(rosterPlayers);

        var forwards = rosterPlayers
            .Where(player => player.Status == RosterStatus.Active && IsForward(player.Position))
            .OrderByDescending(player => PlayerScore(scenario, player))
            .ThenBy(player => player.Position == RosterPosition.Center ? 0 : 1)
            .ToList();
        var defense = rosterPlayers
            .Where(player => player.Status == RosterStatus.Active && player.Position == RosterPosition.Defense)
            .OrderByDescending(player => PlayerScore(scenario, player))
            .ToList();
        var goalies = rosterPlayers
            .Where(player => player.Status == RosterStatus.Active && player.Position == RosterPosition.Goalie)
            .OrderByDescending(player => PlayerScore(scenario, player))
            .ToList();

        var assignments = new List<LineupRoleAssignment>();
        var forwardLines = new List<ForwardLine>();
        for (var line = 1; line <= 4; line++)
        {
            var left = TakeForward(forwards, RosterPosition.LeftWing) ?? TakeForward(forwards);
            var center = TakeForward(forwards, RosterPosition.Center) ?? TakeForward(forwards);
            var right = TakeForward(forwards, RosterPosition.RightWing) ?? TakeForward(forwards);
            var lw = left is null ? null : AssignmentFor(scenario, left, RoleForForward(scenario, line, assignments.Count(item => IsForward(item.Position))), PotentialRoleFor(scenario, left), SlotForForward(line, 0));
            var c = center is null ? null : AssignmentFor(scenario, center, RoleForForward(scenario, line, assignments.Count(item => IsForward(item.Position))), PotentialRoleFor(scenario, center), SlotForForward(line, 1));
            var rw = right is null ? null : AssignmentFor(scenario, right, RoleForForward(scenario, line, assignments.Count(item => IsForward(item.Position))), PotentialRoleFor(scenario, right), SlotForForward(line, 2));
            AddIfNotNull(assignments, lw);
            AddIfNotNull(assignments, c);
            AddIfNotNull(assignments, rw);
            forwardLines.Add(new ForwardLine(line, lw, c, rw));
        }

        var pairs = new List<DefensePair>();
        for (var pair = 1; pair <= 3; pair++)
        {
            var left = TakeDefense(defense);
            var right = TakeDefense(defense);
            var ld = left is null ? null : AssignmentFor(scenario, left, RoleForDefense(scenario, pair, assignments.Count(item => item.Position == RosterPosition.Defense)), PotentialRoleFor(scenario, left), SlotForDefense(pair, 0));
            var rd = right is null ? null : AssignmentFor(scenario, right, RoleForDefense(scenario, pair, assignments.Count(item => item.Position == RosterPosition.Defense)), PotentialRoleFor(scenario, right), SlotForDefense(pair, 1));
            AddIfNotNull(assignments, ld);
            AddIfNotNull(assignments, rd);
            pairs.Add(new DefensePair(pair, ld, rd));
        }

        var starterPlayer = TakeGoalie(goalies);
        var backupPlayer = TakeGoalie(goalies);
        var starter = starterPlayer is null ? null : AssignmentFor(scenario, starterPlayer, RoleForGoalie(scenario, 0), PotentialRoleFor(scenario, starterPlayer), LineupSlot.Starter);
        var backup = backupPlayer is null ? null : AssignmentFor(scenario, backupPlayer, RoleForGoalie(scenario, 1), PotentialRoleFor(scenario, backupPlayer), LineupSlot.Backup);
        AddIfNotNull(assignments, starter);
        AddIfNotNull(assignments, backup);

        foreach (var scratch in forwards.Concat(defense).Concat(goalies))
        {
            assignments.Add(AssignmentFor(scenario, scratch, DepthRoleFor(scenario, scratch), PotentialRoleFor(scenario, scratch), LineupSlot.HealthyScratch));
        }

        var lineup = new Lineup(
            $"lineup:{organizationId}:{scenario.CurrentDate:yyyyMMdd}",
            organizationId,
            organizationName,
            scenario.CurrentDate,
            forwardLines,
            pairs,
            new GoalieDepth(starter, backup),
            assignments,
            BuildCoachRecommendations(scenario, assignments),
            SummaryFor(assignments))
        {
            RoleExpectations = BuildExpectations(assignments),
            Usage = BuildUsage(scenario, assignments, Array.Empty<PlayerRolePromise>()),
            RoleHistory = new[] { $"{scenario.CurrentDate:yyyy-MM-dd}: Auto-filled default lineup." }
        };
        lineup.Validate();
        return lineup;
    }

    public LineupManagementResult AutoFillLineup(NewGmScenarioSnapshot scenario)
    {
        var updated = EnsureLineup(scenario);
        return Result(true, updated, ValidateLineup(updated), "Lineup auto-filled from active roster.");
    }

    public LineupManagementResult AssignPlayerToSlot(NewGmScenarioSnapshot scenario, LineupSlot slot, string personId)
    {
        var validation = ValidatePlayerForSlot(scenario, slot, personId);
        if (!validation.IsValid)
        {
            return Result(false, scenario, validation, validation.Message);
        }

        var lineup = scenario.CurrentLineup ?? BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var assignments = lineup.Assignments
            .Where(assignment => assignment.PersonId != personId && assignment.Slot != slot)
            .ToList();

        var displaced = lineup.Assignments.FirstOrDefault(assignment => assignment.Slot == slot);
        if (displaced is not null && displaced.PersonId != personId)
        {
            assignments.Add(displaced with { CurrentRole = DepthRoleFor(scenario, RequireRosterPlayer(scenario, displaced.PersonId)), Slot = LineupSlot.HealthyScratch, CoachNote = "Moved out of lineup by GM assignment." });
        }

        var player = RequireRosterPlayer(scenario, personId);
        assignments.Add(AssignmentFor(scenario, player, RoleForSlot(scenario, player, slot), PotentialRoleFor(scenario, player), slot));
        var next = RebuildLineup(scenario, lineup, assignments, $"{scenario.CurrentDate:yyyy-MM-dd}: GM assigned {PersonName(scenario, personId)} to {LineupDisplay.SlotLabel(slot)}.");
        var updated = scenario with { CurrentLineup = next };
        updated = ApplyBrokenPromiseEffects(updated);
        return Result(true, updated, ValidateLineup(updated), $"{PersonName(updated, personId)} assigned to {LineupDisplay.SlotLabel(slot)}.");
    }

    public LineupManagementResult RemovePlayerFromSlot(NewGmScenarioSnapshot scenario, LineupSlot slot)
    {
        var lineup = scenario.CurrentLineup ?? BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var assignment = lineup.Assignments.FirstOrDefault(item => item.Slot == slot);
        if (assignment is null)
        {
            return Result(false, scenario, new LineupValidationResult(false, new[] { "Selected lineup slot is already empty." }, "Selected lineup slot is already empty."), "Selected lineup slot is already empty.");
        }

        var player = RequireRosterPlayer(scenario, assignment.PersonId);
        var assignments = lineup.Assignments
            .Where(item => item.PersonId != assignment.PersonId)
            .Append(AssignmentFor(scenario, player, DepthRoleFor(scenario, player), PotentialRoleFor(scenario, player), LineupSlot.HealthyScratch))
            .ToList();
        var next = RebuildLineup(scenario, lineup, assignments, $"{scenario.CurrentDate:yyyy-MM-dd}: GM removed {assignment.PlayerName} from {LineupDisplay.SlotLabel(slot)}.");
        var updated = scenario with { CurrentLineup = next };
        updated = ApplyBrokenPromiseEffects(updated);
        return Result(true, updated, ValidateLineup(updated), $"{assignment.PlayerName} removed from {LineupDisplay.SlotLabel(slot)}.");
    }

    public LineupManagementResult SwapPlayers(NewGmScenarioSnapshot scenario, LineupSlot firstSlot, LineupSlot secondSlot)
    {
        if (firstSlot == secondSlot)
        {
            return Result(false, scenario, new LineupValidationResult(false, new[] { "Select two different lineup slots to swap." }, "Select two different lineup slots to swap."), "Select two different lineup slots to swap.");
        }

        var lineup = scenario.CurrentLineup ?? BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var first = lineup.Assignments.FirstOrDefault(item => item.Slot == firstSlot);
        var second = lineup.Assignments.FirstOrDefault(item => item.Slot == secondSlot);
        if (first is null || second is null)
        {
            return Result(false, scenario, new LineupValidationResult(false, new[] { "Both slots need assigned players before swapping." }, "Both slots need assigned players before swapping."), "Both slots need assigned players before swapping.");
        }

        var firstValidation = ValidatePlayerForSlot(scenario, secondSlot, first.PersonId, ignoreDuplicate: true);
        var secondValidation = ValidatePlayerForSlot(scenario, firstSlot, second.PersonId, ignoreDuplicate: true);
        var warnings = firstValidation.Warnings.Concat(secondValidation.Warnings).Distinct(StringComparer.Ordinal).ToArray();
        if (!firstValidation.IsValid || !secondValidation.IsValid)
        {
            return Result(false, scenario, new LineupValidationResult(false, warnings, string.Join(" ", warnings)), string.Join(" ", warnings));
        }

        var firstPlayer = RequireRosterPlayer(scenario, first.PersonId);
        var secondPlayer = RequireRosterPlayer(scenario, second.PersonId);
        var assignments = lineup.Assignments
            .Where(item => item.PersonId != first.PersonId && item.PersonId != second.PersonId)
            .Append(AssignmentFor(scenario, firstPlayer, RoleForSlot(scenario, firstPlayer, secondSlot), PotentialRoleFor(scenario, firstPlayer), secondSlot))
            .Append(AssignmentFor(scenario, secondPlayer, RoleForSlot(scenario, secondPlayer, firstSlot), PotentialRoleFor(scenario, secondPlayer), firstSlot))
            .ToList();
        var next = RebuildLineup(scenario, lineup, assignments, $"{scenario.CurrentDate:yyyy-MM-dd}: GM swapped {first.PlayerName} and {second.PlayerName}.");
        var updated = scenario with { CurrentLineup = next };
        updated = ApplyBrokenPromiseEffects(updated);
        return Result(true, updated, ValidateLineup(updated), $"{first.PlayerName} and {second.PlayerName} swapped.");
    }

    public NewGmScenarioSnapshot SetRolePromise(NewGmScenarioSnapshot scenario, string personId, LineupRole promisedRole, string source)
    {
        var lineup = scenario.CurrentLineup ?? BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var promise = new PlayerRolePromise(
            personId,
            PersonName(scenario, personId),
            promisedRole,
            scenario.CurrentDate,
            string.IsNullOrWhiteSpace(source) ? "GM role promise" : source,
            Summary: $"Promised {LineupDisplay.Role(promisedRole)}.");
        promise.Validate();

        var promises = lineup.RolePromises
            .Where(item => item.PersonId != personId)
            .Append(promise)
            .ToArray();
        var next = RebuildLineup(scenario, lineup, lineup.Assignments, $"{scenario.CurrentDate:yyyy-MM-dd}: GM promised {promise.PlayerName} {LineupDisplay.Role(promisedRole)}.", promises);
        var updated = scenario with { CurrentLineup = next };
        updated.Validate();
        return updated;
    }

    public LineupValidationResult ValidateLineup(NewGmScenarioSnapshot scenario)
    {
        var lineup = scenario.CurrentLineup ?? BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var warnings = new List<string>();
        foreach (var assignment in lineup.Assignments.Where(assignment => assignment.Slot != LineupSlot.HealthyScratch))
        {
            warnings.AddRange(ValidatePlayerForSlot(scenario, assignment.Slot, assignment.PersonId, ignoreDuplicate: true).Warnings);
        }

        var duplicate = lineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch)
            .GroupBy(assignment => assignment.PersonId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            warnings.Add($"{duplicate.First().PlayerName} appears in the lineup more than once.");
        }

        var missingSlots = Enum.GetValues<LineupSlot>()
            .Where(slot => slot != LineupSlot.HealthyScratch && lineup.Assignments.All(assignment => assignment.Slot != slot))
            .Select(LineupDisplay.SlotLabel)
            .ToArray();
        if (missingSlots.Length > 0)
        {
            warnings.Add($"Lineup has open slots: {string.Join(", ", missingSlots)}.");
        }

        var result = new LineupValidationResult(warnings.Count == 0, warnings.Distinct(StringComparer.Ordinal).ToArray(), warnings.Count == 0 ? "Lineup is valid." : string.Join(" ", warnings.Distinct(StringComparer.Ordinal)));
        result.Validate();
        return result;
    }

    public LineupRoleAssignment? FindAssignment(NewGmScenarioSnapshot scenario, string personId) =>
        (scenario.CurrentLineup ?? BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers))
        .Assignments.FirstOrDefault(assignment => assignment.PersonId == personId);

    public IReadOnlyList<LineupRoleAssignment> EligiblePlayersForSlot(NewGmScenarioSnapshot scenario, LineupSlot slot)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return scenario.AlphaSnapshot.Roster.ActivePlayers
            .Where(player => ValidatePlayerForSlot(scenario, slot, player.PersonId).IsValid)
            .OrderByDescending(player => PlayerScore(scenario, player))
            .Select(player => AssignmentFor(scenario, player, RoleForSlot(scenario, player, slot), PotentialRoleFor(scenario, player), slot))
            .ToArray();
    }

    public LineupDevelopmentImpact BuildDevelopmentImpact(NewGmScenarioSnapshot scenario, string personId)
    {
        var assignment = FindAssignment(scenario, personId);
        if (assignment is null)
        {
            var neutral = new LineupDevelopmentImpact(personId, LineupRole.DepthForward, 0, "Player is not in the current lineup; development impact is neutral until a role is assigned.");
            neutral.Validate();
            return neutral;
        }

        var modifier = assignment.CurrentRole switch
        {
            LineupRole.FranchiseForward or LineupRole.FirstLineForward or LineupRole.TopSixForward or LineupRole.TopPairDefenseman or LineupRole.StartingGoalie => 5,
            LineupRole.CheckingLineForward or LineupRole.ThirdPairDefenseman or LineupRole.BackupGoalie => 2,
            LineupRole.ProspectForward or LineupRole.ProspectDefenseman or LineupRole.ProspectGoalie when assignment.Slot == LineupSlot.HealthyScratch => -4,
            _ when assignment.Slot == LineupSlot.HealthyScratch => -3,
            _ => 0
        };
        var summary = assignment.CurrentRole switch
        {
            LineupRole.FranchiseForward or LineupRole.FirstLineForward or LineupRole.TopSixForward => "Top-line minutes can accelerate high-skill development when the player is ready.",
            LineupRole.CheckingLineForward => "Checking-line usage should build defensive habits, physical traits, and role detail.",
            LineupRole.StartingGoalie => "Goalie starts create meaningful development pressure and confidence swings.",
            _ when assignment.Slot == LineupSlot.HealthyScratch => "Healthy scratches and buried prospects develop slower without a clear minutes plan.",
            _ => "Lineup role creates a modest development context for staff review."
        };
        var impact = new LineupDevelopmentImpact(personId, assignment.CurrentRole, modifier, summary);
        impact.Validate();
        return impact;
    }

    private static LineupValidationResult ValidatePlayerForSlot(NewGmScenarioSnapshot scenario, LineupSlot slot, string personId, bool ignoreDuplicate = false)
    {
        var warnings = new List<string>();
        var player = scenario.AlphaSnapshot.Roster.FindPlayer(personId)
            ?? scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId);
        if (player is null)
        {
            warnings.Add("Selected player is not on the organization roster.");
        }
        else
        {
            if (!IsEligibleForSlot(player.Position, slot))
            {
                warnings.Add($"{PersonName(scenario, personId)} is {player.Position} and cannot be assigned to {LineupDisplay.SlotLabel(slot)}.");
            }

            if (player.Status is RosterStatus.InjuredReserve or RosterStatus.Released)
            {
                warnings.Add($"{PersonName(scenario, personId)} is {player.Status} and unavailable for lineup assignment.");
            }
        }

        if (scenario.AlphaSnapshot.Injuries.Any(injury => injury.PersonId == personId && injury.IsActive))
        {
            warnings.Add($"{PersonName(scenario, personId)} has an active injury and should not be assigned without medical clearance.");
        }

        var pipeline = scenario.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId);
        if (pipeline is not null && !pipeline.IsSigned && scenario.AlphaSnapshot.Roster.FindPlayer(personId) is null)
        {
            warnings.Add($"{PersonName(scenario, personId)} is an unsigned prospect and unavailable for active lineup assignment.");
        }

        if (pipeline is not null && pipeline.PipelineStatus.ToString().Contains("Returned", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{PersonName(scenario, personId)} has been returned to junior/youth and is unavailable for active lineup assignment.");
        }

        var lineup = scenario.CurrentLineup;
        if (!ignoreDuplicate && lineup?.Assignments.Any(assignment => assignment.PersonId == personId && assignment.Slot != LineupSlot.HealthyScratch) == true)
        {
            warnings.Add($"{PersonName(scenario, personId)} is already assigned in the lineup.");
        }

        var result = new LineupValidationResult(warnings.Count == 0, warnings.Distinct(StringComparer.Ordinal).ToArray(), warnings.Count == 0 ? "Lineup placement is valid." : string.Join(" ", warnings.Distinct(StringComparer.Ordinal)));
        result.Validate();
        return result;
    }

    private static bool IsEligibleForSlot(RosterPosition position, LineupSlot slot) =>
        slot switch
        {
            LineupSlot.Line1LW or LineupSlot.Line2LW or LineupSlot.Line3LW or LineupSlot.Line4LW => position == RosterPosition.LeftWing,
            LineupSlot.Line1C or LineupSlot.Line2C or LineupSlot.Line3C or LineupSlot.Line4C => position == RosterPosition.Center,
            LineupSlot.Line1RW or LineupSlot.Line2RW or LineupSlot.Line3RW or LineupSlot.Line4RW => position == RosterPosition.RightWing,
            LineupSlot.Pair1LD or LineupSlot.Pair1RD or LineupSlot.Pair2LD or LineupSlot.Pair2RD or LineupSlot.Pair3LD or LineupSlot.Pair3RD => position == RosterPosition.Defense,
            LineupSlot.Starter or LineupSlot.Backup => position == RosterPosition.Goalie,
            LineupSlot.HealthyScratch => true,
            _ => false
        };

    private static Lineup RebuildLineup(
        NewGmScenarioSnapshot scenario,
        Lineup existing,
        IEnumerable<LineupRoleAssignment> assignments,
        string historyEntry,
        IReadOnlyList<PlayerRolePromise>? rolePromises = null)
    {
        var unique = assignments
            .GroupBy(assignment => $"{assignment.PersonId}:{assignment.Slot}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var promises = rolePromises ?? existing.RolePromises;
        var evaluatedPromises = promises.Select(promise => EvaluatePromise(unique.FirstOrDefault(assignment => assignment.PersonId == promise.PersonId), promise)).ToArray();
        var next = new Lineup(
            existing.LineupId,
            existing.OrganizationId,
            existing.OrganizationName,
            scenario.CurrentDate,
            Enumerable.Range(1, 4)
                .Select(line => new ForwardLine(
                    line,
                    FindSlot(unique, SlotForForward(line, 0)),
                    FindSlot(unique, SlotForForward(line, 1)),
                    FindSlot(unique, SlotForForward(line, 2))))
                .ToArray(),
            Enumerable.Range(1, 3)
                .Select(pair => new DefensePair(
                    pair,
                    FindSlot(unique, SlotForDefense(pair, 0)),
                    FindSlot(unique, SlotForDefense(pair, 1))))
                .ToArray(),
            new GoalieDepth(FindSlot(unique, LineupSlot.Starter), FindSlot(unique, LineupSlot.Backup)),
            unique,
            BuildCoachRecommendations(scenario, unique),
            SummaryFor(unique))
        {
            RolePromises = evaluatedPromises,
            RoleExpectations = BuildExpectations(unique),
            Usage = BuildUsage(scenario, unique, evaluatedPromises),
            RoleHistory = existing.RoleHistory.Append(historyEntry).Distinct(StringComparer.Ordinal).ToArray()
        };
        next.Validate();
        return next;
    }

    private static LineupRoleAssignment? FindSlot(IReadOnlyList<LineupRoleAssignment> assignments, LineupSlot slot) =>
        assignments.FirstOrDefault(assignment => assignment.Slot == slot);

    private static IReadOnlyList<PlayerRoleExpectation> BuildExpectations(IReadOnlyList<LineupRoleAssignment> assignments) =>
        assignments.Select(assignment =>
        {
            var expected = ExpectedRoleFor(assignment);
            var coachRecommended = CoachRecommendedRoleFor(assignment);
            var satisfaction = SatisfactionFor(assignment.CurrentRole, expected, LineupPromiseStatus.NotYetEvaluated);
            var expectation = new PlayerRoleExpectation(
                assignment.PersonId,
                assignment.PlayerName,
                expected,
                coachRecommended,
                assignment.PotentialRole,
                satisfaction,
                $"{assignment.PlayerName} expects {LineupDisplay.Role(expected)} usage; coach recommends {LineupDisplay.Role(coachRecommended)}.");
            expectation.Validate();
            return expectation;
        }).ToArray();

    private static IReadOnlyList<PlayerLineupUsage> BuildUsage(NewGmScenarioSnapshot scenario, IReadOnlyList<LineupRoleAssignment> assignments, IReadOnlyList<PlayerRolePromise> promises) =>
        assignments.Select(assignment =>
        {
            var promise = promises.FirstOrDefault(promise => promise.PersonId == assignment.PersonId);
            var expected = ExpectedRoleFor(assignment);
            var coach = CoachRecommendedRoleFor(assignment);
            var status = promise?.Status ?? LineupPromiseStatus.NotYetEvaluated;
            var satisfaction = SatisfactionFor(assignment.CurrentRole, expected, status);
            var impact = new LineupService().BuildDevelopmentImpact(scenario with
            {
                CurrentLineup = new Lineup(
                    "lineup:usage:temp",
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    scenario.CurrentDate,
                    Enumerable.Range(1, 4).Select(line => new ForwardLine(line, null, null, null)).ToArray(),
                    Enumerable.Range(1, 3).Select(pair => new DefensePair(pair, null, null)).ToArray(),
                    new GoalieDepth(null, null),
                    assignments,
                    Array.Empty<CoachLineupRecommendation>(),
                    "Usage evaluation.")
            }, assignment.PersonId);
            var usage = new PlayerLineupUsage(
                assignment.PersonId,
                assignment.PlayerName,
                assignment.Slot,
                assignment.CurrentRole,
                expected,
                coach,
                assignment.PotentialRole,
                status,
                satisfaction,
                impact.Summary,
                MoraleNoteFor(satisfaction, status));
            usage.Validate();
            return usage;
        }).ToArray();

    private static PlayerRolePromise EvaluatePromise(LineupRoleAssignment? assignment, PlayerRolePromise promise)
    {
        var status = assignment is null
            ? LineupPromiseStatus.Broken
            : PromiseStatusFor(promise.PromisedRole, assignment.CurrentRole, assignment.Slot);
        var summary = status switch
        {
            LineupPromiseStatus.Kept => $"{promise.PlayerName}'s {LineupDisplay.Role(promise.PromisedRole)} promise is being kept.",
            LineupPromiseStatus.AtRisk => $"{promise.PlayerName}'s {LineupDisplay.Role(promise.PromisedRole)} promise is close but at risk.",
            LineupPromiseStatus.Broken => $"{promise.PlayerName}'s {LineupDisplay.Role(promise.PromisedRole)} promise is currently broken.",
            _ => "Role promise has not been evaluated yet."
        };
        var evaluated = promise with { Status = status, Summary = summary };
        evaluated.Validate();
        return evaluated;
    }

    private static LineupPromiseStatus PromiseStatusFor(LineupRole promised, LineupRole actual, LineupSlot slot)
    {
        if (slot == LineupSlot.HealthyScratch)
        {
            return LineupPromiseStatus.Broken;
        }

        var promisedTier = RoleTier(promised);
        var actualTier = RoleTier(actual);
        if (actualTier <= promisedTier)
        {
            return LineupPromiseStatus.Kept;
        }

        return actualTier == promisedTier + 1 ? LineupPromiseStatus.AtRisk : LineupPromiseStatus.Broken;
    }

    private static int RoleTier(LineupRole role) =>
        role switch
        {
            LineupRole.FranchiseForward or LineupRole.FranchiseDefenseman or LineupRole.FranchiseGoalie => 0,
            LineupRole.FirstLineForward or LineupRole.TopPairDefenseman or LineupRole.StartingGoalie => 1,
            LineupRole.TopSixForward or LineupRole.SecondPairDefenseman or LineupRole.TandemGoalie => 2,
            LineupRole.MiddleSixForward or LineupRole.CheckingLineForward or LineupRole.ThirdPairDefenseman or LineupRole.BackupGoalie => 3,
            LineupRole.FourthLineForward or LineupRole.DepthForward or LineupRole.DepthDefenseman or LineupRole.DepthGoalie => 4,
            _ => 5
        };

    private static LineupRole ExpectedRoleFor(LineupRoleAssignment assignment)
    {
        if (assignment.Age is <= 18 && assignment.PotentialRole.ToString().Contains("Prospect", StringComparison.OrdinalIgnoreCase))
        {
            return assignment.PotentialRole;
        }

        return assignment.CurrentRole switch
        {
            LineupRole.DepthForward when assignment.PotentialRole is LineupRole.TopSixForward or LineupRole.FirstLineForward => LineupRole.MiddleSixForward,
            LineupRole.DepthDefenseman when assignment.PotentialRole == LineupRole.TopPairDefenseman => LineupRole.SecondPairDefenseman,
            LineupRole.BackupGoalie when assignment.PotentialRole == LineupRole.StartingGoalie => LineupRole.TandemGoalie,
            _ => assignment.CurrentRole
        };
    }

    private static LineupRole CoachRecommendedRoleFor(LineupRoleAssignment assignment)
    {
        if (assignment.Slot == LineupSlot.HealthyScratch && assignment.PotentialRole.ToString().Contains("Prospect", StringComparison.OrdinalIgnoreCase))
        {
            return assignment.PotentialRole;
        }

        return assignment.CurrentRole;
    }

    private static LineupRoleSatisfaction SatisfactionFor(LineupRole current, LineupRole expected, LineupPromiseStatus promiseStatus)
    {
        if (promiseStatus == LineupPromiseStatus.Broken)
        {
            return LineupRoleSatisfaction.VeryFrustrated;
        }

        if (promiseStatus == LineupPromiseStatus.AtRisk)
        {
            return LineupRoleSatisfaction.Frustrated;
        }

        var gap = RoleTier(current) - RoleTier(expected);
        return gap switch
        {
            <= 0 => LineupRoleSatisfaction.Satisfied,
            1 => LineupRoleSatisfaction.Neutral,
            2 => LineupRoleSatisfaction.Frustrated,
            _ => LineupRoleSatisfaction.VeryFrustrated
        };
    }

    private static string MoraleNoteFor(LineupRoleSatisfaction satisfaction, LineupPromiseStatus promiseStatus) =>
        satisfaction switch
        {
            LineupRoleSatisfaction.Satisfied => "Player is satisfied with the current role path.",
            LineupRoleSatisfaction.Neutral => "Player is neutral; role should be monitored.",
            LineupRoleSatisfaction.Frustrated => promiseStatus == LineupPromiseStatus.AtRisk ? "Player is frustrated because a role promise is at risk." : "Player may want clearer usage.",
            _ => "Player is very frustrated; this can affect trust, confidence, and future contract interest."
        };

    private static NewGmScenarioSnapshot ApplyBrokenPromiseEffects(NewGmScenarioSnapshot scenario)
    {
        var broken = scenario.CurrentLineup?.RolePromises
            .Where(promise => promise.Status == LineupPromiseStatus.Broken)
            .ToArray() ?? Array.Empty<PlayerRolePromise>();
        foreach (var promise in broken)
        {
            scenario = new RelationshipExpansionService().RecordBrokenPromise(scenario, promise.PersonId, scenario.CurrentDate, LineupDisplay.Role(promise.PromisedRole));
        }

        return scenario;
    }

    private static LineupManagementResult Result(bool success, NewGmScenarioSnapshot scenario, LineupValidationResult validation, string message)
    {
        var result = new LineupManagementResult(success, scenario, validation, message);
        result.Validate();
        return result;
    }

    private static IReadOnlyList<CoachLineupRecommendation> BuildCoachRecommendations(NewGmScenarioSnapshot scenario, IReadOnlyList<LineupRoleAssignment> assignments)
    {
        var recommendations = new List<CoachLineupRecommendation>();
        var firstLineDepth = assignments.FirstOrDefault(assignment =>
            assignment.Slot is LineupSlot.Line1LW or LineupSlot.Line1C or LineupSlot.Line1RW
            && assignment.CurrentRole is LineupRole.DepthForward or LineupRole.FourthLineForward);
        if (firstLineDepth is not null)
        {
            recommendations.Add(Recommendation(scenario, CoachLineupRecommendationType.ImproveTopSixScoring, firstLineDepth, "Top line includes a depth-role forward.", "Review trade/free-agent options or promote a higher-skill forward.", true));
        }

        var topPairDepth = assignments.FirstOrDefault(assignment =>
            assignment.Slot is LineupSlot.Pair1LD or LineupSlot.Pair1RD
            && assignment.CurrentRole is LineupRole.DepthDefenseman or LineupRole.ThirdPairDefenseman);
        if (topPairDepth is not null)
        {
            recommendations.Add(Recommendation(scenario, CoachLineupRecommendationType.UpgradeTopPairDefense, topPairDepth, "Top pair lacks a true top-pair profile.", "Consider upgrading or sheltering this pair against hard matchups.", true));
        }

        foreach (var prospect in assignments.Where(assignment =>
            assignment.PotentialRole is LineupRole.TopSixForward or LineupRole.TopPairDefenseman or LineupRole.StartingGoalie
            && assignment.CurrentRole is LineupRole.ProspectForward or LineupRole.ProspectDefenseman or LineupRole.ProspectGoalie or LineupRole.DepthForward or LineupRole.DepthDefenseman
            && assignment.Slot == LineupSlot.HealthyScratch).Take(2))
        {
            recommendations.Add(Recommendation(scenario, CoachLineupRecommendationType.MoveProspectToBiggerRole, prospect, "High-upside prospect is outside the regular lineup.", "Find protected minutes, a call-up path, or a development assignment.", true));
        }

        foreach (var youngTopRole in assignments.Where(assignment =>
            assignment.Age is <= 18
            && assignment.Slot is LineupSlot.Line1LW or LineupSlot.Line1C or LineupSlot.Line1RW or LineupSlot.Pair1LD or LineupSlot.Pair1RD).Take(1))
        {
            recommendations.Add(Recommendation(scenario, CoachLineupRecommendationType.ShelterYoungPlayer, youngTopRole, "Young player is carrying a major lineup role.", "Keep the opportunity but monitor matchups, confidence, and fatigue.", false));
        }

        var checking = assignments.FirstOrDefault(assignment => assignment.CurrentRole == LineupRole.CheckingLineForward);
        if (checking is not null)
        {
            recommendations.Add(Recommendation(scenario, CoachLineupRecommendationType.UseCheckingLinePlayerDefensively, checking, "Checking-line profile fits defensive usage.", "Use this player for defensive-zone starts and matchup minutes.", false));
        }

        return recommendations
            .GroupBy(item => item.RecommendationId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(6)
            .ToArray();
    }

    private static CoachLineupRecommendation Recommendation(NewGmScenarioSnapshot scenario, CoachLineupRecommendationType type, LineupRoleAssignment player, string reason, string action, bool important)
    {
        var recommendation = new CoachLineupRecommendation(
            $"lineup-rec:{type}:{player.PersonId}:{scenario.CurrentDate:yyyyMMdd}",
            type,
            player.PersonId,
            player.PlayerName,
            reason,
            action,
            important);
        recommendation.Validate();
        return recommendation;
    }

    private static LineupRoleAssignment AssignmentFor(NewGmScenarioSnapshot scenario, RosterPlayer player, LineupRole role, LineupRole potentialRole, LineupSlot slot)
    {
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == player.PersonId);
        var assignment = new LineupRoleAssignment(
            player.PersonId,
            PersonName(scenario, player.PersonId),
            player.Position,
            ShootsCatches(scenario, player.PersonId),
            player.Age ?? PersonAge(scenario, player.PersonId),
            PlayerTypeFor(player, profile),
            role,
            potentialRole,
            slot,
            profile?.Stage,
            ContractStatus(scenario, player.PersonId),
            CoachNoteFor(role, potentialRole, slot, profile?.Stage));
        assignment.Validate();
        return assignment;
    }

    private static LineupRole RoleForForward(NewGmScenarioSnapshot scenario, int line, int rank)
    {
        if (scenario.LeagueProfile.Experience == LeagueExperience.Nhl)
        {
            var contender = IsContender(scenario);
            return rank switch
            {
                0 when contender => LineupRole.FranchiseForward,
                <= 2 => LineupRole.FirstLineForward,
                <= 5 => LineupRole.TopSixForward,
                <= 8 => LineupRole.MiddleSixForward,
                <= 10 => LineupRole.CheckingLineForward,
                <= 13 => LineupRole.FourthLineForward,
                _ => LineupRole.DepthForward
            };
        }

        return line switch
        {
            1 => LineupRole.TopSixForward,
            2 => LineupRole.MiddleSixForward,
            3 => LineupRole.CheckingLineForward,
            4 => LineupRole.ProspectForward,
            _ => LineupRole.DepthForward
        };
    }

    private static LineupRole RoleForDefense(NewGmScenarioSnapshot scenario, int pair, int rank)
    {
        if (scenario.LeagueProfile.Experience == LeagueExperience.Nhl)
        {
            return rank switch
            {
                0 when IsContender(scenario) => LineupRole.FranchiseDefenseman,
                <= 1 => LineupRole.TopPairDefenseman,
                <= 3 => LineupRole.SecondPairDefenseman,
                <= 5 => LineupRole.ThirdPairDefenseman,
                _ => LineupRole.DepthDefenseman
            };
        }

        return pair switch
        {
            1 => LineupRole.TopPairDefenseman,
            2 => LineupRole.SecondPairDefenseman,
            3 => LineupRole.ProspectDefenseman,
            _ => LineupRole.DepthDefenseman
        };
    }

    private static LineupRole RoleForGoalie(NewGmScenarioSnapshot scenario, int rank)
    {
        if (rank == 0)
        {
            return scenario.LeagueProfile.Experience == LeagueExperience.Nhl && IsContender(scenario)
                ? LineupRole.StartingGoalie
                : LineupRole.TandemGoalie;
        }

        return LineupRole.BackupGoalie;
    }

    private static LineupRole PotentialRoleFor(NewGmScenarioSnapshot scenario, RosterPlayer player)
    {
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == player.PersonId);
        var age = player.Age ?? PersonAge(scenario, player.PersonId);
        var currentAbility = profile?.CurrentAbility ?? 45;
        var potential = profile?.Potential ?? 52;
        var upside = potential - currentAbility;
        if (player.Position == RosterPosition.Goalie)
        {
            return potential >= 68 ? LineupRole.StartingGoalie : age <= 20 && upside >= 15 ? LineupRole.ProspectGoalie : LineupRole.BackupGoalie;
        }

        if (player.Position == RosterPosition.Defense)
        {
            return potential >= 72 ? LineupRole.TopPairDefenseman : age <= 20 && upside >= 15 ? LineupRole.ProspectDefenseman : LineupRole.SecondPairDefenseman;
        }

        return potential >= 72 ? LineupRole.FirstLineForward : age <= 20 && upside >= 15 ? LineupRole.ProspectForward : LineupRole.TopSixForward;
    }

    private static LineupRole DepthRoleFor(NewGmScenarioSnapshot scenario, RosterPlayer player)
    {
        var age = player.Age ?? PersonAge(scenario, player.PersonId);
        return player.Position switch
        {
            RosterPosition.Goalie when age <= 21 => LineupRole.ProspectGoalie,
            RosterPosition.Goalie => LineupRole.DepthGoalie,
            RosterPosition.Defense when age <= 20 => LineupRole.ProspectDefenseman,
            RosterPosition.Defense => LineupRole.DepthDefenseman,
            _ when age <= 20 => LineupRole.ProspectForward,
            _ => LineupRole.DepthForward
        };
    }

    private static RosterPlayer? TakeForward(List<RosterPlayer> players, RosterPosition? preferred = null)
    {
        if (players.Count == 0)
        {
            return null;
        }

        var index = preferred is null ? 0 : players.FindIndex(player => player.Position == preferred);
        if (index < 0)
        {
            index = 0;
        }

        var player = players[index];
        players.RemoveAt(index);
        return player;
    }

    private static RosterPlayer? TakeDefense(List<RosterPlayer> players) => TakeForward(players);

    private static RosterPlayer? TakeGoalie(List<RosterPlayer> players) => TakeForward(players);

    private static void AddIfNotNull(List<LineupRoleAssignment> assignments, LineupRoleAssignment? assignment)
    {
        if (assignment is not null)
        {
            assignments.Add(assignment);
        }
    }

    private static int PlayerScore(NewGmScenarioSnapshot scenario, RosterPlayer player)
    {
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == player.PersonId);
        var age = player.Age ?? PersonAge(scenario, player.PersonId);
        var ageFit = scenario.LeagueProfile.Experience == LeagueExperience.Nhl
            ? age is >= 24 and <= 31 ? 8 : age <= 21 ? 3 : 0
            : age <= 19 ? 5 : 1;
        return (profile?.CurrentAbility ?? 45) + ageFit + StableNumber(player.PersonId, 9);
    }

    private static bool IsForward(RosterPosition position) =>
        position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing;

    private static bool IsContender(NewGmScenarioSnapshot scenario) =>
        scenario.TeamSelection.RosterQuality.Contains("Elite", StringComparison.OrdinalIgnoreCase)
        || scenario.TeamSelection.RosterQuality.Contains("Strong", StringComparison.OrdinalIgnoreCase)
        || scenario.TeamSelection.Difficulty.Contains("contender", StringComparison.OrdinalIgnoreCase)
        || scenario.TeamSelection.CurrentStrategy.Contains("contend", StringComparison.OrdinalIgnoreCase)
        || scenario.TeamSelection.CurrentStrategy.Contains("win", StringComparison.OrdinalIgnoreCase);

    private static string SummaryFor(IReadOnlyList<LineupRoleAssignment> assignments)
    {
        var topForwards = assignments.Count(assignment => assignment.CurrentRole is LineupRole.FranchiseForward or LineupRole.FirstLineForward or LineupRole.TopSixForward);
        var topDefense = assignments.Count(assignment => assignment.CurrentRole is LineupRole.FranchiseDefenseman or LineupRole.TopPairDefenseman);
        var goalie = assignments.FirstOrDefault(assignment => assignment.Slot == LineupSlot.Starter)?.PlayerName ?? "No starter assigned";
        return $"{topForwards} top-six/top-line forward role(s), {topDefense} top-pair defense role(s), starter: {goalie}.";
    }

    private static string CoachNoteFor(LineupRole role, LineupRole potentialRole, LineupSlot slot, DevelopmentStage? stage)
    {
        if (slot == LineupSlot.HealthyScratch)
        {
            return "Needs a clearer game path; scratches should be temporary for developing players.";
        }

        if (role is LineupRole.CheckingLineForward)
        {
            return "Use in defensive and matchup minutes.";
        }

        if (role is LineupRole.FirstLineForward or LineupRole.TopSixForward or LineupRole.TopPairDefenseman or LineupRole.StartingGoalie)
        {
            return "Major role; monitor confidence, fatigue, and matchup pressure.";
        }

        if (potentialRole.ToString().Contains("Prospect", StringComparison.OrdinalIgnoreCase) || stage is DevelopmentStage.Prospect or DevelopmentStage.Junior)
        {
            return "Development role should match opportunity and confidence.";
        }

        return "Role is stable for current roster balance.";
    }

    private static string PlayerTypeFor(RosterPlayer player, PlayerDevelopmentProfile? profile)
    {
        if (player.Position == RosterPosition.Goalie)
        {
            return "Goalie";
        }

        if (player.Position == RosterPosition.Defense)
        {
            return profile?.Traits.FirstOrDefault(trait => trait.Attribute == DevelopmentAttribute.Skating)?.Value >= 62 ? "Mobile Defenseman" : "Defensive Defenseman";
        }

        var work = profile?.Traits.FirstOrDefault(trait => trait.Attribute == DevelopmentAttribute.WorkEthic)?.Value ?? 55;
        return player.Position == RosterPosition.Center
            ? work >= 64 ? "Two-Way Center" : "Playmaking Center"
            : work >= 64 ? "Checking Winger" : "Scoring Winger";
    }

    private static LineupSlot SlotForForward(int line, int position) =>
        (line, position) switch
        {
            (1, 0) => LineupSlot.Line1LW,
            (1, 1) => LineupSlot.Line1C,
            (1, 2) => LineupSlot.Line1RW,
            (2, 0) => LineupSlot.Line2LW,
            (2, 1) => LineupSlot.Line2C,
            (2, 2) => LineupSlot.Line2RW,
            (3, 0) => LineupSlot.Line3LW,
            (3, 1) => LineupSlot.Line3C,
            (3, 2) => LineupSlot.Line3RW,
            (4, 0) => LineupSlot.Line4LW,
            (4, 1) => LineupSlot.Line4C,
            _ => LineupSlot.Line4RW
        };

    private static LineupSlot SlotForDefense(int pair, int side) =>
        (pair, side) switch
        {
            (1, 0) => LineupSlot.Pair1LD,
            (1, 1) => LineupSlot.Pair1RD,
            (2, 0) => LineupSlot.Pair2LD,
            (2, 1) => LineupSlot.Pair2RD,
            (3, 0) => LineupSlot.Pair3LD,
            _ => LineupSlot.Pair3RD
        };

    private static LineupRole RoleForSlot(NewGmScenarioSnapshot scenario, RosterPlayer player, LineupSlot slot) =>
        slot switch
        {
            LineupSlot.Line1LW or LineupSlot.Line1C or LineupSlot.Line1RW => scenario.LeagueProfile.Experience == LeagueExperience.Nhl && IsContender(scenario) ? LineupRole.FirstLineForward : LineupRole.TopSixForward,
            LineupSlot.Line2LW or LineupSlot.Line2C or LineupSlot.Line2RW => LineupRole.TopSixForward,
            LineupSlot.Line3LW or LineupSlot.Line3C or LineupSlot.Line3RW => LineupRole.MiddleSixForward,
            LineupSlot.Line4LW or LineupSlot.Line4C or LineupSlot.Line4RW => (player.Age ?? PersonAge(scenario, player.PersonId)) <= 19 ? LineupRole.ProspectForward : LineupRole.FourthLineForward,
            LineupSlot.Pair1LD or LineupSlot.Pair1RD => LineupRole.TopPairDefenseman,
            LineupSlot.Pair2LD or LineupSlot.Pair2RD => LineupRole.SecondPairDefenseman,
            LineupSlot.Pair3LD or LineupSlot.Pair3RD => (player.Age ?? PersonAge(scenario, player.PersonId)) <= 20 ? LineupRole.ProspectDefenseman : LineupRole.ThirdPairDefenseman,
            LineupSlot.Starter => LineupRole.StartingGoalie,
            LineupSlot.Backup => LineupRole.BackupGoalie,
            _ => DepthRoleFor(scenario, player)
        };

    private static RosterPlayer RequireRosterPlayer(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)
        ?? scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)
        ?? throw new ArgumentException("Roster player was not found.", nameof(personId));

    private static string ContractStatus(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.PersonId == personId)
            .OrderByDescending(contract => contract.Term.EndDate)
            .Select(contract => $"{contract.Status}, expires {contract.Term.EndDate:yyyy-MM-dd}")
            .FirstOrDefault() ?? "No contract on file";

    private static string ShootsCatches(NewGmScenarioSnapshot scenario, string personId)
    {
        var board = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.ShootsCatches;
        return string.IsNullOrWhiteSpace(board) ? "Unknown" : board;
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private static int? PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate);

    private static int StableNumber(string text, int modulo)
    {
        var hash = 17;
        foreach (var character in text)
        {
            hash = hash * 31 + character;
        }

        return Math.Abs(hash) % Math.Max(1, modulo);
    }
}
