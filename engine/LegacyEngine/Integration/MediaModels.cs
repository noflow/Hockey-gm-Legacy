namespace LegacyEngine.Integration;

public enum MediaArticleType
{
    BreakingNews,
    Transaction,
    Trade,
    FreeAgency,
    Draft,
    ProspectWatch,
    Injury,
    Milestone,
    GameRecap,
    Playoff,
    TeamFeature,
    PlayerFeature,
    StaffFeature,
    OwnerFrontOffice,
    Rumor,
    Analysis,
    Award,
    Record
}

public enum MediaTone
{
    Neutral,
    Positive,
    Critical,
    Speculative,
    Celebratory,
    Concerned
}

public enum MediaImportance
{
    Routine,
    Notable,
    Major,
    Breaking
}

public enum MediaRumorConfidence
{
    None,
    Low,
    Medium,
    High
}

public sealed record MediaSource(
    string SourceId,
    string Name,
    string Focus,
    MediaTone ToneBias,
    int Credibility)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceId)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Focus))
        {
            throw new ArgumentException("Media source requires id, name, and focus.");
        }

        if (Credibility is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Credibility), "Media source credibility must be between zero and one hundred.");
        }
    }
}

public sealed record MediaArticle(
    string ArticleId,
    DateTimeOffset Date,
    string LeagueId,
    string LeagueName,
    MediaSource Source,
    MediaArticleType ArticleType,
    MediaTone Tone,
    MediaImportance Importance,
    string Headline,
    string ShortSummary,
    string? Body,
    IReadOnlyList<string> TeamIds,
    IReadOnlyList<string> TeamNames,
    IReadOnlyList<string> PersonIds,
    IReadOnlyList<string> PersonNames,
    string? RelatedStoryId = null,
    string? RelatedTransactionId = null,
    MediaRumorConfidence RumorConfidence = MediaRumorConfidence.None)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ArticleId)
            || string.IsNullOrWhiteSpace(LeagueId)
            || string.IsNullOrWhiteSpace(LeagueName)
            || string.IsNullOrWhiteSpace(Headline)
            || string.IsNullOrWhiteSpace(ShortSummary))
        {
            throw new ArgumentException("Media article requires id, league, headline, and summary.");
        }

        Source.Validate();

        if (ArticleType == MediaArticleType.Rumor && RumorConfidence == MediaRumorConfidence.None)
        {
            throw new ArgumentException("Rumor articles require a confidence level.", nameof(RumorConfidence));
        }

        if (ArticleType != MediaArticleType.Rumor && RumorConfidence != MediaRumorConfidence.None)
        {
            throw new ArgumentException("Only rumor articles can carry rumor confidence.", nameof(RumorConfidence));
        }

        if (TeamIds.Any(string.IsNullOrWhiteSpace)
            || TeamNames.Any(string.IsNullOrWhiteSpace)
            || PersonIds.Any(string.IsNullOrWhiteSpace)
            || PersonNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Media article team and person references cannot be blank.");
        }
    }
}

public sealed record MediaFeed(
    IReadOnlyList<MediaSource> Sources,
    IReadOnlyList<MediaArticle> Articles)
{
    public static MediaFeed Empty { get; } = new(Array.Empty<MediaSource>(), Array.Empty<MediaArticle>());

    public IReadOnlyList<MediaArticle> Query(
        MediaArticleType? articleType = null,
        string? sourceId = null,
        string? teamId = null,
        string? personId = null,
        MediaImportance? minimumImportance = null)
    {
        return Articles
            .Where(article => articleType is null || article.ArticleType == articleType)
            .Where(article => sourceId is null || string.Equals(article.Source.SourceId, sourceId, StringComparison.Ordinal))
            .Where(article => teamId is null || article.TeamIds.Contains(teamId, StringComparer.Ordinal))
            .Where(article => personId is null || article.PersonIds.Contains(personId, StringComparer.Ordinal))
            .Where(article => minimumImportance is null || article.Importance >= minimumImportance)
            .OrderByDescending(article => article.Date)
            .ThenByDescending(article => article.Importance)
            .ThenBy(article => article.Headline, StringComparer.Ordinal)
            .ToArray();
    }

    public void Validate()
    {
        if (Sources.Count == 0)
        {
            throw new ArgumentException("Media feed requires at least one fictional source.", nameof(Sources));
        }

        foreach (var source in Sources)
        {
            source.Validate();
        }

        foreach (var article in Articles)
        {
            article.Validate();
        }
    }
}
