using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffMovementRecord(
    string MovementId,
    DateOnly Date,
    string PersonId,
    string StaffName,
    StaffRole Role,
    string? FromOrganizationId,
    string? FromTeamName,
    string? ToOrganizationId,
    string? ToTeamName,
    StaffMarketStatus ResultingStatus,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MovementId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(StaffName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff movement requires identity and summary.");
        }
    }
}
