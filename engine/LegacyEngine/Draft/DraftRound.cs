namespace LegacyEngine.Draft;

public sealed record DraftRound(int RoundNumber)
{
    public void Validate()
    {
        if (RoundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RoundNumber), "Draft round must be positive.");
        }
    }
}
