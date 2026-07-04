namespace LegacyEngine.Draft;

public sealed record Draft(
    string DraftId,
    int SeasonYear,
    DraftStatus Status,
    int NumberOfRounds,
    DraftOrder DraftOrder,
    IReadOnlyList<DraftPick> Picks)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DraftId))
        {
            throw new ArgumentException("Draft id is required.", nameof(DraftId));
        }

        if (NumberOfRounds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NumberOfRounds), "Draft must have at least one round.");
        }

        DraftOrder.Validate();

        foreach (var pick in Picks)
        {
            pick.Validate();
        }

        if (Picks.Select(item => item.PickId).Distinct(StringComparer.Ordinal).Count() != Picks.Count)
        {
            throw new ArgumentException("Draft pick ids must be unique.", nameof(Picks));
        }
    }

    public bool HasSelectedProspect(string prospectPersonId) =>
        Picks.Any(pick => pick.Selection?.ProspectPersonId == prospectPersonId);
}
