using LegacyEngine.Draft;

namespace LegacyEngine.Integration;

public sealed record DraftExperienceState(
    DraftExperienceStatus Status,
    LegacyEngine.Draft.Draft? Draft,
    string PlayerOrganizationId,
    IReadOnlyDictionary<string, string> OrganizationNames,
    IReadOnlyList<DraftPickSummary> Selections,
    DraftRecap? Recap,
    string CountdownPlaceholder)
{
    public int TotalRounds => Draft?.NumberOfRounds ?? 0;

    public DraftPick? CurrentPick => Draft?.Picks
        .OrderBy(item => item.RoundNumber)
        .ThenBy(item => item.PickNumber)
        .FirstOrDefault(item => !item.IsSelected);

    public int CurrentRound => CurrentPick?.RoundNumber ?? TotalRounds;

    public int OverallPick => CurrentPick?.PickNumber ?? Selections.Count;

    public string TeamSelecting => CurrentPick is null
        ? "Draft complete"
        : OrganizationNames.GetValueOrDefault(CurrentPick.OwningOrganizationId, CurrentPick.OwningOrganizationId);

    public bool IsPlayerTurn => CurrentPick?.OwningOrganizationId == PlayerOrganizationId;

    public DraftPick? PlayerNextPick => Draft?.Picks
        .OrderBy(item => item.RoundNumber)
        .ThenBy(item => item.PickNumber)
        .FirstOrDefault(item => !item.IsSelected && item.OwningOrganizationId == PlayerOrganizationId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayerOrganizationId))
        {
            throw new ArgumentException("Player organization id is required.", nameof(PlayerOrganizationId));
        }

        if (OrganizationNames.Count == 0)
        {
            throw new ArgumentException("Draft experience must include organization names.", nameof(OrganizationNames));
        }

        if (string.IsNullOrWhiteSpace(CountdownPlaceholder))
        {
            throw new ArgumentException("Countdown placeholder is required.", nameof(CountdownPlaceholder));
        }

        Draft?.Validate();
        Recap?.Validate();
    }
}
