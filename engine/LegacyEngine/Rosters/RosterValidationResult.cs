namespace LegacyEngine.Rosters;

public sealed record RosterValidationResult(
    bool IsValid,
    string RuleCode,
    string Message,
    IReadOnlyDictionary<string, object?> Details)
{
    public static RosterValidationResult Valid(string message = "Roster is valid.") =>
        new(true, "VALID", message, new Dictionary<string, object?>());

    public static RosterValidationResult Failure(
        string ruleCode,
        string message,
        IReadOnlyDictionary<string, object?>? details = null) =>
        new(false, ruleCode, message, details ?? new Dictionary<string, object?>());
}
