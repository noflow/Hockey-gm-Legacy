namespace LegacyEngine.Integration;

public sealed record TradeBlock(
    string TradeBlockId,
    DateOnly UpdatedOn,
    IReadOnlyList<TradeBlockEntry> Entries)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TradeBlockId))
        {
            throw new ArgumentException("Trade block id is required.", nameof(TradeBlockId));
        }

        if (Entries.Select(entry => entry.PersonId).Distinct(StringComparer.Ordinal).Count() != Entries.Count)
        {
            throw new ArgumentException("Trade block cannot contain duplicate person ids.", nameof(Entries));
        }

        foreach (var entry in Entries)
        {
            entry.Validate();
        }
    }

    public TradeBlockEntry? Find(string personId) =>
        Entries.FirstOrDefault(entry => entry.PersonId == personId);
}
