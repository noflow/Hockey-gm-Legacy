namespace LegacyEngine.World;

public sealed record WorldSystemRegistration(
    string SystemName,
    bool IsEnabled)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SystemName))
        {
            throw new ArgumentException("World system registration name is required.", nameof(SystemName));
        }
    }
}
