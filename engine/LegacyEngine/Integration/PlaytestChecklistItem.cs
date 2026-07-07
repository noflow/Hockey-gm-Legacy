namespace LegacyEngine.Integration;

public sealed record PlaytestChecklistItem(
    string ChecklistItemId,
    string Area,
    string Question,
    string ExpectedOutcome,
    bool IsPassing,
    string Notes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ChecklistItemId)
            || string.IsNullOrWhiteSpace(Area)
            || string.IsNullOrWhiteSpace(Question)
            || string.IsNullOrWhiteSpace(ExpectedOutcome)
            || string.IsNullOrWhiteSpace(Notes))
        {
            throw new ArgumentException("Playtest checklist item requires area, question, expected outcome, and notes.");
        }
    }
}
