namespace LegacyEngine.Integration;

public sealed record ExecutiveReportSection(
    string Title,
    IReadOnlyDictionary<string, string> Items,
    string Narrative)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Executive report section title is required.", nameof(Title));
        }

        if (Items.Count == 0)
        {
            throw new ArgumentException("Executive report section must include items.", nameof(Items));
        }

        if (string.IsNullOrWhiteSpace(Narrative))
        {
            throw new ArgumentException("Executive report section narrative is required.", nameof(Narrative));
        }
    }
}
