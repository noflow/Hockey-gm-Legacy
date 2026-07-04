namespace LegacyEngine.Draft;

public sealed record DraftPick(
    string PickId,
    int RoundNumber,
    int PickNumber,
    string OwningOrganizationId,
    DraftSelection? Selection)
{
    public bool IsSelected => Selection is not null;

    public DraftPick Select(DraftSelection selection)
    {
        if (IsSelected)
        {
            throw new InvalidOperationException("Draft pick has already been used.");
        }

        selection.Validate();
        return this with { Selection = selection };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PickId))
        {
            throw new ArgumentException("Draft pick id is required.", nameof(PickId));
        }

        if (RoundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RoundNumber), "Draft pick round must be positive.");
        }

        if (PickNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PickNumber), "Draft pick number must be positive.");
        }

        if (string.IsNullOrWhiteSpace(OwningOrganizationId))
        {
            throw new ArgumentException("Owning organization id is required.", nameof(OwningOrganizationId));
        }

        Selection?.Validate();
    }
}
