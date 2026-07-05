namespace LegacyEngine.Integration;

public sealed record DraftRecap(
    int RoundsCompleted,
    int PlayersDrafted,
    IReadOnlyList<DraftPickSummary> YourSelections,
    IReadOnlyList<DraftPickSummary> OtherNotableSelections,
    DraftPickSummary? BiggestSteal,
    DraftPickSummary? BiggestSurprise,
    string OwnerReaction,
    string HeadScoutReaction)
{
    public void Validate()
    {
        if (RoundsCompleted <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RoundsCompleted), "Rounds completed must be positive.");
        }

        if (PlayersDrafted < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayersDrafted), "Players drafted cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(OwnerReaction))
        {
            throw new ArgumentException("Owner reaction is required.", nameof(OwnerReaction));
        }

        if (string.IsNullOrWhiteSpace(HeadScoutReaction))
        {
            throw new ArgumentException("Head scout reaction is required.", nameof(HeadScoutReaction));
        }
    }
}
