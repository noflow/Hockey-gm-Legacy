namespace LegacyEngine.Organizations;

/// <summary>
/// How an organization is regarded locally, within its league, and nationally.
/// Every value is an integer between 0 and 100.
/// </summary>
public sealed record OrganizationReputation(
    int Local,
    int League,
    int National)
{
    public static OrganizationReputation Neutral { get; } = new(50, 50, 50);

    public OrganizationReputation Change(int localDelta, int leagueDelta, int nationalDelta) =>
        new(
            Local: ClampScore(Local + localDelta),
            League: ClampScore(League + leagueDelta),
            National: ClampScore(National + nationalDelta));

    public void Validate()
    {
        ValidateScore(Local, nameof(Local));
        ValidateScore(League, nameof(League));
        ValidateScore(National, nameof(National));
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Organization reputation scores must be between 0 and 100.");
        }
    }

    private static int ClampScore(int value) => Math.Clamp(value, 0, 100);
}
