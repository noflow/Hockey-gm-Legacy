namespace LegacyEngine.Integration;

public sealed record SeasonReadinessState(
    bool ReviewsGenerated = false,
    bool SeasonBegun = false)
{
    public void Validate()
    {
    }
}
