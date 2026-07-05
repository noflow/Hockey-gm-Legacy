using LegacyEngine.Development;
using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Integration;
using LegacyEngine.World;

internal sealed class DevelopmentInboxPolicyTests
{
    public void NoOffseasonDevelopmentInboxSpam()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var before = scenario.AlphaSnapshot.DevelopmentProfiles[0].LastUpdated;
        var result = new DailySimulationCoordinator().AdvanceOneDay(scenario.Registry, scenario.AlphaSnapshot);
        var developmentItems = result.InboxItems.Where(item => item.EventType is LegacyEventType.PlayerDevelopmentUpdated or LegacyEventType.PlayerBreakout or LegacyEventType.PlayerRegression).ToArray();

        Assert.Equal(before, result.WorldSnapshot.DevelopmentProfiles[0].LastUpdated);
        Assert.Equal(0, developmentItems.Length);
        Assert.True(result.LogEntries.Any(item => item.Message.Contains("development skipped", StringComparison.OrdinalIgnoreCase)), "Development skip should be logged.");
    }

    public void DevelopmentInboxMessagesAreCapped()
    {
        var (registry, snapshot) = ActiveWorldWithProfiles(BuildBreakoutProfile);
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);
        var developmentItems = DevelopmentInboxItems(result);

        Assert.True(developmentItems.Length is >= 1 and <= 3, "Development inbox should produce between one and three meaningful messages.");
    }

    public void PlayerNameAppearsInDevelopmentMessage()
    {
        var (registry, snapshot) = ActiveWorldWithProfiles(BuildBreakoutProfile);
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);
        var item = DevelopmentInboxItems(result)[0];
        var name = snapshot.People.Single(person => person.PersonId == item.PrimaryPersonId).Identity.DisplayName;

        Assert.True(item.Title.Contains(name, StringComparison.Ordinal), "Development subject should include player name.");
        Assert.True(item.Summary.Contains(name, StringComparison.Ordinal), "Development body should include player name.");
    }

    public void RoutineDevelopmentUpdateDoesNotCreateInboxItem()
    {
        var (registry, snapshot) = ActiveWorldWithProfiles(BuildRoutineProfile, injuries: Array.Empty<Injury>());
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.Equal(0, DevelopmentInboxItems(result).Length);
        Assert.True(result.LogEntries.Any(item => item.Message.Contains("routine changes", StringComparison.OrdinalIgnoreCase)), "Routine updates should be logged internally.");
    }

    public void MeaningfulDevelopmentUpdateCreatesInboxItem()
    {
        var (registry, snapshot) = ActiveWorldWithProfiles(BuildBreakoutProfile);
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);
        var item = DevelopmentInboxItems(result)[0];

        Assert.True(item.Title.StartsWith("Player Development Update:", StringComparison.Ordinal), "Development message should have a useful subject.");
        Assert.True(item.Summary.Contains("age", StringComparison.OrdinalIgnoreCase), "Development body should include age.");
        Assert.True(item.Summary.Contains("This matters", StringComparison.Ordinal), "Development body should explain why the update matters.");
    }

    private static AlphaInboxItem[] DevelopmentInboxItems(AlphaSimulationResult result) =>
        result.InboxItems
            .Where(item => item.EventType is LegacyEventType.PlayerDevelopmentUpdated or LegacyEventType.PlayerBreakout or LegacyEventType.PlayerRegression)
            .ToArray();

    private static (EngineRegistry Registry, AlphaWorldSnapshot Snapshot) ActiveWorldWithProfiles(
        Func<EngineRegistry, string, DateOnly, PlayerDevelopmentProfile> buildProfile,
        IReadOnlyList<Injury>? injuries = null)
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        registry.WorldEngine.SetPhase(WorldPhase.Preseason);
        var profiles = snapshot.Players
            .Take(5)
            .Select(player => buildProfile(registry, player.PersonId, snapshot.CurrentDate))
            .ToArray();

        return (registry, snapshot with
        {
            WorldState = registry.WorldEngine.State,
            DevelopmentProfiles = profiles,
            Injuries = injuries ?? snapshot.Injuries
        });
    }

    private static PlayerDevelopmentProfile BuildBreakoutProfile(EngineRegistry registry, string personId, DateOnly date) =>
        registry.DevelopmentEngine.CreateProfile(
            personId,
            currentAbility: 36,
            potential: 82,
            stage: DevelopmentStage.Junior,
            traits: HighGrowthTraits(),
            lastUpdated: date);

    private static PlayerDevelopmentProfile BuildRoutineProfile(EngineRegistry registry, string personId, DateOnly date) =>
        registry.DevelopmentEngine.CreateProfile(
            personId,
            currentAbility: 62,
            potential: 62,
            stage: DevelopmentStage.Prime,
            traits: RoutineTraits(),
            lastUpdated: date);

    private static IReadOnlyList<DevelopmentTrait> HighGrowthTraits() =>
        new[]
        {
            new DevelopmentTrait(DevelopmentAttribute.Skating, 78),
            new DevelopmentTrait(DevelopmentAttribute.Shooting, 76),
            new DevelopmentTrait(DevelopmentAttribute.Passing, 77),
            new DevelopmentTrait(DevelopmentAttribute.Defense, 74),
            new DevelopmentTrait(DevelopmentAttribute.Physicality, 72),
            new DevelopmentTrait(DevelopmentAttribute.HockeyIQ, 79),
            new DevelopmentTrait(DevelopmentAttribute.WorkEthic, 98),
            new DevelopmentTrait(DevelopmentAttribute.Coachability, 96),
            new DevelopmentTrait(DevelopmentAttribute.Confidence, 94)
        };

    private static IReadOnlyList<DevelopmentTrait> RoutineTraits() =>
        new[]
        {
            new DevelopmentTrait(DevelopmentAttribute.Skating, 50),
            new DevelopmentTrait(DevelopmentAttribute.Shooting, 50),
            new DevelopmentTrait(DevelopmentAttribute.Passing, 50),
            new DevelopmentTrait(DevelopmentAttribute.Defense, 50),
            new DevelopmentTrait(DevelopmentAttribute.Physicality, 50),
            new DevelopmentTrait(DevelopmentAttribute.HockeyIQ, 50),
            new DevelopmentTrait(DevelopmentAttribute.WorkEthic, 50),
            new DevelopmentTrait(DevelopmentAttribute.Coachability, 50),
            new DevelopmentTrait(DevelopmentAttribute.Confidence, 50)
        };
}
