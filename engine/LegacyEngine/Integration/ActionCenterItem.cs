namespace LegacyEngine.Integration;

public sealed record ActionCenterItem(
    string ActionCenterItemId,
    string Title,
    ActionCenterCategory Category,
    ActionCenterPriority Priority,
    DateOnly? DueDate,
    string? RelatedPersonId,
    string? RelatedPersonName,
    string? RelatedTeamId,
    string? RelatedTeamName,
    string Reason,
    string Consequence,
    string RecommendedAction,
    string? SourceInboxItemId,
    string? SourceEventId,
    string? SourcePendingActionId,
    ActionCenterStatus Status = ActionCenterStatus.Open)
{
    public override string ToString() =>
        $"{Priority} | {Category} | {Title}\n{RecommendedAction}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActionCenterItemId))
        {
            throw new ArgumentException("Action center item id is required.", nameof(ActionCenterItemId));
        }

        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(Consequence) || string.IsNullOrWhiteSpace(RecommendedAction))
        {
            throw new ArgumentException("Action center items require title, reason, consequence, and recommendation.");
        }
    }
}
