namespace LegacyEngine.Owners;

public sealed record OwnerEvaluationResult(
    OwnerEvaluationOutcome Outcome,
    decimal GoalScore,
    int TrustChange,
    int ConfidenceChange,
    int PatienceChange,
    string Message,
    IReadOnlyDictionary<string, object?> Details);
