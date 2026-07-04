namespace LegacyEngine.Owners;

public sealed record OwnerArchetypeProfile(
    OwnerArchetype Archetype,
    OwnerAutonomyLevel DefaultAutonomyLevel,
    IReadOnlyList<OwnerGoalType> PreferredGoals,
    int DefaultTrust,
    int DefaultConfidence,
    int DefaultPatience)
{
    public static OwnerArchetypeProfile For(OwnerArchetype archetype) =>
        archetype switch
        {
            OwnerArchetype.Builder => new(archetype, OwnerAutonomyLevel.High, new[] { OwnerGoalType.DevelopProspects, OwnerGoalType.Rebuild }, 65, 55, 80),
            OwnerArchetype.Competitor => new(archetype, OwnerAutonomyLevel.Normal, new[] { OwnerGoalType.MakePlayoffs, OwnerGoalType.WinChampionship }, 55, 70, 45),
            OwnerArchetype.CommunityOwner => new(archetype, OwnerAutonomyLevel.Normal, new[] { OwnerGoalType.BuildCommunityTrust, OwnerGoalType.MakePlayoffs }, 70, 55, 70),
            OwnerArchetype.Investor => new(archetype, OwnerAutonomyLevel.Low, new[] { OwnerGoalType.ImproveFinances, OwnerGoalType.MakePlayoffs }, 50, 55, 55),
            OwnerArchetype.Traditionalist => new(archetype, OwnerAutonomyLevel.Normal, new[] { OwnerGoalType.MakePlayoffs, OwnerGoalType.BuildCommunityTrust }, 60, 60, 60),
            OwnerArchetype.Innovator => new(archetype, OwnerAutonomyLevel.FullHockeyControl, new[] { OwnerGoalType.DevelopProspects, OwnerGoalType.Rebuild }, 65, 60, 75),
            _ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Unsupported owner archetype.")
        };
}
