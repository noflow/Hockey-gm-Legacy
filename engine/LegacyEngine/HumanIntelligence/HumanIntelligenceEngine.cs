namespace LegacyEngine.HumanIntelligence;

public sealed class HumanIntelligenceEngine
{
    public HumanDecisionResult Evaluate(
        HumanDecisionContext context,
        HumanIntelligenceProfile profile,
        IReadOnlyList<HumanDecisionOption> options)
    {
        context.Validate();
        profile.Validate();

        if (options.Count == 0)
        {
            throw new ArgumentException("At least one decision option is required.", nameof(options));
        }

        foreach (var option in options)
        {
            option.Validate();
        }

        var rankedOptions = options
            .Select(option => ScoreOption(context, profile, option))
            .OrderByDescending(option => option.Score)
            .ThenBy(option => option.Option.Title, StringComparer.Ordinal)
            .ThenBy(option => option.Option.OptionId, StringComparer.Ordinal)
            .ToArray();

        var selected = rankedOptions[0];
        var summary = $"Selected '{selected.Option.Title}' because it scored {selected.Score:0.##} after weighing {selected.Reasons.Count} explainable factor(s).";

        return new HumanDecisionResult(
            Context: context,
            SelectedOption: selected.Option,
            RankedOptions: rankedOptions,
            Reasons: selected.Reasons,
            Summary: summary);
    }

    private static HumanDecisionOptionScore ScoreOption(
        HumanDecisionContext context,
        HumanIntelligenceProfile profile,
        HumanDecisionOption option)
    {
        var reasons = new List<HumanDecisionReason>();
        decimal weightedTotal = 0;
        decimal totalWeight = 0;

        foreach (var factor in option.Factors)
        {
            var adjustedScore = AdjustScore(factor, context, profile);
            var adjustedWeight = AdjustWeight(factor, context, profile);
            var contribution = adjustedScore * adjustedWeight;
            weightedTotal += contribution;
            totalWeight += adjustedWeight;

            reasons.Add(new HumanDecisionReason(
                OptionId: option.OptionId,
                FactorType: factor.FactorType,
                Contribution: Math.Round(contribution, 2, MidpointRounding.AwayFromZero),
                Text: BuildReasonText(factor, adjustedScore, adjustedWeight, context, profile)));
        }

        var score = totalWeight == 0
            ? 0
            : Math.Round(weightedTotal / totalWeight, 2, MidpointRounding.AwayFromZero);

        foreach (var reason in reasons)
        {
            reason.Validate();
        }

        return new HumanDecisionOptionScore(option, score, reasons);
    }

    private static decimal AdjustScore(
        HumanDecisionFactor factor,
        HumanDecisionContext context,
        HumanIntelligenceProfile profile)
    {
        var score = factor.FactorType switch
        {
            HumanDecisionFactorType.Ambition => Blend(factor.Score, profile.Ambition, context.Reward),
            HumanDecisionFactorType.Loyalty => Blend(factor.Score, profile.Loyalty, context.Loyalty),
            HumanDecisionFactorType.Risk => Blend(factor.Score, profile.RiskTolerance, 100 - context.Risk),
            HumanDecisionFactorType.Pressure => Blend(factor.Score, profile.PressureHandling, 100 - context.Pressure),
            HumanDecisionFactorType.Professionalism => Blend(factor.Score, profile.Professionalism, 50),
            HumanDecisionFactorType.Communication => Blend(factor.Score, profile.Communication, 50),
            HumanDecisionFactorType.RelationshipTrust => Blend(factor.Score, context.Trust, profile.Communication),
            HumanDecisionFactorType.RelationshipRespect => Blend(factor.Score, context.Respect, profile.Professionalism),
            HumanDecisionFactorType.RelationshipConfidence => Blend(factor.Score, context.Confidence, profile.PressureHandling),
            HumanDecisionFactorType.RelationshipLoyalty => Blend(factor.Score, context.Loyalty, profile.Loyalty),
            HumanDecisionFactorType.Urgency => Blend(factor.Score, context.Urgency, profile.PressureHandling),
            HumanDecisionFactorType.Reward => Blend(factor.Score, context.Reward, profile.Ambition),
            HumanDecisionFactorType.Uncertainty => Blend(factor.Score, 100 - context.Uncertainty, profile.RiskTolerance),
            HumanDecisionFactorType.OrganizationFit => Blend(factor.Score, context.OrganizationFit, profile.Professionalism),
            _ => factor.Score
        };

        return Math.Clamp(score, 0, 100);
    }

    private static decimal AdjustWeight(
        HumanDecisionFactor factor,
        HumanDecisionContext context,
        HumanIntelligenceProfile profile)
    {
        var multiplier = factor.FactorType switch
        {
            HumanDecisionFactorType.Ambition => 0.75m + (profile.Ambition / 100m),
            HumanDecisionFactorType.Loyalty => 0.75m + (profile.Loyalty / 100m),
            HumanDecisionFactorType.Risk => 0.75m + (profile.RiskTolerance / 100m),
            HumanDecisionFactorType.Pressure => 0.75m + (context.Pressure / 100m),
            HumanDecisionFactorType.Professionalism => 0.75m + (profile.Professionalism / 100m),
            HumanDecisionFactorType.Communication => 0.75m + (profile.Communication / 100m),
            HumanDecisionFactorType.RelationshipTrust => 0.75m + (context.Trust / 100m),
            HumanDecisionFactorType.RelationshipRespect => 0.75m + (context.Respect / 100m),
            HumanDecisionFactorType.RelationshipConfidence => 0.75m + (context.Confidence / 100m),
            HumanDecisionFactorType.RelationshipLoyalty => 0.75m + (context.Loyalty / 100m),
            HumanDecisionFactorType.Urgency => 0.75m + (context.Urgency / 100m),
            HumanDecisionFactorType.Reward => 0.75m + (context.Reward / 100m),
            HumanDecisionFactorType.Uncertainty => 0.75m + (context.Uncertainty / 100m),
            HumanDecisionFactorType.OrganizationFit => 0.75m + (context.OrganizationFit / 100m),
            _ => 1m
        };

        return Math.Round(factor.Weight.Value * multiplier, 4, MidpointRounding.AwayFromZero);
    }

    private static string BuildReasonText(
        HumanDecisionFactor factor,
        decimal adjustedScore,
        decimal adjustedWeight,
        HumanDecisionContext context,
        HumanIntelligenceProfile profile) =>
        factor.FactorType switch
        {
            HumanDecisionFactorType.Ambition => $"{factor.Description} Ambition ({profile.Ambition}) pushed this factor to {adjustedScore:0.##}.",
            HumanDecisionFactorType.Loyalty => $"{factor.Description} Loyalty ({profile.Loyalty}) and relationship loyalty ({context.Loyalty}) shaped this score.",
            HumanDecisionFactorType.Risk => $"{factor.Description} Risk tolerance ({profile.RiskTolerance}) and context risk ({context.Risk}) produced a score of {adjustedScore:0.##}.",
            HumanDecisionFactorType.RelationshipTrust => $"{factor.Description} Trust ({context.Trust}) mattered with weight {adjustedWeight:0.##}.",
            HumanDecisionFactorType.RelationshipRespect => $"{factor.Description} Respect ({context.Respect}) mattered with weight {adjustedWeight:0.##}.",
            HumanDecisionFactorType.RelationshipConfidence => $"{factor.Description} Confidence ({context.Confidence}) mattered with weight {adjustedWeight:0.##}.",
            HumanDecisionFactorType.RelationshipLoyalty => $"{factor.Description} Relationship loyalty ({context.Loyalty}) mattered with weight {adjustedWeight:0.##}.",
            HumanDecisionFactorType.OrganizationFit => $"{factor.Description} Organization fit ({context.OrganizationFit}) supported this option.",
            HumanDecisionFactorType.Pressure => $"{factor.Description} Pressure handling ({profile.PressureHandling}) was tested by pressure ({context.Pressure}).",
            HumanDecisionFactorType.Reward => $"{factor.Description} Reward ({context.Reward}) made this more attractive.",
            _ => $"{factor.Description} This factor contributed {adjustedScore:0.##} with weight {adjustedWeight:0.##}."
        };

    private static decimal Blend(int factorScore, int firstInfluence, int secondInfluence) =>
        Math.Round((factorScore * 0.50m) + (firstInfluence * 0.30m) + (secondInfluence * 0.20m), 2, MidpointRounding.AwayFromZero);
}
