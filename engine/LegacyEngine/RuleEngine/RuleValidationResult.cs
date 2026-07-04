using System.Text.Json.Serialization;

namespace LegacyEngine.RuleEngine;

public sealed record RuleValidationResult(
    [property: JsonPropertyName("is_valid")] bool IsValid,
    [property: JsonPropertyName("rule_code")] string RuleCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("details")] IReadOnlyDictionary<string, object?> Details)
{
    public static RuleValidationResult Valid(string message = "Rule validation passed.") =>
        new(true, RuleErrorCodes.Valid, message, RuleSeverity.Info, new Dictionary<string, object?>());

    public static RuleValidationResult Failure(
        string ruleCode,
        string message,
        string severity = RuleSeverity.Error,
        IReadOnlyDictionary<string, object?>? details = null) =>
        new(false, ruleCode, message, severity, details ?? new Dictionary<string, object?>());
}

public static class RuleSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}
