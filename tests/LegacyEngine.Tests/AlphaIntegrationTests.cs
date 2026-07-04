using LegacyEngine.Events;
using LegacyEngine.Integration;

internal sealed class AlphaIntegrationTests
{
    public void BootstrapAlphaWorld()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        snapshot.Validate();
        Assert.Equal("Alpha Hockey World", snapshot.WorldState.WorldName);
    }

    public void AlphaWorldHasPeople()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.True(snapshot.People.Count >= 8, "Alpha world should have GM, scout, and several players.");
    }

    public void AlphaRosterExists()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.Equal("roster-alpha-001", snapshot.Roster.RosterId);
        Assert.True(snapshot.Roster.Players.Count > 0, "Alpha roster should contain players.");
    }

    public void AlphaRecruitsExist()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.True(snapshot.Recruits.Count >= 3, "Alpha world should include recruits.");
    }

    public void AlphaScoutExists()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.Equal("scout-001", snapshot.Scout.ScoutId);
        Assert.Equal(snapshot.ScoutPerson.Identity.DisplayName, snapshot.Scout.Name);
    }

    public void AlphaOwnerExists()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.Equal("owner-001", snapshot.Owner.OwnerId);
        Assert.Equal(snapshot.OrganizationId, snapshot.Owner.OrganizationId);
    }

    public void AlphaDraftBoardExists()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.Equal("draft-board-alpha-001", snapshot.DraftBoard.BoardId);
        Assert.True(snapshot.DraftBoard.Entries.Count >= 3, "Alpha draft board should include recruit prospects.");
    }

    public void AdvanceOneDayAdvancesDate()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.Equal(new DateOnly(2026, 9, 2), result.CurrentDate);
        Assert.Equal(new DateOnly(2026, 9, 2), result.WorldSnapshot.CurrentDate);
    }

    public void SimulationResultIsReturned()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(!string.IsNullOrWhiteSpace(result.Summary), "Alpha simulation result should include summary text.");
        Assert.True(result.WorldSnapshot.People.Count > 0, "Alpha simulation result should include a world snapshot.");
    }

    public void EventQueueIsProcessed()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.True(registry.EventEngine.Queue.Count > 0, "Bootstrap should queue at least one event.");
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(result.ProcessedEventCount > 0, "Alpha daily simulation should process queued events.");
        Assert.Equal(0, registry.EventEngine.Queue.Count);
    }

    public void InboxItemsCanBeGenerated()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(result.InboxItems.Count > 0, "Important processed events should become inbox items.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.OwnerGoalSet), "Owner goal event should become an inbox item.");
    }

    public void RegistrySharesEventEngine()
    {
        var (registry, _) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.True(ReferenceEquals(registry.EventEngine, registry.WorldEngine.EventEngine), "Registry should share EventEngine with WorldEngine.");
        Assert.True(ReferenceEquals(registry.EventEngine, registry.RosterEngine.EventEngine), "Registry should share EventEngine with RosterEngine.");
        Assert.True(ReferenceEquals(registry.EventEngine, registry.RecruitingEngine.EventEngine), "Registry should share EventEngine with RecruitingEngine.");
    }

    public void IntegrationLayerHasNoUiOrGodotDependency()
    {
        var integrationFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in integrationFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Integration layer should not reference Godot.");
            Assert.False(text.Contains("Control", StringComparison.Ordinal), "Integration layer should not define UI controls.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
