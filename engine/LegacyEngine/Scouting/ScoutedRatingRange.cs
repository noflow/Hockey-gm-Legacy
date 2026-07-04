namespace LegacyEngine.Scouting;

public sealed record ScoutedRatingRange(int Low, int High)
{
    public void Validate()
    {
        if (Low is < 0 or > 100 || High is < 0 or > 100 || Low > High)
        {
            throw new ArgumentOutOfRangeException(nameof(ScoutedRatingRange), "Scouted rating ranges must stay within 0-100.");
        }
    }
}
