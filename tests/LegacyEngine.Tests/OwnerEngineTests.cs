using LegacyEngine.Owners;

internal sealed class OwnerEngineTests
{
    public void OwnerArchetypeProfiles()
    {
        var archetypes = Enum.GetValues<OwnerArchetype>();

        Assert.Equal(6, archetypes.Length);
        Assert.Equal(OwnerAutonomyLevel.High, OwnerArchetypeProfile.For(OwnerArchetype.Builder).DefaultAutonomyLevel);
        Assert.Equal(OwnerAutonomyLevel.Normal, OwnerArchetypeProfile.For(OwnerArchetype.Competitor).DefaultAutonomyLevel);
        Assert.Equal(OwnerAutonomyLevel.Normal, OwnerArchetypeProfile.For(OwnerArchetype.CommunityOwner).DefaultAutonomyLevel);
        Assert.Equal(OwnerAutonomyLevel.Low, OwnerArchetypeProfile.For(OwnerArchetype.Investor).DefaultAutonomyLevel);
        Assert.Equal(OwnerAutonomyLevel.Normal, OwnerArchetypeProfile.For(OwnerArchetype.Traditionalist).DefaultAutonomyLevel);
        Assert.Equal(OwnerAutonomyLevel.FullHockeyControl, OwnerArchetypeProfile.For(OwnerArchetype.Innovator).DefaultAutonomyLevel);
    }

    public void OwnerModelValidation()
    {
        var owner = BuildOwner();

        owner.Validate();
        Assert.Equal(OwnerAutonomyLevel.High, owner.AutonomyLevel);
        Assert.Equal(4_250_000m, owner.Budget.Total);
        Assert.True(owner.Budget.CanFund(4_000_000m), "Budget should fund amounts inside the approved total.");
        Assert.False(owner.Budget.CanFund(4_500_000m), "Budget should reject amounts beyond the approved total.");

        var badBudgetOwner = owner with { Budget = owner.Budget with { Scouting = -1 } };
        Assert.Throws<ArgumentOutOfRangeException>(badBudgetOwner.Validate);

        var badGoalOwner = owner with { Goals = new[] { new OwnerGoal(OwnerGoalType.MakePlayoffs, 0, "Bad priority") } };
        Assert.Throws<ArgumentOutOfRangeException>(badGoalOwner.Validate);

        var badScoreOwner = owner with { Trust = 101 };
        Assert.Throws<ArgumentOutOfRangeException>(badScoreOwner.Validate);
    }

    public void OwnerAssignment()
    {
        var owner = BuildOwner(organizationId: null);
        var assignedOwner = owner.AssignToOrganization("halifax-juniors");

        Assert.Equal("halifax-juniors", assignedOwner.OrganizationId);
        Assert.Equal<string?>(null, owner.OrganizationId);
    }

    public void OwnerEvaluationExtendsGm()
    {
        var result = new OwnerEvaluator().Evaluate(
            BuildOwner(trust: 70, confidence: 70, patience: 70),
            new OwnerSeasonPerformance(
                WinPercentage: 0.70m,
                MadePlayoffs: true,
                WonChampionship: false,
                ProspectsDeveloped: 3,
                FinancialTargetMet: true,
                CommunityTrustChange: 4,
                BudgetSpent: 4_000_000m));

        Assert.Equal(OwnerEvaluationOutcome.Extend, result.Outcome);
        Assert.Equal(1m, result.GoalScore);
        Assert.True(result.TrustChange > 0, "Strong seasons should increase trust.");
        Assert.True(result.ConfidenceChange > 0, "Strong seasons should increase confidence.");
    }

    public void OwnerEvaluationConsequences()
    {
        var evaluator = new OwnerEvaluator();
        var owner = BuildOwner(trust: 45, confidence: 45, patience: 45);
        var poorSeason = new OwnerSeasonPerformance(
            WinPercentage: 0.25m,
            MadePlayoffs: false,
            WonChampionship: false,
            ProspectsDeveloped: 0,
            FinancialTargetMet: false,
            CommunityTrustChange: -5,
            BudgetSpent: 4_000_000m);

        Assert.Equal(OwnerEvaluationOutcome.FinalWarning, evaluator.Evaluate(owner, poorSeason).Outcome);

        var warningOwner = owner with { Goals = new[] { new OwnerGoal(OwnerGoalType.MakePlayoffs, 5, "Make the playoffs.") } };
        var nearMissSeason = poorSeason with { MadePlayoffs = false, ProspectsDeveloped = 2 };
        Assert.Equal(OwnerEvaluationOutcome.FinalWarning, evaluator.Evaluate(warningOwner, nearMissSeason).Outcome);

        var lowConfidenceOwner = BuildOwner(trust: 60, confidence: 30, patience: 60);
        var mixedSeason = poorSeason with { MadePlayoffs = true, FinancialTargetMet = true };
        Assert.Equal(OwnerEvaluationOutcome.Warning, evaluator.Evaluate(lowConfidenceOwner, mixedSeason).Outcome);

        var exhaustedOwner = BuildOwner(trust: 20, confidence: 20, patience: 15);
        var firedResult = evaluator.Evaluate(exhaustedOwner, poorSeason with { BudgetSpent = 5_000_000m });
        Assert.Equal(OwnerEvaluationOutcome.Fired, firedResult.Outcome);
    }

    public void OwnerEvaluationAppliesRelationshipChanges()
    {
        var owner = BuildOwner(trust: 50, confidence: 50, patience: 50);
        var result = new OwnerEvaluator().Evaluate(
            owner,
            new OwnerSeasonPerformance(
                WinPercentage: 0.60m,
                MadePlayoffs: true,
                WonChampionship: false,
                ProspectsDeveloped: 2,
                FinancialTargetMet: true,
                CommunityTrustChange: 2,
                BudgetSpent: 4_500_000m));

        var updatedOwner = owner.ApplyEvaluation(result);

        Assert.Equal(50 + result.TrustChange, updatedOwner.Trust);
        Assert.Equal(50 + result.ConfidenceChange, updatedOwner.Confidence);
        Assert.Equal(50 + result.PatienceChange, updatedOwner.Patience);
    }

    private static Owner BuildOwner(
        string? organizationId = "windsor-juniors",
        int trust = 60,
        int confidence = 60,
        int patience = 60) =>
        new(
            OwnerId: "owner-001",
            Name: "Mara Ellison",
            OrganizationId: organizationId,
            Archetype: OwnerArchetype.Builder,
            Budget: new OwnerBudget(
                PlayerPayroll: 2_500_000m,
                Staff: 750_000m,
                Scouting: 300_000m,
                Facilities: 500_000m,
                Operations: 200_000m),
            Goals: new[]
            {
                new OwnerGoal(OwnerGoalType.MakePlayoffs, 3, "Make the playoffs."),
                new OwnerGoal(OwnerGoalType.DevelopProspects, 4, "Develop at least two prospects."),
                new OwnerGoal(OwnerGoalType.ImproveFinances, 2, "Stay aligned with the financial plan."),
                new OwnerGoal(OwnerGoalType.BuildCommunityTrust, 1, "Improve community trust.")
            },
            Trust: trust,
            Confidence: confidence,
            Patience: patience,
            AutonomyLevel: OwnerAutonomyLevel.High);
}
