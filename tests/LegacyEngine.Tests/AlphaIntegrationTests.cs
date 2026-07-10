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

    public void BootstrapCreatesPipelineWorldObjects()
    {
        var (_, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.True(snapshot.CoachPerson is not null, "Alpha world should include a coach person.");
        Assert.True(snapshot.Relationships.Count > 0, "Alpha world should include relationships.");
        Assert.True(snapshot.DevelopmentProfiles.Count > 0, "Alpha world should include development profiles.");
        Assert.True(snapshot.Injuries.Count > 0, "Alpha world should include injury records.");
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
        Assert.True(result.LogEntries.Count > 0, "Alpha simulation result should include pipeline log entries.");
    }

    public void AdvanceMultipleDaysChangesDateCorrectly()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var results = new DailySimulationCoordinator().AdvanceDays(registry, snapshot, 7);

        Assert.Equal(7, results.Count);
        Assert.Equal(new DateOnly(2026, 9, 8), results[^1].CurrentDate);
    }

    public void EventQueueIsProcessed()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));

        Assert.True(registry.EventEngine.Queue.Count > 0, "Bootstrap should queue at least one event.");
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(result.ProcessedEventCount > 0, "Alpha daily simulation should process queued events.");
        Assert.True(registry.EventEngine.History.Count >= result.ProcessedEventCount, "Processed events should be archived into event history.");
    }

    public void InboxItemsCanBeGenerated()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(result.InboxItems.Count > 0, "Important processed events should become inbox items.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.OwnerGoalSet), "Owner goal event should become an inbox item.");
    }

    public void RelationshipDecayRuns()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var originalTrust = snapshot.Relationships[0].Trust;
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(result.WorldSnapshot.Relationships[0].Trust < originalTrust, "Relationship trust should decay toward neutral.");
        Assert.True(result.LogEntries.Any(item => item.Step == DailySimulationStep.ApplyRelationshipDecay), "Relationship decay step should be logged.");
    }

    public void DevelopmentUpdateStepRuns()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.Equal(result.CurrentDate, result.WorldSnapshot.DevelopmentProfiles[0].LastUpdated);
        Assert.True(result.LogEntries.Any(item => item.Step == DailySimulationStep.ApplyPlayerDevelopmentUpdates), "Development update step should be logged.");
    }

    public void InjuryRecoveryStepRuns()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var originalProgress = snapshot.Injuries[0].RecoveryProgress;
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(result.WorldSnapshot.Injuries[0].RecoveryProgress > originalProgress, "Injury recovery should progress.");
        Assert.True(result.LogEntries.Any(item => item.Step == DailySimulationStep.ApplyInjuryRecoveryUpdates), "Injury recovery step should be logged.");
    }

    public void CommunicationMessagesGeneratedFromImportantEvents()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.True(result.CommunicationMessages.Count > 0, "Important events should generate communication messages.");
        Assert.True(result.CommunicationMessages.Any(item => item.SourceEventType == LegacyEventType.PlayerInjured), "Player injury event should generate a communication message.");
    }

    public void PipelineOrderIsStable()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);
        var steps = result.LogEntries.Select(item => item.Step).ToArray();

        Assert.Equal(DailySimulationStep.AdvanceWorldClock, steps[0]);
        Assert.Equal(DailySimulationStep.ProcessQueuedEvents, steps[1]);
        Assert.Equal(DailySimulationStep.ApplyRelationshipDecay, steps[2]);
        Assert.Equal(DailySimulationStep.ApplyPlayerDevelopmentUpdates, steps[3]);
        Assert.Equal(DailySimulationStep.ApplyInjuryRecoveryUpdates, steps[4]);
        Assert.Equal(DailySimulationStep.CheckContractStatuses, steps[5]);
        Assert.Equal(DailySimulationStep.ProgressRecruiting, steps[6]);
        Assert.Equal(DailySimulationStep.GenerateCommunicationMessages, steps[7]);
        Assert.Equal(DailySimulationStep.ConvertInboxItems, steps[8]);
        Assert.Equal(DailySimulationStep.ReturnSimulationResult, steps[9]);
    }

    public void PipelineResultContainsSnapshotDateLogAndInbox()
    {
        var (registry, snapshot) = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        var result = new DailySimulationCoordinator().AdvanceOneDay(registry, snapshot);

        Assert.Equal(result.CurrentDate, result.WorldSnapshot.CurrentDate);
        Assert.True(result.LogEntries.Count == 10, "Daily pipeline should return all pipeline log entries.");
        Assert.True(result.InboxItems.Count > 0, "Daily pipeline should return inbox items.");
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
            Assert.False(text.Contains("System.Windows", StringComparison.Ordinal), "Integration layer should not reference desktop UI frameworks.");
            Assert.False(text.Contains("System.Windows.Controls", StringComparison.Ordinal), "Integration layer should not define UI controls.");
            Assert.False(text.Contains("Avalonia", StringComparison.OrdinalIgnoreCase), "Integration layer should not reference Avalonia UI.");
            Assert.False(text.Contains("UIElement", StringComparison.Ordinal), "Integration layer should not define UI controls.");
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
