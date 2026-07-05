namespace LegacyEngine.Integration;

public sealed record ProspectListSummary(
    int TotalProspects,
    int RightsHeld,
    int ContractOffered,
    int Signed,
    int InvitedToCamp,
    int Returned,
    int AssignedToAffiliate,
    int ReleasedOrDeclined);
