namespace LegacyEngine.Integration;

public sealed record ContractPreference(
    string DesiredRole,
    int MoneyImportance,
    int TermImportance,
    int RoleImportance,
    int DevelopmentImportance,
    int RelationshipImportance,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DesiredRole) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Contract preference requires role and summary.");
        }

        ValidateScore(MoneyImportance, nameof(MoneyImportance));
        ValidateScore(TermImportance, nameof(TermImportance));
        ValidateScore(RoleImportance, nameof(RoleImportance));
        ValidateScore(DevelopmentImportance, nameof(DevelopmentImportance));
        ValidateScore(RelationshipImportance, nameof(RelationshipImportance));
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Contract preference scores must be between 0 and 100.");
        }
    }
}
