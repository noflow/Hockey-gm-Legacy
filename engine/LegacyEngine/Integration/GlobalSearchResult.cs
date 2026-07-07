namespace LegacyEngine.Integration;

public sealed record GlobalSearchResult(
    string SearchResultId,
    string ResultType,
    string Title,
    string Subtitle,
    string TargetWorkspace,
    string? TargetPersonId,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SearchResultId)
            || string.IsNullOrWhiteSpace(ResultType)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Subtitle)
            || string.IsNullOrWhiteSpace(TargetWorkspace)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Global search result requires readable routing fields.");
        }
    }
}
