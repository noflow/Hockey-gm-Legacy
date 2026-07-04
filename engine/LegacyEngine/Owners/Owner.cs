namespace LegacyEngine.Owners;

public sealed record Owner(
    string OwnerId,
    string Name,
    string? OrganizationId,
    OwnerArchetype Archetype,
    OwnerBudget Budget,
    IReadOnlyList<OwnerGoal> Goals,
    int Trust,
    int Confidence,
    int Patience,
    OwnerAutonomyLevel AutonomyLevel)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OwnerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(OwnerId));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Owner name is required.", nameof(Name));
        }

        Budget.Validate();

        if (Goals.Count == 0)
        {
            throw new ArgumentException("An owner must have at least one goal.", nameof(Goals));
        }

        foreach (var goal in Goals)
        {
            goal.Validate();
        }

        ValidateScore(Trust, nameof(Trust));
        ValidateScore(Confidence, nameof(Confidence));
        ValidateScore(Patience, nameof(Patience));
    }

    public Owner AssignToOrganization(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        return this with { OrganizationId = organizationId };
    }

    public Owner ApplyEvaluation(OwnerEvaluationResult result) =>
        this with
        {
            Trust = ClampScore(Trust + result.TrustChange),
            Confidence = ClampScore(Confidence + result.ConfidenceChange),
            Patience = ClampScore(Patience + result.PatienceChange)
        };

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Owner trust, confidence, and patience must be between 0 and 100.");
        }
    }

    private static int ClampScore(int value) => Math.Clamp(value, 0, 100);
}
