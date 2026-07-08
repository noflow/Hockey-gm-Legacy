namespace LegacyEngine.Integration;

public sealed class LineChemistryService
{
    public NewGmScenarioSnapshot EnsureChemistry(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var lineup = scenario.CurrentLineup ?? new LineupService().BuildDefaultLineup(
            scenario,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.AlphaSnapshot.Roster.ActivePlayers);
        var report = BuildReport(scenario with { CurrentLineup = lineup });
        var updated = scenario with { CurrentLineup = lineup, CurrentLineChemistry = report };
        updated.Validate();
        return updated;
    }

    public LineChemistryReport BuildReport(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var lineup = scenario.CurrentLineup ?? new LineupService().BuildDefaultLineup(
            scenario,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.AlphaSnapshot.Roster.ActivePlayers);
        var forwards = lineup.ForwardLines.Select(line => EvaluateForwardLine(scenario, line.LineNumber, line)).ToArray();
        var defense = lineup.DefensePairs.Select(pair => EvaluateDefensePair(scenario, pair.PairNumber, pair)).ToArray();
        var goalieDepth = EvaluateGoalieDepth(scenario, lineup.Goalies);
        var overall = BuildOverall(scenario, forwards, defense, goalieDepth);
        var ranked = forwards.Concat(defense).Append(goalieDepth).OrderByDescending(unit => unit.Score.Value).ToArray();
        var majorConcerns = ranked
            .Where(unit => unit.IsMajorIssue)
            .Select(unit => $"{unit.Label}: {unit.Weaknesses.FirstOrDefault() ?? unit.Recommendation}")
            .Take(4)
            .ToArray();
        var coachRecommendations = ranked
            .Where(unit => unit.Score.Grade is LineChemistryGrade.Poor or LineChemistryGrade.Problem or LineChemistryGrade.Neutral)
            .OrderBy(unit => unit.Score.Value)
            .Select(unit => unit.Recommendation)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();
        var history = ranked
            .Where(unit => unit.Score.Grade is LineChemistryGrade.Excellent or LineChemistryGrade.Problem)
            .Select(unit => $"{scenario.CurrentDate:yyyy-MM-dd}: {unit.Label} chemistry rated {unit.Score.Grade}.")
            .Take(4)
            .ToArray();

        var report = new LineChemistryReport(
            $"line-chemistry:{scenario.Organization.OrganizationId}:{scenario.CurrentDate:yyyyMMdd}",
            scenario.Organization.OrganizationId,
            scenario.CurrentDate,
            overall,
            forwards,
            defense,
            goalieDepth,
            ranked.FirstOrDefault()?.Label ?? "No line available",
            ranked.LastOrDefault()?.Label ?? "No line available",
            majorConcerns.Length == 0 ? new[] { "No major chemistry issues today." } : majorConcerns,
            coachRecommendations.Length == 0 ? new[] { "No urgent chemistry changes recommended." } : coachRecommendations,
            history.Length == 0 ? new[] { $"{scenario.CurrentDate:yyyy-MM-dd}: Team chemistry reviewed." } : history);
        report.Validate();
        return report;
    }

    public LineChemistry EvaluateForwardLine(NewGmScenarioSnapshot scenario, int lineNumber, ForwardLine line)
    {
        var players = new[] { line.LeftWing, line.Center, line.RightWing }.Where(player => player is not null).Select(player => player!).ToArray();
        var factors = new List<LineChemistryFactor>();
        AddForwardTypeFit(factors, lineNumber, players);
        AddHandednessFit(factors, players, isDefensePair: false);
        AddPositionFit(factors, players);
        AddRoleFit(factors, lineNumber, players);
        AddAgeExperienceFit(factors, players);
        AddPersonalityFit(factors, players);
        AddRelationshipFit(factors, scenario, players);
        AddCoachFit(factors, scenario, players, lineNumber);
        AddMoraleFit(factors, scenario, players);
        AddPromiseFit(factors, scenario, players);
        factors.Add(new(LineChemistryFactorType.RecentPerformancePlaceholder, 0, "Recent performance will be folded in later; v1 keeps this neutral."));
        return BuildUnit(
            $"forward-line:{lineNumber}",
            LineChemistryUnitType.ForwardLine,
            $"Line {lineNumber}",
            players,
            factors,
            RecommendationForForward(lineNumber, players, factors),
            DevelopmentNoteFor(players),
            RelationshipNoteFor(factors),
            PromiseNoteFor(factors));
    }

    public LineChemistry EvaluateDefensePair(NewGmScenarioSnapshot scenario, int pairNumber, DefensePair pair)
    {
        var players = new[] { pair.LeftDefense, pair.RightDefense }.Where(player => player is not null).Select(player => player!).ToArray();
        var factors = new List<LineChemistryFactor>();
        AddDefenseTypeFit(factors, players);
        AddHandednessFit(factors, players, isDefensePair: true);
        AddPositionFit(factors, players);
        AddRoleFit(factors, pairNumber, players);
        AddAgeExperienceFit(factors, players);
        AddPersonalityFit(factors, players);
        AddRelationshipFit(factors, scenario, players);
        AddCoachFit(factors, scenario, players, pairNumber);
        AddMoraleFit(factors, scenario, players);
        AddPromiseFit(factors, scenario, players);
        factors.Add(new(LineChemistryFactorType.RecentPerformancePlaceholder, 0, "Recent performance will be folded in later; v1 keeps this neutral."));
        return BuildUnit(
            $"defense-pair:{pairNumber}",
            LineChemistryUnitType.DefensePair,
            $"Pair {pairNumber}",
            players,
            factors,
            RecommendationForDefense(pairNumber, players, factors),
            DevelopmentNoteFor(players),
            RelationshipNoteFor(factors),
            PromiseNoteFor(factors));
    }

    public LineChemistry EvaluateGoalieDepth(NewGmScenarioSnapshot scenario, GoalieDepth depth)
    {
        var players = new[] { depth.Starter, depth.Backup }.Where(player => player is not null).Select(player => player!).ToArray();
        var factors = new List<LineChemistryFactor>
        {
            new(LineChemistryFactorType.PlayerTypeFit, players.Length == 2 ? 5 : -8, players.Length == 2 ? "Starter and backup roles are both covered." : "Goalie room is missing depth."),
            new(LineChemistryFactorType.RoleFit, depth.Starter is not null && depth.Backup is not null ? 4 : -8, depth.Starter is not null && depth.Backup is not null ? "Starter/backup structure is clear." : "Goalie roles are unclear."),
            new(LineChemistryFactorType.CoachPhilosophyFit, 2, "Goalie rotation is simple in v1; coach fit is neutral-positive.")
        };
        AddAgeExperienceFit(factors, players);
        AddRelationshipFit(factors, scenario, players);
        AddMoraleFit(factors, scenario, players);
        AddPromiseFit(factors, scenario, players);
        factors.Add(new(LineChemistryFactorType.RecentPerformancePlaceholder, 0, "Starter workload and recent form are placeholders for later."));
        return BuildUnit(
            "goalie-depth",
            LineChemistryUnitType.GoalieDepth,
            "Goalie Room",
            players,
            factors,
            players.Any(player => player.Age is <= 21) ? "Use veteran support and avoid overloading the young goalie." : "Keep the starter workload clear and maintain backup trust.",
            DevelopmentNoteFor(players),
            RelationshipNoteFor(factors),
            PromiseNoteFor(factors));
    }

    public LineChemistry? FindChemistryForPerson(LineChemistryReport report, string personId) =>
        report.Units
            .Where(unit => unit.UnitType != LineChemistryUnitType.Team)
            .FirstOrDefault(unit => unit.PlayerIds.Contains(personId, StringComparer.Ordinal));

    private static LineChemistry BuildOverall(NewGmScenarioSnapshot scenario, IReadOnlyList<LineChemistry> forwards, IReadOnlyList<LineChemistry> defense, LineChemistry goalie)
    {
        var all = forwards.Concat(defense).Append(goalie).ToArray();
        var average = all.Length == 0 ? 50 : (int)Math.Round(all.Average(unit => unit.Score.Value));
        var factors = new List<LineChemistryFactor>
        {
            new(LineChemistryFactorType.PlayerTypeFit, 0, $"Forward chemistry average: {AverageGrade(forwards)}."),
            new(LineChemistryFactorType.RoleFit, 0, $"Defense chemistry average: {AverageGrade(defense)}."),
            new(LineChemistryFactorType.CoachPhilosophyFit, 0, $"Goalie room: {goalie.Score.Grade}."),
            new(LineChemistryFactorType.PromiseSatisfaction, all.Any(unit => unit.RolePromiseNote.Contains("broken", StringComparison.OrdinalIgnoreCase)) ? -6 : 2, all.Any(unit => unit.RolePromiseNote.Contains("broken", StringComparison.OrdinalIgnoreCase)) ? "A broken role promise is affecting chemistry." : "No broken role promise is driving team chemistry today.")
        };
        var score = Score(average + factors.Sum(factor => factor.Modifier));
        var strengths = all.OrderByDescending(unit => unit.Score.Value).Take(2).Select(unit => $"{unit.Label}: {unit.Score.Grade}").ToArray();
        var weaknesses = all.Where(unit => unit.IsMajorIssue).Select(unit => $"{unit.Label}: {unit.Weaknesses.FirstOrDefault() ?? unit.Recommendation}").Take(3).ToArray();
        var overall = new LineChemistry(
            "team-chemistry",
            LineChemistryUnitType.Team,
            "Team Chemistry",
            score,
            Array.Empty<string>(),
            Array.Empty<string>(),
            factors,
            strengths.Length == 0 ? new[] { "Roster balance is still being evaluated." } : strengths,
            weaknesses.Length == 0 ? new[] { "No major chemistry concern." } : weaknesses,
            score.Grade is LineChemistryGrade.Poor or LineChemistryGrade.Problem ? "Review the weakest line before advancing." : "Keep monitoring chemistry as roles and performance settle.",
            "Team chemistry creates a small development environment modifier only.",
            scenario.RelationshipChemistry is null ? "Relationship chemistry is neutral." : $"Relationship chemistry: roster {scenario.RelationshipChemistry.RosterChemistry}, staff {scenario.RelationshipChemistry.StaffChemistry}.",
            "Role promises are monitored through lineup usage.");
        overall.Validate();
        return overall;
    }

    private static void AddForwardTypeFit(List<LineChemistryFactor> factors, int lineNumber, IReadOnlyList<LineupRoleAssignment> players)
    {
        var types = players.Select(player => player.PlayerType).ToArray();
        var hasPlaymaker = types.Any(ContainsPlaymaker);
        var hasShooter = types.Any(ContainsShooter);
        var hasPower = types.Any(ContainsPower);
        var hasChecker = types.Any(ContainsChecking);
        var duplicateScorers = types.Count(ContainsShooter) >= 3;
        if (hasPlaymaker && hasShooter && hasPower)
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, 14, "Playmaker, shooter, and power-forward traits create a clean attacking identity."));
        }
        else if (duplicateScorers)
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, -10, "Too many similar shooters can leave the line short on puck distribution and retrieval."));
        }
        else if (lineNumber <= 2 && !hasPlaymaker)
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, -6, "Scoring line lacks an obvious playmaker."));
        }
        else if (lineNumber >= 3 && hasChecker)
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, 8, "Checking-line identity fits a lower-line role."));
        }
        else
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, 2, "Player types are workable but not a standout blend."));
        }
    }

    private static void AddDefenseTypeFit(List<LineChemistryFactor> factors, IReadOnlyList<LineupRoleAssignment> players)
    {
        var mobile = players.Count(player => player.PlayerType.Contains("Mobile", StringComparison.OrdinalIgnoreCase) || player.PlayerType.Contains("Offensive", StringComparison.OrdinalIgnoreCase));
        var defensive = players.Count(player => player.PlayerType.Contains("Defensive", StringComparison.OrdinalIgnoreCase) || player.PlayerType.Contains("Physical", StringComparison.OrdinalIgnoreCase));
        if (mobile > 0 && defensive > 0)
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, 12, "Offensive/mobile and defensive traits balance the pair."));
        }
        else if (mobile >= 2)
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, -8, "Two high-risk puck movers may need a steadier partner."));
        }
        else
        {
            factors.Add(new(LineChemistryFactorType.PlayerTypeFit, 2, "Defense-pair type balance is acceptable."));
        }
    }

    private static void AddHandednessFit(List<LineChemistryFactor> factors, IReadOnlyList<LineupRoleAssignment> players, bool isDefensePair)
    {
        var hands = players.Select(player => player.ShootsCatches).Where(hand => !string.IsNullOrWhiteSpace(hand) && hand != "Unknown").ToArray();
        if (hands.Length < 2)
        {
            factors.Add(new(LineChemistryFactorType.HandednessBalance, 0, "Handedness information is incomplete."));
            return;
        }

        var hasLeft = hands.Any(hand => hand.Contains("L", StringComparison.OrdinalIgnoreCase));
        var hasRight = hands.Any(hand => hand.Contains("R", StringComparison.OrdinalIgnoreCase));
        factors.Add(hasLeft && hasRight
            ? new LineChemistryFactor(LineChemistryFactorType.HandednessBalance, isDefensePair ? 7 : 4, isDefensePair ? "Left/right defensive balance helps exits and retrievals." : "Mixed handedness gives the line more puck-skill options.")
            : new LineChemistryFactor(LineChemistryFactorType.HandednessBalance, isDefensePair ? -4 : -2, "Same-handed group is workable but less balanced."));
    }

    private static void AddPositionFit(List<LineChemistryFactor> factors, IReadOnlyList<LineupRoleAssignment> players)
    {
        var outOfPosition = players.Count(player => !PositionMatchesSlot(player));
        factors.Add(outOfPosition == 0
            ? new LineChemistryFactor(LineChemistryFactorType.PositionFit, 6, "Everyone is in a natural lineup position.")
            : new LineChemistryFactor(LineChemistryFactorType.PositionFit, -8 * outOfPosition, $"{outOfPosition} player(s) are outside their natural lineup position."));
    }

    private static void AddRoleFit(List<LineChemistryFactor> factors, int groupNumber, IReadOnlyList<LineupRoleAssignment> players)
    {
        var pressuredProspect = players.Any(player => player.Age is <= 19 && groupNumber == 1 && player.CurrentRole.ToString().Contains("Prospect", StringComparison.OrdinalIgnoreCase));
        var topRoles = players.Count(player => player.CurrentRole is LineupRole.FranchiseForward or LineupRole.FirstLineForward or LineupRole.TopSixForward or LineupRole.TopPairDefenseman or LineupRole.StartingGoalie);
        if (pressuredProspect)
        {
            factors.Add(new(LineChemistryFactorType.RoleFit, -8, "Young prospect may be carrying too much role pressure."));
        }
        else if (topRoles > 0 && groupNumber <= 2)
        {
            factors.Add(new(LineChemistryFactorType.RoleFit, 5, "Assigned roles fit the line/pair usage."));
        }
        else
        {
            factors.Add(new(LineChemistryFactorType.RoleFit, 1, "Role fit is neutral."));
        }
    }

    private static void AddAgeExperienceFit(List<LineChemistryFactor> factors, IReadOnlyList<LineupRoleAssignment> players)
    {
        var hasYoung = players.Any(player => player.Age is <= 20);
        var hasVeteran = players.Any(player => player.Age is >= 23);
        if (hasYoung && hasVeteran)
        {
            factors.Add(new(LineChemistryFactorType.AgeExperienceMix, 7, "Veteran/young mix can create a useful mentorship environment."));
        }
        else if (players.Count > 1 && players.All(player => player.Age is <= 20))
        {
            factors.Add(new(LineChemistryFactorType.AgeExperienceMix, -4, "Very young group may need steadier support."));
        }
        else
        {
            factors.Add(new(LineChemistryFactorType.AgeExperienceMix, 1, "Age/experience mix is stable."));
        }
    }

    private static void AddPersonalityFit(List<LineChemistryFactor> factors, IReadOnlyList<LineupRoleAssignment> players)
    {
        var score = players.Sum(player => StableNumber(player.PersonId + player.PlayerType, 7)) - Math.Max(0, players.Count - 1) * 3;
        factors.Add(new(LineChemistryFactorType.PersonalityFit, Math.Clamp(score, -4, 5), "Personality blend is estimated from known profiles and remains a small modifier."));
    }

    private static void AddRelationshipFit(List<LineChemistryFactor> factors, NewGmScenarioSnapshot scenario, IReadOnlyList<LineupRoleAssignment> players)
    {
        var scores = new List<int>();
        for (var i = 0; i < players.Count; i++)
        {
            for (var j = i + 1; j < players.Count; j++)
            {
                var relationship = scenario.RelationshipProfiles.FirstOrDefault(profile =>
                    profile.RelationshipType == ExpandedRelationshipType.PlayerPlayer
                    && ((profile.SourceId == players[i].PersonId && profile.TargetId == players[j].PersonId)
                        || (profile.SourceId == players[j].PersonId && profile.TargetId == players[i].PersonId)));
                if (relationship is not null)
                {
                    scores.Add(relationship.OverallScore);
                }
            }
        }

        if (scores.Count == 0)
        {
            factors.Add(new(LineChemistryFactorType.RelationshipFit, 0, "No strong player-player relationship read yet."));
            return;
        }

        var average = (int)Math.Round(scores.Average());
        factors.Add(average >= 70
            ? new LineChemistryFactor(LineChemistryFactorType.RelationshipFit, 7, "Strong relationships support communication on this unit.")
            : average < 40
                ? new LineChemistryFactor(LineChemistryFactorType.RelationshipFit, -10, "Poor relationship fit is dragging down chemistry.")
                : new LineChemistryFactor(LineChemistryFactorType.RelationshipFit, 0, "Relationship fit is neutral."));
    }

    private static void AddCoachFit(List<LineChemistryFactor> factors, NewGmScenarioSnapshot scenario, IReadOnlyList<LineupRoleAssignment> players, int groupNumber)
    {
        var strategy = scenario.TeamSelection.CurrentStrategy;
        var contender = strategy.Contains("win", StringComparison.OrdinalIgnoreCase) || strategy.Contains("contend", StringComparison.OrdinalIgnoreCase);
        var development = players.Any(player => player.Age is <= 20);
        var modifier = development && !contender ? 5 : development && groupNumber == 1 ? -3 : 2;
        factors.Add(new(LineChemistryFactorType.CoachPhilosophyFit, modifier, development ? "Coach fit accounts for development pressure and role usage." : "Coach philosophy fit is stable."));
    }

    private static void AddMoraleFit(List<LineChemistryFactor> factors, NewGmScenarioSnapshot scenario, IReadOnlyList<LineupRoleAssignment> players)
    {
        var usages = scenario.CurrentLineup?.Usage.Where(usage => players.Any(player => player.PersonId == usage.PersonId)).ToArray() ?? Array.Empty<PlayerLineupUsage>();
        var frustrated = usages.Count(usage => usage.Satisfaction is LineupRoleSatisfaction.Frustrated or LineupRoleSatisfaction.VeryFrustrated);
        factors.Add(frustrated == 0
            ? new LineChemistryFactor(LineChemistryFactorType.MoraleConfidence, 3, "Role satisfaction is not hurting the unit.")
            : new LineChemistryFactor(LineChemistryFactorType.MoraleConfidence, -6 * frustrated, $"{frustrated} player(s) are frustrated with role usage."));
    }

    private static void AddPromiseFit(List<LineChemistryFactor> factors, NewGmScenarioSnapshot scenario, IReadOnlyList<LineupRoleAssignment> players)
    {
        var promises = scenario.CurrentLineup?.RolePromises.Where(promise => players.Any(player => player.PersonId == promise.PersonId)).ToArray() ?? Array.Empty<PlayerRolePromise>();
        var broken = promises.Count(promise => promise.Status == LineupPromiseStatus.Broken);
        var atRisk = promises.Count(promise => promise.Status == LineupPromiseStatus.AtRisk);
        if (broken > 0)
        {
            factors.Add(new(LineChemistryFactorType.PromiseSatisfaction, -12, "Broken role promise is damaging line trust."));
        }
        else if (atRisk > 0)
        {
            factors.Add(new(LineChemistryFactorType.PromiseSatisfaction, -6, "Role promise is at risk and should be monitored."));
        }
        else
        {
            factors.Add(new(LineChemistryFactorType.PromiseSatisfaction, promises.Length > 0 ? 4 : 1, promises.Length > 0 ? "Role promises are currently being respected." : "No explicit role promise affects this unit."));
        }
    }

    private static LineChemistry BuildUnit(
        string unitId,
        LineChemistryUnitType unitType,
        string label,
        IReadOnlyList<LineupRoleAssignment> players,
        IReadOnlyList<LineChemistryFactor> factors,
        string recommendation,
        string developmentNote,
        string relationshipNote,
        string promiseNote)
    {
        var score = Score(50 + factors.Sum(factor => factor.Modifier));
        var strengths = factors.Where(factor => factor.Modifier > 0).OrderByDescending(factor => factor.Modifier).Select(factor => factor.Summary).Take(3).ToArray();
        var weaknesses = factors.Where(factor => factor.Modifier < 0).OrderBy(factor => factor.Modifier).Select(factor => factor.Summary).Take(3).ToArray();
        var chemistry = new LineChemistry(
            unitId,
            unitType,
            label,
            score,
            players.Select(player => player.PersonId).ToArray(),
            players.Select(player => player.PlayerName).ToArray(),
            factors,
            strengths.Length == 0 ? new[] { "No standout chemistry strength yet." } : strengths,
            weaknesses.Length == 0 ? new[] { "No major chemistry weakness." } : weaknesses,
            recommendation,
            developmentNote,
            relationshipNote,
            promiseNote);
        chemistry.Validate();
        return chemistry;
    }

    private static LineChemistryScore Score(int value)
    {
        var clamped = Math.Clamp(value, 0, 100);
        var grade = clamped >= 78 ? LineChemistryGrade.Excellent :
            clamped >= 63 ? LineChemistryGrade.Good :
            clamped >= 45 ? LineChemistryGrade.Neutral :
            clamped >= 30 ? LineChemistryGrade.Poor :
            LineChemistryGrade.Problem;
        var score = new LineChemistryScore(clamped, grade, grade switch
        {
            LineChemistryGrade.Excellent => "78-100",
            LineChemistryGrade.Good => "63-77",
            LineChemistryGrade.Neutral => "45-62",
            LineChemistryGrade.Poor => "30-44",
            _ => "0-29"
        });
        score.Validate();
        return score;
    }

    private static string RecommendationForForward(int lineNumber, IReadOnlyList<LineupRoleAssignment> players, IReadOnlyList<LineChemistryFactor> factors)
    {
        if (factors.Any(factor => factor.Summary.Contains("lacks an obvious playmaker", StringComparison.OrdinalIgnoreCase)))
        {
            return $"Move a playmaker to Line {lineNumber} or change one winger's role.";
        }

        if (factors.Any(factor => factor.Summary.Contains("similar shooters", StringComparison.OrdinalIgnoreCase)))
        {
            return "Split two similar shooters or add a puck-distributor/retrieval player.";
        }

        if (factors.Any(factor => factor.Summary.Contains("Young prospect", StringComparison.OrdinalIgnoreCase)))
        {
            return "Reduce prospect pressure or add veteran support.";
        }

        return lineNumber >= 3 ? "Use this line in a clear checking/development role." : "Keep monitoring this line as performance data arrives.";
    }

    private static string RecommendationForDefense(int pairNumber, IReadOnlyList<LineupRoleAssignment> players, IReadOnlyList<LineChemistryFactor> factors)
    {
        if (factors.Any(factor => factor.Summary.Contains("high-risk puck movers", StringComparison.OrdinalIgnoreCase)))
        {
            return $"Pair one offensive defenseman on Pair {pairNumber} with a steadier defensive partner.";
        }

        if (factors.Any(factor => factor.Summary.Contains("young", StringComparison.OrdinalIgnoreCase)))
        {
            return "Pair the young defenseman with a veteran mentor when possible.";
        }

        return "Maintain this pair and revisit after more games.";
    }

    private static string DevelopmentNoteFor(IReadOnlyList<LineupRoleAssignment> players)
    {
        var young = players.FirstOrDefault(player => player.Age is <= 20);
        var veteran = players.FirstOrDefault(player => player.Age is >= 23);
        if (young is not null && veteran is not null)
        {
            return $"{young.PlayerName} may benefit from veteran support beside {veteran.PlayerName}.";
        }

        return players.Any(player => player.Age is <= 20)
            ? "Young players need clear minutes and confidence support in this unit."
            : "Development environment is stable; no large chemistry modifier applied.";
    }

    private static string RelationshipNoteFor(IReadOnlyList<LineChemistryFactor> factors) =>
        factors.FirstOrDefault(factor => factor.FactorType == LineChemistryFactorType.RelationshipFit)?.Summary ?? "Relationship fit is neutral.";

    private static string PromiseNoteFor(IReadOnlyList<LineChemistryFactor> factors) =>
        factors.FirstOrDefault(factor => factor.FactorType == LineChemistryFactorType.PromiseSatisfaction)?.Summary ?? "No role promise issue.";

    private static string AverageGrade(IReadOnlyList<LineChemistry> units)
    {
        if (units.Count == 0)
        {
            return "not available";
        }

        return Score((int)Math.Round(units.Average(unit => unit.Score.Value))).Grade.ToString();
    }

    private static bool PositionMatchesSlot(LineupRoleAssignment player) =>
        player.Slot switch
        {
            LineupSlot.Line1LW or LineupSlot.Line2LW or LineupSlot.Line3LW or LineupSlot.Line4LW => player.Position == LegacyEngine.Rosters.RosterPosition.LeftWing,
            LineupSlot.Line1C or LineupSlot.Line2C or LineupSlot.Line3C or LineupSlot.Line4C => player.Position == LegacyEngine.Rosters.RosterPosition.Center,
            LineupSlot.Line1RW or LineupSlot.Line2RW or LineupSlot.Line3RW or LineupSlot.Line4RW => player.Position == LegacyEngine.Rosters.RosterPosition.RightWing,
            LineupSlot.Pair1LD or LineupSlot.Pair1RD or LineupSlot.Pair2LD or LineupSlot.Pair2RD or LineupSlot.Pair3LD or LineupSlot.Pair3RD => player.Position == LegacyEngine.Rosters.RosterPosition.Defense,
            LineupSlot.Starter or LineupSlot.Backup => player.Position == LegacyEngine.Rosters.RosterPosition.Goalie,
            _ => true
        };

    private static bool ContainsPlaymaker(string text) =>
        text.Contains("Playmaking", StringComparison.OrdinalIgnoreCase) || text.Contains("Playmaker", StringComparison.OrdinalIgnoreCase) || text.Contains("Two-Way Center", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsShooter(string text) =>
        text.Contains("Scoring", StringComparison.OrdinalIgnoreCase) || text.Contains("Shooter", StringComparison.OrdinalIgnoreCase) || text.Contains("Sniper", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPower(string text) =>
        text.Contains("Power", StringComparison.OrdinalIgnoreCase) || text.Contains("Physical", StringComparison.OrdinalIgnoreCase) || text.Contains("Checking", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsChecking(string text) =>
        text.Contains("Checking", StringComparison.OrdinalIgnoreCase) || text.Contains("Defensive", StringComparison.OrdinalIgnoreCase);

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
