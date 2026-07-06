namespace LegacyEngine.Integration;

public sealed record SeasonStatLeaders(
    IReadOnlyList<StatLeader> TeamLeaders,
    IReadOnlyList<StatLeader> SkaterLeaders,
    IReadOnlyList<StatLeader> GoalieLeaders,
    IReadOnlyList<StatLeader> LeagueLeaders)
{
    public void Validate()
    {
        foreach (var leader in TeamLeaders.Concat(SkaterLeaders).Concat(GoalieLeaders).Concat(LeagueLeaders))
        {
            leader.Validate();
        }
    }
}

public sealed record StatLeader(
    string Category,
    string Name,
    string Detail,
    decimal Value)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Detail))
        {
            throw new ArgumentException("Stat leader requires category, name, and detail.");
        }
    }
}
