using LegacyEngine.World;

namespace LegacyEngine.Seasons;

/// <summary>
/// Lets a <see cref="WorldEngine"/> ask "what season phase are we currently in?" for a
/// given season, using the world's current date. The dependency points from Seasons
/// to World, so the World engine stays free of any season references.
/// </summary>
public static class WorldSeasonQueries
{
    public static SeasonPhase CurrentSeasonPhase(this WorldEngine world, Season season)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(season);

        return season.PhaseOn(world.State.CurrentDate.Value);
    }
}
