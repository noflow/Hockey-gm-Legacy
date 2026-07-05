namespace LegacyEngine.Seasons;

/// <summary>
/// A calendar date within a season. A thin wrapper over <see cref="DateOnly"/> so
/// season code reads clearly and can convert to a plain date when needed.
/// </summary>
public sealed record SeasonDate(DateOnly Value)
{
    public int Year => Value.Year;

    public SeasonDate AddDays(int days) => new(Value.AddDays(days));

    public static implicit operator DateOnly(SeasonDate seasonDate) => seasonDate.Value;
}
