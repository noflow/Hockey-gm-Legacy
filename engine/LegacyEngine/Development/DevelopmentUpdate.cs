namespace LegacyEngine.Development;

public sealed record DevelopmentUpdate(
    DateOnly Date,
    DevelopmentAttribute Attribute,
    int PreviousValue,
    int NewValue)
{
    public int Change => NewValue - PreviousValue;
}
