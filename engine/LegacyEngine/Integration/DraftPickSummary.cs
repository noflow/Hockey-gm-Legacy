namespace LegacyEngine.Integration;

public sealed record DraftPickSummary(
    int RoundNumber,
    int PickNumber,
    string OrganizationId,
    string OrganizationName,
    string ProspectPersonId,
    string ProspectName,
    bool IsPlayerSelection);
