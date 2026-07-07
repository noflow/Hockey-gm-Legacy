namespace LegacyEngine.Integration;

public sealed record OwnerPersonalityProfile(
    OwnerPersonalityType PersonalityType,
    string Vision,
    int RiskTolerance,
    int Patience,
    OwnerBudgetPhilosophy BudgetPhilosophy,
    string WinningExpectation,
    string ProspectExpectation,
    OwnerRelationshipStyle RelationshipStyle)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Vision)
            || string.IsNullOrWhiteSpace(WinningExpectation)
            || string.IsNullOrWhiteSpace(ProspectExpectation))
        {
            throw new ArgumentException("Owner personality profile requires vision and expectation text.");
        }

        if (RiskTolerance is < 0 or > 100 || Patience is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(RiskTolerance), "Owner risk tolerance and patience must be between 0 and 100.");
        }
    }
}
