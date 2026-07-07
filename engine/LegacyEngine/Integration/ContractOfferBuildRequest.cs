using LegacyEngine.Contracts;

namespace LegacyEngine.Integration;

public sealed record ContractOfferBuildRequest(
    string PersonId,
    ContractAskType AskType,
    decimal AnnualSalary,
    int TermYears,
    string RolePromise,
    string DevelopmentPromise,
    bool CampInvitePromise,
    string StaffRoleOrFocusPromise,
    string Notes,
    ContractType? ContractType = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(RolePromise)
            || string.IsNullOrWhiteSpace(DevelopmentPromise)
            || string.IsNullOrWhiteSpace(StaffRoleOrFocusPromise)
            || string.IsNullOrWhiteSpace(Notes))
        {
            throw new ArgumentException("Contract offer request requires person, promises, and notes.");
        }

        if (AnnualSalary < 0 || TermYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AnnualSalary), "Contract offer salary and term must be valid.");
        }
    }
}
