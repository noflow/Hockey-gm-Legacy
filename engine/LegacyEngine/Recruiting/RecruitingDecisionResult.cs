using LegacyEngine.Events;

namespace LegacyEngine.Recruiting;

public sealed record RecruitingDecisionResult(
    string RecruitPersonId,
    string OrganizationId,
    RecruitingDecision Decision,
    RecruitStatus ResultingStatus,
    int DecisionScore,
    string Message,
    RecruitProfile UpdatedProfile,
    LegacyEvent CreatedEvent,
    IReadOnlyDictionary<string, object?> Details);
