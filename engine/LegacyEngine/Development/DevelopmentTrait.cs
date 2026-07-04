namespace LegacyEngine.Development;

public sealed record DevelopmentTrait(DevelopmentAttribute Attribute, int Value)
{
    public DevelopmentTrait Change(int delta) => this with { Value = Clamp(Value + delta) };

    public void Validate()
    {
        if (Value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Value), "Development trait values must be between 0 and 100.");
        }
    }

    internal static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
