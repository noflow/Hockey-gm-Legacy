namespace LegacyEngine.Recruiting;

public sealed record RecruitInterest(
    string OrganizationId,
    int Value,
    DateOnly LastUpdated)
{
    public RecruitInterest Change(int amount, DateOnly date) =>
        this with
        {
            Value = Clamp(Value + amount),
            LastUpdated = date
        };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (Value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Value), "Recruit interest must be between 0 and 100.");
        }
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
