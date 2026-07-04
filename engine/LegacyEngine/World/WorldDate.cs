namespace LegacyEngine.World;

public sealed record WorldDate(DateOnly Value)
{
    public int Year => Value.Year;

    public WorldDate AddDays(int days) => new(Value.AddDays(days));

    public static implicit operator DateOnly(WorldDate worldDate) => worldDate.Value;
}
