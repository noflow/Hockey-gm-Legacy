using LegacyEngine.Draft;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class DraftPositionValueService
{
    private readonly PositionScarcityService _scarcity = new();

    public DraftPositionValueProfile BuildPositionValueProfile(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var scarcity = scenario.PositionScarcity ?? _scarcity.BuildProfile(scenario);
        var leagueType = scenario.LeagueProfile.Rulebook.LeagueType;
        var leagueId = scenario.LeagueProfile.Identity.LeagueId;
        var theme = scenario.CurrentDraftClassProfile?.Theme ?? DraftClassTheme.BalancedClass;
        var adjustments = Enum.GetValues<DraftPositionGroup>()
            .Select(position => BuildAdjustment(position, leagueType, theme, scarcity))
            .ToArray();
        var summary = leagueType.Equals("nhl", StringComparison.OrdinalIgnoreCase)
            ? "NHL draft value applies modest center/RD premiums and discounts goalies unless the projection clearly separates."
            : "Junior draft value keeps broader positional uncertainty and discounts goalies less aggressively than NHL boards.";
        var profile = new DraftPositionValueProfile(leagueType, leagueId, adjustments, summary);
        profile.Validate();
        return profile;
    }

    public DraftBoardRealismProfile BuildRealismProfile(NewGmScenarioSnapshot scenario)
    {
        var distribution = HistoricalDraftDistributionProfile.ForLeague(
            scenario.LeagueProfile.Rulebook.LeagueType,
            scenario.LeagueProfile.Identity.LeagueId);
        var profile = new DraftBoardRealismProfile(
            distribution,
            scenario.LeagueProfile.Experience == LeagueExperience.Nhl ? 16 : 20,
            4,
            true,
            $"{distribution.LeagueLabel}: broad historical bands guide validation without hard quotas.");
        profile.Validate();
        return profile;
    }

    public IReadOnlyList<DraftPositionValueEvaluation> EvaluateBoard(NewGmScenarioSnapshot scenario, DraftPositionValueProfile? profile = null)
    {
        profile ??= BuildPositionValueProfile(scenario);
        var prepared = scenario.PlayerRatings.Count == 0 ? new PlayerRatingService().EnsureRatings(scenario) : scenario;
        return prepared.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Select(entry => EvaluateEntry(prepared, entry, profile))
            .ToArray();
    }

    public DraftPositionValueEvaluation EvaluateEntry(NewGmScenarioSnapshot scenario, DraftBoardEntry entry, DraftPositionValueProfile? profile = null)
    {
        profile ??= BuildPositionValueProfile(scenario);
        var rating = scenario.PlayerRatings.FirstOrDefault(item => item.PersonId == entry.ProspectPersonId)
            ?? new PlayerRatingService().BuildSnapshot(scenario, entry.ProspectPersonId);
        var scouted = scenario.ScoutedRatings.FirstOrDefault(item => item.PersonId == entry.ProspectPersonId);
        var curve = scenario.DevelopmentCurves.FirstOrDefault(item => item.PersonId == entry.ProspectPersonId);
        var position = DraftPositionGroupMapper.FromRosterPosition(entry.Bio?.Position ?? rating.Position);
        var adjustment = profile.For(position);
        var confidenceScore = ConfidenceScore(entry.ScoutingConfidence, scouted?.ConfidenceColor);
        var risk = RiskPenalty(entry, curve);
        var floorCeiling = CeilingFloorScore(rating, scouted);
        var value = Math.Clamp(
            rating.Overall.Midpoint * 2
            + rating.Potential.Midpoint * 4
            + floorCeiling
            + confidenceScore
            + adjustment.TotalAdjustment
            - risk,
            0,
            700);
        var name = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == entry.ProspectPersonId)?.Identity.DisplayName
            ?? entry.ProspectPersonId;
        var explanation = $"{PositionLabel(position)} context: {adjustment.Explanation} Visible OVR/POT, confidence, development curve, scarcity, theme, and risk drive the board; hidden truth is not exposed.";
        var evaluation = new DraftPositionValueEvaluation(entry.ProspectPersonId, name, position, entry.Rank, value, explanation);
        evaluation.Validate();
        return evaluation;
    }

    private static DraftPositionAdjustment BuildAdjustment(
        DraftPositionGroup position,
        string leagueType,
        DraftClassTheme theme,
        PositionScarcityProfile scarcity)
    {
        var isNhl = leagueType.Equals("nhl", StringComparison.OrdinalIgnoreCase);
        var baseAdjustment = position switch
        {
            DraftPositionGroup.Center => isNhl ? 22 : 10,
            DraftPositionGroup.Defense => isNhl ? 4 : 8,
            DraftPositionGroup.Goalie => isNhl ? -80 : -16,
            DraftPositionGroup.LeftWing or DraftPositionGroup.RightWing => isNhl ? 12 : 3,
            _ => 0
        };
        var marketPositions = position switch
        {
            DraftPositionGroup.Center => new[] { PositionMarketPosition.C },
            DraftPositionGroup.LeftWing => new[] { PositionMarketPosition.LW },
            DraftPositionGroup.RightWing => new[] { PositionMarketPosition.RW },
            DraftPositionGroup.Goalie => new[] { PositionMarketPosition.G },
            _ => new[] { PositionMarketPosition.LD, PositionMarketPosition.RD }
        };
        var scarcityAdjustment = marketPositions
            .Select(market => scarcity.For(market).ScarcityLevel switch
            {
                PositionScarcityLevel.Critical => 18,
                PositionScarcityLevel.Scarce => 12,
                PositionScarcityLevel.Thin => 6,
                PositionScarcityLevel.Oversupplied => -8,
                _ => 0
            })
            .DefaultIfEmpty(0)
            .Max();
        var themeAdjustment = theme switch
        {
            DraftClassTheme.DeepDefenseClass when position == DraftPositionGroup.Defense => isNhl ? 24 : 18,
            DraftClassTheme.DeepForwardClass when DraftPositionGroupMapper.IsForward(position) => 12,
            DraftClassTheme.StrongGoalieClass when position == DraftPositionGroup.Goalie => isNhl ? 54 : 18,
            DraftClassTheme.WeakGoalieClass when position == DraftPositionGroup.Goalie => -18,
            DraftClassTheme.WeakOverallClass => -4,
            DraftClassTheme.EliteTopEnd => 4,
            _ => 0
        };
        var riskAdjustment = position == DraftPositionGroup.Goalie
            ? isNhl ? -25 : -4
            : 0;
        var explanation = $"{PositionLabel(position)} base {baseAdjustment}, market {scarcityAdjustment}, class theme {themeAdjustment}, development risk {riskAdjustment}.";
        return new DraftPositionAdjustment(position, baseAdjustment, scarcityAdjustment, themeAdjustment, riskAdjustment, explanation);
    }

    private static int ConfidenceScore(ScoutingConfidenceLevel? confidence, PlayerRatingColor? color)
    {
        var confidencePart = confidence switch
        {
            ScoutingConfidenceLevel.VeryHigh => 28,
            ScoutingConfidenceLevel.High => 20,
            ScoutingConfidenceLevel.Medium => 10,
            ScoutingConfidenceLevel.Low => -5,
            ScoutingConfidenceLevel.Unknown or null => -12,
            _ => 0
        };
        var colorPart = color switch
        {
            PlayerRatingColor.Black => 16,
            PlayerRatingColor.Blue => 10,
            PlayerRatingColor.Green => 4,
            PlayerRatingColor.Red => -8,
            PlayerRatingColor.Unknown or null => 0,
            _ => 0
        };
        return confidencePart + colorPart;
    }

    private static int CeilingFloorScore(PlayerRatingSnapshot rating, PlayerScoutedRatings? scouted)
    {
        var overallLow = scouted?.Overall.IsUnknown == false ? scouted.Overall.Low!.Value : rating.Overall.Low;
        var overallHigh = scouted?.Overall.IsUnknown == false ? scouted.Overall.High!.Value : rating.Overall.High;
        var potentialLow = scouted?.Potential.IsUnknown == false ? scouted.Potential.Low!.Value : rating.Potential.Low;
        var potentialHigh = scouted?.Potential.IsUnknown == false ? scouted.Potential.High!.Value : rating.Potential.High;
        var overallMid = (overallLow + overallHigh) / 2;
        var potentialMid = (potentialLow + potentialHigh) / 2;
        var runway = potentialMid - overallMid;
        var rangePenalty = (overallHigh - overallLow) + (potentialHigh - potentialLow);
        return Math.Clamp(runway * 2 - rangePenalty, -35, 60);
    }

    private static int RiskPenalty(DraftBoardEntry entry, PlayerDevelopmentCurve? curve)
    {
        var risk = 0;
        if (entry.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase))
        {
            risk += 26;
        }

        if (entry.RiskSummary.Contains("character", StringComparison.OrdinalIgnoreCase)
            || entry.RiskSummary.Contains("confidence", StringComparison.OrdinalIgnoreCase))
        {
            risk += 16;
        }

        if (entry.RiskSummary.Contains("boom", StringComparison.OrdinalIgnoreCase)
            || entry.ProjectionText.Contains("high-variance", StringComparison.OrdinalIgnoreCase))
        {
            risk += 10;
        }

        if (curve is not null)
        {
            risk += curve.Variance.PlateauRisk / 8;
            risk += curve.Variance.ProbabilityMissingProjection / 10;
        }

        return risk;
    }

    public static string PositionLabel(DraftPositionGroup position) =>
        position switch
        {
            DraftPositionGroup.Center => "C",
            DraftPositionGroup.LeftWing => "LW",
            DraftPositionGroup.RightWing => "RW",
            DraftPositionGroup.Defense => "D",
            DraftPositionGroup.Goalie => "G",
            _ => position.ToString()
        };
}

public sealed class DraftBoardRealismValidator
{
    public DraftBoardValidationResult ValidateBoard(
        IReadOnlyList<DraftBoardEntry> board,
        DraftBoardRealismProfile profile,
        DraftClassProfile? classProfile = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(profile);
        var ordered = board.OrderBy(entry => entry.Rank).ToArray();
        var issues = new List<DraftBoardValidationIssue>();
        var distribution = profile.HistoricalDistribution;
        ValidateWindow(ordered.Take(5).ToArray(), distribution.TopFiveGoalies, "Top5Goalies", "top 5", issues, CountGoalies, "goalie");
        ValidateWindow(ordered.Take(10).ToArray(), distribution.TopTenGoalies, "Top10Goalies", "top 10", issues, CountGoalies, "goalie");
        var firstRound = ordered.Take(Math.Min(distribution.FirstRoundSize, ordered.Length)).ToArray();
        ValidateWindow(firstRound, distribution.FirstRoundForwards, "FirstRoundForwards", "first round", issues, CountForwards, "forward");
        ValidateWindow(firstRound, distribution.FirstRoundDefense, "FirstRoundDefense", "first round", issues, CountDefense, "defenseman");
        ValidateWindow(firstRound, distribution.FirstRoundGoalies, "FirstRoundGoalies", "first round", issues, CountGoalies, "goalie");
        var topFifty = ordered.Take(Math.Min(50, ordered.Length)).ToArray();
        ValidateWindow(topFifty, distribution.TopFiftyForwards, "Top50Forwards", "top 50", issues, CountForwards, "forward", DraftBoardValidationSeverity.Warning);
        ValidateWindow(topFifty, distribution.TopFiftyDefense, "Top50Defense", "top 50", issues, CountDefense, "defenseman", DraftBoardValidationSeverity.Warning);
        ValidateWindow(topFifty, distribution.TopFiftyGoalies, "Top50Goalies", "top 50", issues, CountGoalies, "goalie", DraftBoardValidationSeverity.Warning);
        ValidateRuns(ordered, distribution, issues);
        ValidateFirstPositionRanges(ordered, distribution, issues);
        ValidateClassCompatibility(ordered, classProfile, issues);
        if (ordered.Take(10).Select(PositionFor).Distinct().Count() < 3)
        {
            issues.Add(new DraftBoardValidationIssue(
                DraftBoardValidationSeverity.Invalid,
                "TopTenTooNarrow",
                "top 10",
                "Top ten is too concentrated; realistic boards need multiple skater groups unless talent separation is exceptional."));
        }

        if (CountForwards(topFifty) == 0 || CountDefense(topFifty) == 0 || CountGoalies(topFifty) == 0)
        {
            issues.Add(new DraftBoardValidationIssue(
                DraftBoardValidationSeverity.Warning,
                "MissingPositionGroup",
                "top 50",
                "Top 50 is missing a major position group, which should be reviewed against the class profile."));
        }

        var invalid = issues.Any(issue => issue.Severity == DraftBoardValidationSeverity.Invalid);
        var summary = invalid
            ? $"Draft board realism needs review: {issues.Count(issue => issue.Severity == DraftBoardValidationSeverity.Invalid)} invalid issue(s), {issues.Count(issue => issue.Severity == DraftBoardValidationSeverity.Warning)} warning(s)."
            : $"Draft board realism is playable: {issues.Count(issue => issue.Severity == DraftBoardValidationSeverity.Warning)} warning(s), {issues.Count(issue => issue.Severity == DraftBoardValidationSeverity.Information)} note(s).";
        var result = new DraftBoardValidationResult(!invalid, issues, summary);
        result.Validate();
        return result;
    }

    public DraftBoardRebalancingResult RebalanceBoard(
        NewGmScenarioSnapshot scenario,
        DraftBoardRealismProfile profile,
        DraftPositionValueProfile valueProfile)
    {
        var valueService = new DraftPositionValueService();
        var original = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).ToArray();
        var before = ValidateBoard(original, profile, scenario.CurrentDraftClassProfile);
        if (before.IsValid)
        {
            return Result(false, 0, Array.Empty<string>(), before, before, "Board already fits broad realism profile.");
        }

        var evaluations = original
            .Select(entry => valueService.EvaluateEntry(scenario, entry, valueProfile))
            .ToDictionary(evaluation => evaluation.ProspectPersonId, StringComparer.Ordinal);
        var current = original
            .OrderByDescending(entry => evaluations[entry.ProspectPersonId].DraftValue)
            .ThenBy(entry => entry.Rank)
            .Select((entry, index) => entry with { Rank = index + 1 })
            .ToArray();
        if (profile.PreserveEliteExceptions && scenario.CurrentDraftClassProfile?.Theme == DraftClassTheme.EliteTopEnd)
        {
            var eliteIds = original
                .OrderBy(entry => entry.Rank)
                .Take(3)
                .Select(entry => entry.ProspectPersonId)
                .ToHashSet(StringComparer.Ordinal);
            current = original
                .Where(entry => eliteIds.Contains(entry.ProspectPersonId))
                .OrderBy(entry => entry.Rank)
                .Concat(current.Where(entry => !eliteIds.Contains(entry.ProspectPersonId)))
                .Select((entry, index) => entry with { Rank = index + 1 })
                .ToArray();
        }

        var moves = new List<string>();
        var passes = 0;
        DraftBoardValidationResult after = before;
        while (passes < profile.MaximumPasses)
        {
            passes++;
            var changed = ApplyComparableRealismPass(current, evaluations, profile, moves);
            current = current.Select((entry, index) => entry with { Rank = index + 1 }).ToArray();
            after = ValidateBoard(current, profile, scenario.CurrentDraftClassProfile);
            if (after.IsValid || !changed)
            {
                break;
            }
        }

        if (!after.IsValid)
        {
            var spread = SoftDiversifyTopTen(current, evaluations, profile, moves);
            if (spread)
            {
                current = current.Select((entry, index) => entry with { Rank = index + 1 }).ToArray();
                after = ValidateBoard(current, profile, scenario.CurrentDraftClassProfile);
            }
        }

        var summary = after.IsValid
            ? $"Rebalanced among comparable prospects in {passes} pass(es); elite/value separation preserved."
            : $"Rebalance stopped after {passes} pass(es); remaining issues require talent/context review instead of forced quotas.";
        return Result(true, passes, moves, before, after, summary);
    }

    public DraftBoard ApplyRebalancedBoard(DraftBoard board, DraftBoardRebalancingResult result, NewGmScenarioSnapshot scenario)
    {
        if (!result.Rebalanced)
        {
            return board with
            {
                Entries = board.Entries
                    .OrderBy(entry => entry.Rank)
                    .ThenBy(entry => entry.ProspectPersonId, StringComparer.Ordinal)
                    .Select((entry, index) => entry with { Rank = index + 1 })
                    .ToArray()
            };
        }

        var valueService = new DraftPositionValueService();
        var profile = new DraftPositionValueService().BuildPositionValueProfile(scenario);
        var realism = new DraftPositionValueService().BuildRealismProfile(scenario);
        var evaluations = board.Entries
            .Select(entry => valueService.EvaluateEntry(scenario, entry, profile))
            .ToDictionary(evaluation => evaluation.ProspectPersonId, StringComparer.Ordinal);
        var entries = board.Entries
            .OrderByDescending(entry => evaluations[entry.ProspectPersonId].DraftValue)
            .ThenBy(entry => entry.Rank)
            .ToArray();
        var current = entries.Select((entry, index) => entry with { Rank = index + 1 }).ToArray();
        if (realism.PreserveEliteExceptions && scenario.CurrentDraftClassProfile?.Theme == DraftClassTheme.EliteTopEnd)
        {
            var eliteIds = board.Entries
                .OrderBy(entry => entry.Rank)
                .Take(3)
                .Select(entry => entry.ProspectPersonId)
                .ToHashSet(StringComparer.Ordinal);
            current = board.Entries
                .Where(entry => eliteIds.Contains(entry.ProspectPersonId))
                .OrderBy(entry => entry.Rank)
                .Concat(current.Where(entry => !eliteIds.Contains(entry.ProspectPersonId)))
                .Select((entry, index) => entry with { Rank = index + 1 })
                .ToArray();
        }

        for (var pass = 0; pass < realism.MaximumPasses; pass++)
        {
            if (!ApplyComparableRealismPass(current, evaluations, realism, new List<string>()))
            {
                break;
            }

            current = current.Select((entry, index) => entry with { Rank = index + 1 }).ToArray();
        }

        SoftDiversifyTopTen(current, evaluations, realism, new List<string>());
        return board with { Entries = current.Select((entry, index) => entry with { Rank = index + 1 }).ToArray() };
    }

    private static bool ApplyComparableRealismPass(
        DraftBoardEntry[] current,
        IReadOnlyDictionary<string, DraftPositionValueEvaluation> evaluations,
        DraftBoardRealismProfile profile,
        List<string> moves)
    {
        var changed = false;
        for (var index = 1; index < current.Length; index++)
        {
            if (!CreatesProblem(current, index, profile))
            {
                continue;
            }

            var replacement = FindComparableReplacement(current, evaluations, index, profile);
            if (replacement <= index)
            {
                continue;
            }

            var moved = current[replacement];
            for (var shift = replacement; shift > index; shift--)
            {
                current[shift] = current[shift - 1];
            }

            current[index] = moved;
            moves.Add($"Moved {evaluations[moved.ProspectPersonId].ProspectName} from #{replacement + 1} to #{index + 1} to break unrealistic position clustering.");
            changed = true;
        }

        return changed;
    }

    private static bool SoftDiversifyTopTen(
        DraftBoardEntry[] current,
        IReadOnlyDictionary<string, DraftPositionValueEvaluation> evaluations,
        DraftBoardRealismProfile profile,
        List<string> moves)
    {
        var changed = false;
        var topTen = current.Take(Math.Min(10, current.Length)).ToArray();
        if (topTen.Length < 10 || topTen.Select(PositionFor).Distinct().Count() >= 3)
        {
            return false;
        }

        var present = topTen.Select(PositionFor).ToHashSet();
        var targetIndex = Array.FindIndex(current, 10, entry => !present.Contains(PositionFor(entry))
            && IsComparable(evaluations[current[9].ProspectPersonId].DraftValue, evaluations[entry.ProspectPersonId].DraftValue, profile.ComparableValueWindow + 8));
        if (targetIndex < 0)
        {
            return false;
        }

        var moved = current[targetIndex];
        for (var shift = targetIndex; shift > 9; shift--)
        {
            current[shift] = current[shift - 1];
        }

        current[9] = moved;
        moves.Add($"Moved {evaluations[moved.ProspectPersonId].ProspectName} into the top ten to preserve a believable multi-position board.");
        changed = true;
        return changed;
    }

    private static bool CreatesProblem(DraftBoardEntry[] current, int index, DraftBoardRealismProfile profile)
    {
        var position = PositionFor(current[index]);
        if (position == DraftPositionGroup.Goalie)
        {
            var goaliesTopFive = current.Take(Math.Min(index + 1, 5)).Count(entry => PositionFor(entry) == DraftPositionGroup.Goalie);
            var goaliesTopTen = current.Take(Math.Min(index + 1, 10)).Count(entry => PositionFor(entry) == DraftPositionGroup.Goalie);
            if ((index < 5 && goaliesTopFive > profile.HistoricalDistribution.TopFiveGoalies.Maximum)
                || (index < 10 && goaliesTopTen > profile.HistoricalDistribution.TopTenGoalies.Maximum))
            {
                return true;
            }
        }

        return ConsecutiveRunLength(current, index) > (position == DraftPositionGroup.Goalie
            ? profile.HistoricalDistribution.MaximumConsecutiveGoalies
            : profile.HistoricalDistribution.MaximumConsecutiveSamePosition);
    }

    private static int FindComparableReplacement(
        DraftBoardEntry[] current,
        IReadOnlyDictionary<string, DraftPositionValueEvaluation> evaluations,
        int problemIndex,
        DraftBoardRealismProfile profile)
    {
        var problem = current[problemIndex];
        var problemPosition = PositionFor(problem);
        var problemValue = evaluations[problem.ProspectPersonId].DraftValue;
        var limit = Math.Min(current.Length, problemIndex + 24);
        for (var candidate = problemIndex + 1; candidate < limit; candidate++)
        {
            var candidatePosition = PositionFor(current[candidate]);
            if (candidatePosition == problemPosition)
            {
                continue;
            }

            var candidateValue = evaluations[current[candidate].ProspectPersonId].DraftValue;
            if (!IsComparable(problemValue, candidateValue, profile.ComparableValueWindow))
            {
                continue;
            }

            if (profile.PreserveEliteExceptions && problemValue - candidateValue > profile.ComparableValueWindow)
            {
                continue;
            }

            return candidate;
        }

        return -1;
    }

    private static bool IsComparable(int left, int right, int window) =>
        Math.Abs(left - right) <= window;

    private static int ConsecutiveRunLength(DraftBoardEntry[] current, int index)
    {
        var position = PositionFor(current[index]);
        var run = 1;
        for (var cursor = index - 1; cursor >= 0 && PositionFor(current[cursor]) == position; cursor--)
        {
            run++;
        }

        return run;
    }

    private static void ValidateWindow(
        IReadOnlyList<DraftBoardEntry> entries,
        DraftCountRange range,
        string code,
        string scope,
        List<DraftBoardValidationIssue> issues,
        Func<IReadOnlyList<DraftBoardEntry>, int> count,
        string label,
        DraftBoardValidationSeverity defaultSeverity = DraftBoardValidationSeverity.Invalid)
    {
        var value = count(entries);
        if (range.Contains(value))
        {
            return;
        }

        var severity = value > range.Maximum ? defaultSeverity : DraftBoardValidationSeverity.Warning;
        issues.Add(new DraftBoardValidationIssue(
            severity,
            code,
            scope,
            $"{scope} has {value} {label}(s); expected broad range {range.Minimum}-{range.Maximum}."));
    }

    private static void ValidateRuns(DraftBoardEntry[] ordered, HistoricalDraftDistributionProfile distribution, List<DraftBoardValidationIssue> issues)
    {
        var runPosition = ordered.Length == 0 ? DraftPositionGroup.Center : PositionFor(ordered[0]);
        var runLength = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            var position = PositionFor(ordered[index]);
            if (index == 0 || position != runPosition)
            {
                runPosition = position;
                runLength = 1;
            }
            else
            {
                runLength++;
            }

            var max = position == DraftPositionGroup.Goalie
                ? distribution.MaximumConsecutiveGoalies
                : distribution.MaximumConsecutiveSamePosition;
            if (runLength > max)
            {
                issues.Add(new DraftBoardValidationIssue(
                    DraftBoardValidationSeverity.Invalid,
                    "ConsecutiveRun",
                    $"rank {index + 1}",
                    $"{runLength} consecutive {DraftPositionValueService.PositionLabel(position)} prospects is an unrealistic run for this profile."));
                return;
            }
        }
    }

    private static void ValidateFirstPositionRanges(DraftBoardEntry[] ordered, HistoricalDraftDistributionProfile distribution, List<DraftBoardValidationIssue> issues)
    {
        FirstRange(DraftPositionGroup.Goalie, distribution.ExpectedFirstGoalieRange, "FirstGoalie", "goalie");
        FirstRange(DraftPositionGroup.Defense, distribution.ExpectedFirstDefenseRange, "FirstDefense", "defenseman");
        FirstRange(DraftPositionGroup.LeftWing, distribution.ExpectedFirstWingerRange, "FirstWinger", "winger");

        void FirstRange(DraftPositionGroup position, DraftCountRange range, string code, string label)
        {
            var first = Array.FindIndex(ordered, entry => position == DraftPositionGroup.LeftWing
                ? PositionFor(entry) is DraftPositionGroup.LeftWing or DraftPositionGroup.RightWing
                : PositionFor(entry) == position) + 1;
            if (first <= 0 || range.Contains(first))
            {
                return;
            }

            issues.Add(new DraftBoardValidationIssue(
                DraftBoardValidationSeverity.Information,
                code,
                "board",
                $"First {label} appears at #{first}; normal expected range is #{range.Minimum}-#{range.Maximum}."));
        }
    }

    private static void ValidateClassCompatibility(DraftBoardEntry[] ordered, DraftClassProfile? classProfile, List<DraftBoardValidationIssue> issues)
    {
        if (classProfile is null)
        {
            return;
        }

        var firstRound = ordered.Take(Math.Min(32, ordered.Length)).ToArray();
        if (classProfile.Theme == DraftClassTheme.DeepDefenseClass && CountDefense(firstRound) < 8)
        {
            issues.Add(new DraftBoardValidationIssue(
                DraftBoardValidationSeverity.Warning,
                "DeepDefenseUnderrepresented",
                "first round",
                "Deep defense class should lift defensemen, but not make the board all defense."));
        }

        if (classProfile.Theme == DraftClassTheme.StrongGoalieClass && CountGoalies(ordered.Take(20).ToArray()) == 0)
        {
            issues.Add(new DraftBoardValidationIssue(
                DraftBoardValidationSeverity.Warning,
                "StrongGoalieMissing",
                "top 20",
                "Strong goalie class should usually place at least one goalie in the early board."));
        }

        if (classProfile.Theme == DraftClassTheme.StrongGoalieClass && CountGoalies(ordered.Take(10).ToArray()) > 2)
        {
            issues.Add(new DraftBoardValidationIssue(
                DraftBoardValidationSeverity.Invalid,
                "StrongGoalieCluster",
                "top 10",
                "Even a strong goalie class should not create an extreme goalie cluster without exceptional separation."));
        }
    }

    private static DraftBoardRebalancingResult Result(
        bool rebalanced,
        int passes,
        IReadOnlyList<string> moves,
        DraftBoardValidationResult before,
        DraftBoardValidationResult after,
        string summary)
    {
        var result = new DraftBoardRebalancingResult(rebalanced, passes, moves, before, after, summary);
        result.Validate();
        return result;
    }

    private static int CountGoalies(IReadOnlyList<DraftBoardEntry> entries) =>
        entries.Count(entry => PositionFor(entry) == DraftPositionGroup.Goalie);

    private static int CountDefense(IReadOnlyList<DraftBoardEntry> entries) =>
        entries.Count(entry => PositionFor(entry) == DraftPositionGroup.Defense);

    private static int CountForwards(IReadOnlyList<DraftBoardEntry> entries) =>
        entries.Count(entry => DraftPositionGroupMapper.IsForward(PositionFor(entry)));

    private static DraftPositionGroup PositionFor(DraftBoardEntry entry) =>
        DraftPositionGroupMapper.FromRosterPosition(entry.Bio?.Position ?? RosterPosition.Unknown);
}
