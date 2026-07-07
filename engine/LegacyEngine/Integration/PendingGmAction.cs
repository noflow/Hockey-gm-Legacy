using LegacyEngine.Contracts;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record PendingGmAction(
    string ActionId,
    PendingGmActionType ActionType,
    PendingGmActionStatus Status,
    DateOnly CreatedOn,
    string PersonId,
    string PersonName,
    string OrganizationId,
    string Title,
    string Reason,
    string RecommendedAction,
    RosterPosition? Position = null,
    PlayerAcquisitionSource AcquisitionSource = PlayerAcquisitionSource.Unknown,
    ContractType? ContractType = null,
    decimal? OfferedSalary = null,
    int? OfferedTermYears = null,
    string RolePromise = "",
    string DevelopmentPromise = "",
    string ContractNotes = "")
{
    public bool IsOpen => Status == PendingGmActionStatus.Pending;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActionId))
        {
            throw new ArgumentException("Pending GM action id is required.", nameof(ActionId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Pending GM action person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(PersonName))
        {
            throw new ArgumentException("Pending GM action person name is required.", nameof(PersonName));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Pending GM action organization id is required.", nameof(OrganizationId));
        }

        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(RecommendedAction))
        {
            throw new ArgumentException("Pending GM action must include title, reason, and recommended action.");
        }

        if (OfferedSalary < 0 || OfferedTermYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OfferedSalary), "Pending contract offer terms must be valid when provided.");
        }
    }
}
