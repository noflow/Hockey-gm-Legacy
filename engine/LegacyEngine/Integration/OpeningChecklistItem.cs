namespace LegacyEngine.Integration;

public sealed record OpeningChecklistItem(
    string Code,
    string Text,
    bool IsComplete,
    bool IsMandatory = true)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            throw new ArgumentException("Checklist item code is required.", nameof(Code));
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            throw new ArgumentException("Checklist item text is required.", nameof(Text));
        }
    }
}
