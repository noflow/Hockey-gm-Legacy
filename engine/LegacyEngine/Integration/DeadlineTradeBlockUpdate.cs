namespace LegacyEngine.Integration;

public sealed record DeadlineTradeBlockUpdate(
    DateOnly Date,
    int AddedPlayers,
    string Summary)
{
    public void Validate()
    {
        if (AddedPlayers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AddedPlayers), "Added player count cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Deadline trade block update summary is required.", nameof(Summary));
        }
    }
}
