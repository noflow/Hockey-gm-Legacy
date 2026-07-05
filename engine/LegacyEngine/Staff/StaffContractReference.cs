namespace LegacyEngine.Staff;

/// <summary>
/// A reference to a contract owned by the Contracts engine. The Staff engine never
/// creates, negotiates, or mutates contracts; it only points at a contract id.
/// </summary>
public sealed record StaffContractReference(
    string ContractId,
    string OrganizationId,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ContractId))
        {
            throw new ArgumentException("Contract id is required.", nameof(ContractId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(EndDate), "Contract end date cannot be before start date.");
        }
    }
}
