namespace LegacyEngine.People;

public sealed record PersonReputation(
    int Local,
    int League,
    int National)
{
    public void Validate()
    {
        ValidateScore(Local, nameof(Local));
        ValidateScore(League, nameof(League));
        ValidateScore(National, nameof(National));
    }

    public PersonReputation Change(int localDelta, int leagueDelta, int nationalDelta) =>
        new(
            Local: ClampScore(Local + localDelta),
            League: ClampScore(League + leagueDelta),
            National: ClampScore(National + nationalDelta));

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Reputation scores must be between 0 and 100.");
        }
    }

    private static int ClampScore(int value) => Math.Clamp(value, 0, 100);
}
