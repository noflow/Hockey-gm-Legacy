namespace LegacyEngine.Draft;

public sealed record OrganizationStanding(
    string OrganizationId,
    int StandingRank)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (StandingRank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StandingRank), "Standing rank must be positive.");
        }
    }
}

public sealed record DraftOrder(IReadOnlyList<string> OrganizationIds)
{
    public static DraftOrder FromReverseStandings(IReadOnlyList<OrganizationStanding> standings)
    {
        if (standings.Count == 0)
        {
            throw new ArgumentException("Standings are required to generate draft order.", nameof(standings));
        }

        foreach (var standing in standings)
        {
            standing.Validate();
        }

        if (standings.Select(item => item.OrganizationId).Distinct(StringComparer.Ordinal).Count() != standings.Count)
        {
            throw new ArgumentException("Standing organization ids must be unique.", nameof(standings));
        }

        return new DraftOrder(standings
            .OrderByDescending(item => item.StandingRank)
            .ThenBy(item => item.OrganizationId, StringComparer.Ordinal)
            .Select(item => item.OrganizationId)
            .ToArray());
    }

    public void Validate()
    {
        if (OrganizationIds.Count == 0)
        {
            throw new ArgumentException("Draft order must include at least one organization.", nameof(OrganizationIds));
        }

        if (OrganizationIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Draft order organization ids cannot be blank.", nameof(OrganizationIds));
        }

        if (OrganizationIds.Distinct(StringComparer.Ordinal).Count() != OrganizationIds.Count)
        {
            throw new ArgumentException("Draft order organization ids must be unique.", nameof(OrganizationIds));
        }
    }
}
