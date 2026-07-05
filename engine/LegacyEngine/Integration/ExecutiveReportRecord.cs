namespace LegacyEngine.Integration;

public sealed record ExecutiveReportRecord(
    string ReportId,
    ExecutiveReportKind Kind,
    DateTimeOffset GeneratedAt,
    string OrganizationId,
    string OrganizationName,
    string LeagueId,
    string SeasonId,
    int SeasonYear,
    string GeneralManagerName,
    string OwnerName,
    string Title,
    string Recommendation,
    int OrganizationHealthPercent,
    IReadOnlyList<ExecutiveReportSection> Sections,
    string ExecutiveSummary)
{
    public ExecutiveReportSection? FindSection(string title) =>
        Sections.SingleOrDefault(section => string.Equals(section.Title, title, StringComparison.Ordinal));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReportId))
        {
            throw new ArgumentException("Executive report id is required.", nameof(ReportId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(OrganizationName))
        {
            throw new ArgumentException("Executive report organization identity is required.", nameof(OrganizationId));
        }

        if (string.IsNullOrWhiteSpace(LeagueId) || string.IsNullOrWhiteSpace(SeasonId))
        {
            throw new ArgumentException("Executive report league and season references are required.", nameof(LeagueId));
        }

        if (SeasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Executive report season year must be positive.");
        }

        if (string.IsNullOrWhiteSpace(GeneralManagerName) || string.IsNullOrWhiteSpace(OwnerName))
        {
            throw new ArgumentException("Executive report must include GM and owner names.");
        }

        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Recommendation) || string.IsNullOrWhiteSpace(ExecutiveSummary))
        {
            throw new ArgumentException("Executive report must include title, recommendation, and executive summary.");
        }

        if (OrganizationHealthPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(OrganizationHealthPercent), "Organization health must be between 0 and 100.");
        }

        if (Sections.Count == 0)
        {
            throw new ArgumentException("Executive report must include sections.", nameof(Sections));
        }

        foreach (var section in Sections)
        {
            section.Validate();
        }
    }
}
