namespace LegacyEngine.Organizations;

/// <summary>
/// The cultural leanings of an organization. Each dimension is an integer between
/// 0 and 100 and describes emphasis, not ability.
/// </summary>
public sealed record OrganizationCulture(
    int DevelopmentFocus,
    int WinningPressure,
    int FinancialDiscipline,
    int CommunityFocus,
    int Innovation,
    int Loyalty)
{
    public static OrganizationCulture Balanced { get; } = new(50, 50, 50, 50, 50, 50);

    public void Validate()
    {
        ValidateScore(DevelopmentFocus, nameof(DevelopmentFocus));
        ValidateScore(WinningPressure, nameof(WinningPressure));
        ValidateScore(FinancialDiscipline, nameof(FinancialDiscipline));
        ValidateScore(CommunityFocus, nameof(CommunityFocus));
        ValidateScore(Innovation, nameof(Innovation));
        ValidateScore(Loyalty, nameof(Loyalty));
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Organization culture values must be between 0 and 100.");
        }
    }
}
