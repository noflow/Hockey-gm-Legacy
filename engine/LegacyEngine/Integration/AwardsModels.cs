namespace LegacyEngine.Integration;

public enum AwardCategory
{
    League,
    Team,
    Playoff,
    Rookie,
    Staff,
    GM,
    Scouting,
    Development
}

public enum AwardType
{
    Mvp,
    BestDefenseman,
    BestGoalie,
    RookieOfTheYear,
    CoachOfTheYear,
    GmOfTheYear,
    PlayoffMvp,
    TopScorer,
    BestDefensiveForward,
    TeamMvp,
    MostImproved,
    BestProspect,
    TopScout,
    DevelopmentStaffAward
}

public sealed record AwardRecipient(
    string RecipientId,
    string RecipientName,
    string RecipientKind,
    string? OrganizationId,
    string? OrganizationName)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecipientId)
            || string.IsNullOrWhiteSpace(RecipientName)
            || string.IsNullOrWhiteSpace(RecipientKind))
        {
            throw new ArgumentException("Award recipient requires id, name, and kind.");
        }
    }
}

public sealed record Award(
    string AwardId,
    int SeasonYear,
    DateOnly AwardDate,
    AwardType AwardType,
    AwardCategory Category,
    AwardRecipient Winner,
    IReadOnlyList<AwardRecipient> Finalists,
    int Score,
    string Reasoning,
    bool IsMajor)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AwardId) || string.IsNullOrWhiteSpace(Reasoning))
        {
            throw new ArgumentException("Award requires id and reasoning.");
        }

        Winner.Validate();
        foreach (var finalist in Finalists)
        {
            finalist.Validate();
        }
    }
}

public sealed record AwardHistory(IReadOnlyList<Award> Awards)
{
    public static AwardHistory Empty { get; } = new(Array.Empty<Award>());

    public AwardHistory Merge(IEnumerable<Award> awards)
    {
        var merged = Awards
            .Concat(awards)
            .GroupBy(award => award.AwardId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderByDescending(award => award.SeasonYear)
            .ThenBy(award => award.Category)
            .ThenBy(award => award.AwardType)
            .ToArray();
        var history = new AwardHistory(merged);
        history.Validate();
        return history;
    }

    public IReadOnlyList<Award> ForPerson(string personId) =>
        Awards
            .Where(award => string.Equals(award.Winner.RecipientId, personId, StringComparison.Ordinal)
                || award.Finalists.Any(finalist => string.Equals(finalist.RecipientId, personId, StringComparison.Ordinal)))
            .OrderByDescending(award => award.SeasonYear)
            .ThenBy(award => award.AwardType)
            .ToArray();

    public void Validate()
    {
        foreach (var award in Awards)
        {
            award.Validate();
        }

        if (Awards.Select(award => award.AwardId).Distinct(StringComparer.Ordinal).Count() != Awards.Count)
        {
            throw new ArgumentException("Award ids must be unique.", nameof(Awards));
        }
    }
}
