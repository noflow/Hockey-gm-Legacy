namespace LegacyEngine.Integration;

public sealed record ExecutiveReportArchive(IReadOnlyList<ExecutiveReportRecord> Reports)
{
    public static ExecutiveReportArchive Empty { get; } = new(Array.Empty<ExecutiveReportRecord>());

    public ExecutiveReportArchive AddOrReplace(ExecutiveReportRecord report)
    {
        report.Validate();
        return new ExecutiveReportArchive(
            Reports
                .Where(existing => existing.ReportId != report.ReportId)
                .Append(report)
                .OrderBy(item => item.SeasonYear)
                .ThenBy(item => item.Kind)
                .ToArray());
    }

    public ExecutiveReportRecord? Find(string reportId) =>
        Reports.SingleOrDefault(report => string.Equals(report.ReportId, reportId, StringComparison.Ordinal));

    public IReadOnlyList<ExecutiveReportRecord> ForSeason(int seasonYear) =>
        Reports.Where(report => report.SeasonYear == seasonYear).ToArray();

    public IReadOnlyList<ExecutiveReportRecord> CurrentSeason(int seasonYear) =>
        ForSeason(seasonYear);

    public IReadOnlyList<ExecutiveReportRecord> PreviousSeasons(int seasonYear) =>
        Reports.Where(report => report.SeasonYear < seasonYear).ToArray();

    public ExecutiveReportRecord? Latest(ExecutiveReportKind kind) =>
        Reports
            .Where(report => report.Kind == kind)
            .OrderByDescending(report => report.SeasonYear)
            .ThenByDescending(report => report.GeneratedAt)
            .FirstOrDefault();

    public void Validate()
    {
        foreach (var report in Reports)
        {
            report.Validate();
        }

        if (Reports.Select(report => report.ReportId).Distinct(StringComparer.Ordinal).Count() != Reports.Count)
        {
            throw new ArgumentException("Executive report ids must be unique.", nameof(Reports));
        }
    }
}
