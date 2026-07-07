namespace LegacyEngine.Integration;

public sealed record OwnerConfidenceState(
    int Confidence,
    int Trust,
    int Patience,
    int Pressure,
    int Support,
    IReadOnlyList<string> Drivers)
{
    public void Validate()
    {
        foreach (var value in new[] { Confidence, Trust, Patience, Pressure, Support })
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(Confidence), "Owner confidence values must be between 0 and 100.");
            }
        }

        if (Drivers.Count == 0 || Drivers.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Owner confidence state requires driver text.", nameof(Drivers));
        }
    }
}
