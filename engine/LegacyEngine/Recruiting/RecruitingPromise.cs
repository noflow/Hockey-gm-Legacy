namespace LegacyEngine.Recruiting;

public sealed record RecruitingPromise(
    string PromiseId,
    string OrganizationId,
    RecruitingPromiseType PromiseType,
    int Strength,
    DateOnly Date,
    string Description,
    DateOnly? TargetDate = null,
    bool IsFulfilled = false,
    bool IsBroken = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PromiseId))
        {
            throw new ArgumentException("Promise id is required.", nameof(PromiseId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (Strength is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Strength), "Promise strength must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Promise description is required.", nameof(Description));
        }
    }
}
