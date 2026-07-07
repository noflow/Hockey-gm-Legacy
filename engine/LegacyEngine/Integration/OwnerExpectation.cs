namespace LegacyEngine.Integration;

public sealed record OwnerExpectation(
    string ExpectationId,
    OwnerExpectationType ExpectationType,
    int Priority,
    int Difficulty,
    DateOnly Deadline,
    int CurrentProgress,
    string Description)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ExpectationId) || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Owner expectation requires id and description.");
        }

        if (Priority is < 1 or > 5 || Difficulty is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(Priority), "Owner expectation priority and difficulty must be between 1 and 5.");
        }

        if (CurrentProgress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(CurrentProgress), "Owner expectation progress must be between 0 and 100.");
        }
    }
}
