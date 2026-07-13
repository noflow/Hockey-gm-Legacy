namespace LegacyEngine.Integration;

/// <summary>
/// Keeps visible rating uncertainty tied to player maturity without exposing hidden ratings.
/// Young players retain meaningful upside uncertainty; established players have a tighter
/// current-read and a potential estimate that sits closer to their current overall.
/// </summary>
internal static class AgeAwareRatingRules
{
    public static (int Low, int High) Overall(
        int estimate,
        int? age,
        PlayerRatingConfidence confidence)
    {
        var confidenceSpread = confidence switch
        {
            PlayerRatingConfidence.VeryHigh => 0,
            PlayerRatingConfidence.High => 1,
            PlayerRatingConfidence.Medium => 3,
            PlayerRatingConfidence.Low => 6,
            _ => 8
        };

        // Current ability becomes easier to identify as a player accumulates league time.
        // The confidence input still matters, but age prevents a veteran's OVR from looking
        // as uncertain as a lightly scouted teenager's.
        var maturityCap = age switch
        {
            null => confidenceSpread,
            <= 18 => 8,
            <= 21 => 6,
            <= 24 => 4,
            <= 26 => 2,
            <= 28 => 1,
            _ => 0
        };

        var spread = age is null ? confidenceSpread : Math.Min(confidenceSpread, maturityCap);
        return (Math.Clamp(estimate - spread, 0, 100), Math.Clamp(estimate + spread, 0, 100));
    }

    public static (int Low, int High) Potential(
        int estimate,
        int currentOverall,
        int? age,
        PlayerRatingConfidence confidence)
    {
        var confidenceSpread = confidence switch
        {
            PlayerRatingConfidence.VeryHigh => 1,
            PlayerRatingConfidence.High => 2,
            PlayerRatingConfidence.Medium => 4,
            PlayerRatingConfidence.Low => 6,
            _ => 8
        };

        // This is the remaining-development window. It is deliberately wider for young
        // prospects and tightens sharply once a player is close to his expected peak.
        var ageSpread = age switch
        {
            null => confidenceSpread,
            <= 18 => 5,
            <= 20 => 5,
            <= 22 => 4,
            <= 24 => 5,
            <= 26 => 2,
            <= 28 => 1,
            _ => 0
        };
        var spread = age is null ? confidenceSpread : Math.Max(confidenceSpread, ageSpread);

        var (minimumGap, maximumGap) = age switch
        {
            null => (0, 100),
            <= 18 => (6, 14),
            <= 20 => (5, 12),
            <= 22 => (3, 9),
            <= 24 => (2, 6),
            <= 26 => (1, 2),
            <= 28 => (0, 2),
            _ => (0, 1)
        };

        var maximumVisiblePotential = Math.Clamp(currentOverall + maximumGap, currentOverall, 100);
        var low = Math.Max(currentOverall + minimumGap, estimate - spread);
        // When an uncertain estimate sits above the age-appropriate peak window, clamp the
        // whole visible range into that window instead of allowing the low end to push the
        // high end back above the player's expected mature ceiling.
        low = Math.Clamp(low, currentOverall, maximumVisiblePotential);
        var high = Math.Min(maximumVisiblePotential, estimate + spread);
        high = Math.Clamp(high, low, 100);
        return (low, high);
    }

    public static PlayerRatingConfidence FromColor(PlayerRatingColor color) =>
        color switch
        {
            PlayerRatingColor.Black => PlayerRatingConfidence.VeryHigh,
            PlayerRatingColor.Blue => PlayerRatingConfidence.High,
            PlayerRatingColor.Green => PlayerRatingConfidence.Medium,
            PlayerRatingColor.Red => PlayerRatingConfidence.Low,
            _ => PlayerRatingConfidence.Unknown
        };
}
