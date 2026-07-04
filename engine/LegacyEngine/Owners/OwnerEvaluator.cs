namespace LegacyEngine.Owners;

public sealed class OwnerEvaluator
{
    public OwnerEvaluationResult Evaluate(Owner owner, OwnerSeasonPerformance performance)
    {
        owner.Validate();
        performance.Validate();

        var goalScore = CalculateGoalScore(owner.Goals, performance);
        var overBudget = performance.BudgetSpent > owner.Budget.Total;
        var trustChange = ScoreToTrustChange(goalScore, overBudget);
        var confidenceChange = ScoreToConfidenceChange(goalScore, overBudget);
        var patienceChange = ScoreToPatienceChange(goalScore, overBudget);
        var projectedTrust = ClampScore(owner.Trust + trustChange);
        var projectedConfidence = ClampScore(owner.Confidence + confidenceChange);
        var projectedPatience = ClampScore(owner.Patience + patienceChange);
        var outcome = ChooseOutcome(goalScore, projectedTrust, projectedConfidence, projectedPatience);

        return new OwnerEvaluationResult(
            outcome,
            goalScore,
            trustChange,
            confidenceChange,
            patienceChange,
            BuildMessage(outcome),
            new Dictionary<string, object?>
            {
                ["budget_total"] = owner.Budget.Total,
                ["budget_spent"] = performance.BudgetSpent,
                ["over_budget"] = overBudget,
                ["projected_trust"] = projectedTrust,
                ["projected_confidence"] = projectedConfidence,
                ["projected_patience"] = projectedPatience
            });
    }

    private static decimal CalculateGoalScore(IReadOnlyList<OwnerGoal> goals, OwnerSeasonPerformance performance)
    {
        var totalWeight = goals.Sum(goal => goal.Priority);
        var completedWeight = goals
            .Where(goal => IsGoalMet(goal.GoalType, performance))
            .Sum(goal => goal.Priority);

        return totalWeight == 0 ? 0 : decimal.Round((decimal)completedWeight / totalWeight, 2);
    }

    private static bool IsGoalMet(OwnerGoalType goalType, OwnerSeasonPerformance performance) =>
        goalType switch
        {
            OwnerGoalType.MakePlayoffs => performance.MadePlayoffs,
            OwnerGoalType.WinChampionship => performance.WonChampionship,
            OwnerGoalType.DevelopProspects => performance.ProspectsDeveloped >= 2,
            OwnerGoalType.ImproveFinances => performance.FinancialTargetMet,
            OwnerGoalType.BuildCommunityTrust => performance.CommunityTrustChange > 0,
            OwnerGoalType.Rebuild => performance.ProspectsDeveloped >= 3 || performance.WinPercentage >= 0.45m,
            _ => false
        };

    private static int ScoreToTrustChange(decimal goalScore, bool overBudget) =>
        goalScore switch
        {
            >= 0.80m => overBudget ? 4 : 8,
            >= 0.50m => overBudget ? -4 : 2,
            >= 0.25m => overBudget ? -12 : -6,
            _ => overBudget ? -20 : -14
        };

    private static int ScoreToConfidenceChange(decimal goalScore, bool overBudget) =>
        goalScore switch
        {
            >= 0.80m => overBudget ? 6 : 12,
            >= 0.50m => overBudget ? -2 : 4,
            >= 0.25m => overBudget ? -14 : -8,
            _ => overBudget ? -24 : -16
        };

    private static int ScoreToPatienceChange(decimal goalScore, bool overBudget) =>
        goalScore switch
        {
            >= 0.80m => overBudget ? 0 : 4,
            >= 0.50m => overBudget ? -4 : 0,
            >= 0.25m => overBudget ? -12 : -8,
            _ => overBudget ? -22 : -16
        };

    private static OwnerEvaluationOutcome ChooseOutcome(decimal goalScore, int trust, int confidence, int patience)
    {
        if (trust <= 15 || patience <= 10 || confidence <= 10)
        {
            return OwnerEvaluationOutcome.Fired;
        }

        if (trust <= 25 || patience <= 20 || goalScore < 0.25m)
        {
            return OwnerEvaluationOutcome.FinalWarning;
        }

        if (goalScore < 0.50m || confidence <= 35)
        {
            return OwnerEvaluationOutcome.Warning;
        }

        if (goalScore >= 0.80m && trust >= 65 && confidence >= 65)
        {
            return OwnerEvaluationOutcome.Extend;
        }

        return OwnerEvaluationOutcome.Stable;
    }

    private static string BuildMessage(OwnerEvaluationOutcome outcome) =>
        outcome switch
        {
            OwnerEvaluationOutcome.Extend => "Ownership is ready to extend the GM.",
            OwnerEvaluationOutcome.Stable => "Ownership is satisfied enough to continue.",
            OwnerEvaluationOutcome.Warning => "Ownership is concerned and issues a warning.",
            OwnerEvaluationOutcome.FinalWarning => "Ownership issues a final warning.",
            OwnerEvaluationOutcome.Fired => "Ownership fires the GM.",
            _ => "Ownership completed its evaluation."
        };

    private static int ClampScore(int value) => Math.Clamp(value, 0, 100);
}
