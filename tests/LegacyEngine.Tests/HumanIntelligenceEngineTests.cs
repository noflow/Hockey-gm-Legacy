using LegacyEngine.HumanIntelligence;

internal sealed class HumanIntelligenceEngineTests
{
    public void DecisionOptionCreation()
    {
        var option = Option("commit", "Commit", Factor(HumanDecisionFactorType.Reward, 80, 2, "Strong opportunity."));

        option.Validate();
        Assert.Equal("commit", option.OptionId);
        Assert.Equal(1, option.Factors.Count);
    }

    public void DecisionContextCreation()
    {
        var context = Context();

        context.Validate();
        Assert.Equal("decision-001", context.ContextId);
        Assert.Equal(60, context.Reward);
    }

    public void FactorWeighting()
    {
        var engine = new HumanIntelligenceEngine();
        var result = engine.Evaluate(
            Context(organizationFit: 90, reward: 50),
            Profile(),
            new[]
            {
                Option("fit", "Best Fit", Factor(HumanDecisionFactorType.OrganizationFit, 80, 5, "Excellent organization fit.")),
                Option("reward", "Best Reward", Factor(HumanDecisionFactorType.Reward, 95, 1, "Tempting reward."))
            });

        Assert.Equal("fit", result.SelectedOption.OptionId);
    }

    public void HighestScoredOptionSelected()
    {
        var result = new HumanIntelligenceEngine().Evaluate(
            Context(),
            Profile(),
            new[]
            {
                Option("low", "Low", Factor(HumanDecisionFactorType.Reward, 20, 1, "Low reward.")),
                Option("high", "High", Factor(HumanDecisionFactorType.Reward, 95, 1, "High reward."))
            });

        Assert.Equal("high", result.SelectedOption.OptionId);
    }

    public void RankedOptionsReturned()
    {
        var result = new HumanIntelligenceEngine().Evaluate(
            Context(),
            Profile(),
            new[]
            {
                Option("third", "Third", Factor(HumanDecisionFactorType.Reward, 20, 1, "Low reward.")),
                Option("first", "First", Factor(HumanDecisionFactorType.Reward, 90, 1, "High reward.")),
                Option("second", "Second", Factor(HumanDecisionFactorType.Reward, 60, 1, "Medium reward."))
            });

        Assert.Equal(3, result.RankedOptions.Count);
        Assert.Equal("first", result.RankedOptions[0].Option.OptionId);
        Assert.Equal("second", result.RankedOptions[1].Option.OptionId);
        Assert.Equal("third", result.RankedOptions[2].Option.OptionId);
    }

    public void PersonalityAffectsScore()
    {
        var engine = new HumanIntelligenceEngine();
        var option = Option("ambitious", "Ambitious Move", Factor(HumanDecisionFactorType.Ambition, 70, 2, "Career upside."));
        var low = engine.Evaluate(Context(), Profile(ambition: 20), new[] { option });
        var high = engine.Evaluate(Context(), Profile(ambition: 95), new[] { option });

        Assert.True(high.SelectedScore > low.SelectedScore, "Higher ambition should improve ambition-driven option score.");
    }

    public void RelationshipTrustAffectsScore()
    {
        var engine = new HumanIntelligenceEngine();
        var option = Option("trust", "Trust Person", Factor(HumanDecisionFactorType.RelationshipTrust, 60, 2, "Depends on trust."));
        var low = engine.Evaluate(Context(trust: 10), Profile(), new[] { option });
        var high = engine.Evaluate(Context(trust: 90), Profile(), new[] { option });

        Assert.True(high.SelectedScore > low.SelectedScore, "Higher trust should improve trust-driven option score.");
    }

    public void HighRiskToleranceImprovesRiskyOptionScore()
    {
        var engine = new HumanIntelligenceEngine();
        var risky = Option("risky", "Risky Prospect", Factor(HumanDecisionFactorType.Risk, 80, 3, "High variance upside."));
        var cautious = engine.Evaluate(Context(risk: 75), Profile(riskTolerance: 15), new[] { risky });
        var bold = engine.Evaluate(Context(risk: 75), Profile(riskTolerance: 95), new[] { risky });

        Assert.True(bold.SelectedScore > cautious.SelectedScore, "High risk tolerance should improve risky option score.");
    }

    public void HighLoyaltyImprovesLoyalOptionScore()
    {
        var engine = new HumanIntelligenceEngine();
        var loyal = Option("loyal", "Stay Loyal", Factor(HumanDecisionFactorType.Loyalty, 70, 3, "Honors an existing commitment."));
        var low = engine.Evaluate(Context(loyalty: 60), Profile(loyalty: 20), new[] { loyal });
        var high = engine.Evaluate(Context(loyalty: 60), Profile(loyalty: 95), new[] { loyal });

        Assert.True(high.SelectedScore > low.SelectedScore, "High loyalty should improve loyal option score.");
    }

    public void PlainLanguageReasonsReturned()
    {
        var result = new HumanIntelligenceEngine().Evaluate(
            Context(trust: 80),
            Profile(),
            new[] { Option("trust", "Trust Person", Factor(HumanDecisionFactorType.RelationshipTrust, 70, 2, "The person has earned credibility.")) });

        Assert.True(result.Reasons.Count > 0, "Decision result should include reasons.");
        Assert.True(result.Reasons[0].Text.Contains("Trust", StringComparison.OrdinalIgnoreCase), "Reason should be plain-language and mention trust.");
        Assert.True(!string.IsNullOrWhiteSpace(result.Summary), "Decision result should include summary text.");
    }

    public void DeterministicTestsAreStable()
    {
        var engine = new HumanIntelligenceEngine();
        var context = Context();
        var profile = Profile();
        var options = new[]
        {
            Option("alpha", "Alpha", Factor(HumanDecisionFactorType.Reward, 60, 2, "Good reward.")),
            Option("beta", "Beta", Factor(HumanDecisionFactorType.Reward, 60, 2, "Good reward."))
        };

        var first = engine.Evaluate(context, profile, options);
        var second = engine.Evaluate(context, profile, options);

        Assert.Equal(first.SelectedOption.OptionId, second.SelectedOption.OptionId);
        Assert.Equal(first.SelectedScore, second.SelectedScore);
    }

    public void NoUiOrGodotDependencyExists()
    {
        var files = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "HumanIntelligence"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Human Intelligence module should not reference Godot.");
            Assert.False(text.Contains("Control", StringComparison.Ordinal), "Human Intelligence module should not define UI controls.");
        }
    }

    private static HumanDecisionContext Context(
        int urgency = 50,
        int pressure = 50,
        int risk = 50,
        int reward = 60,
        int uncertainty = 40,
        int organizationFit = 55,
        int trust = 50,
        int respect = 50,
        int confidence = 50,
        int loyalty = 50) =>
        new(
            ContextId: "decision-001",
            ActorPersonId: "person-001",
            DecisionDate: new DateOnly(2026, 9, 1),
            Urgency: urgency,
            Pressure: pressure,
            Risk: risk,
            Reward: reward,
            Uncertainty: uncertainty,
            OrganizationFit: organizationFit,
            Trust: trust,
            Respect: respect,
            Confidence: confidence,
            Loyalty: loyalty);

    private static HumanIntelligenceProfile Profile(
        int ambition = 55,
        int loyalty = 55,
        int riskTolerance = 50,
        int pressureHandling = 50,
        int professionalism = 60,
        int communication = 55) =>
        new(
            PersonId: "person-001",
            Ambition: ambition,
            Loyalty: loyalty,
            RiskTolerance: riskTolerance,
            PressureHandling: pressureHandling,
            Professionalism: professionalism,
            Communication: communication);

    private static HumanDecisionOption Option(string id, string title, params HumanDecisionFactor[] factors) =>
        new(id, title, $"{title} option.", factors);

    private static HumanDecisionFactor Factor(HumanDecisionFactorType type, int score, decimal weight, string description) =>
        new(type, score, new HumanDecisionWeight(type, weight), description);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
