namespace LegacyEngine.Integration;

public sealed record ContractAsk(
    string PersonId,
    string PersonName,
    ContractAskType AskType,
    decimal RequestedSalary,
    int RequestedTermYears,
    string DesiredRole,
    ContractPreference Preference,
    string SigningPriority,
    ContractInterest InterestLevel,
    decimal BudgetImpact,
    int PreferredOrganizationFit,
    int RelationshipTrustImpact,
    string DevelopmentPathwayConcern,
    string StaffCoachConfidence)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(DesiredRole)
            || string.IsNullOrWhiteSpace(SigningPriority)
            || string.IsNullOrWhiteSpace(DevelopmentPathwayConcern)
            || string.IsNullOrWhiteSpace(StaffCoachConfidence))
        {
            throw new ArgumentException("Contract ask requires readable person, role, priority, and context fields.");
        }

        if (RequestedSalary < 0 || BudgetImpact < 0 || RequestedTermYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RequestedSalary), "Contract ask salary, budget impact, and term must be valid.");
        }

        if (PreferredOrganizationFit is < 0 or > 100 || RelationshipTrustImpact is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(PreferredOrganizationFit), "Contract ask fit scores must be between 0 and 100.");
        }

        Preference.Validate();
    }
}
