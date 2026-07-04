using LegacyEngine.Events;

namespace LegacyEngine.Contracts;

public sealed record ContractDecisionResult(
    ContractDecision Decision,
    ContractStatus ResultingStatus,
    Contract Contract,
    LegacyEvent CreatedEvent,
    string Message,
    IReadOnlyDictionary<string, object?> Details);
