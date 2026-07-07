using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha35SaveLoadTests
{
    public void SaveFileCreated()
    {
        var saved = SavePreparedCareer();

        Assert.True(File.Exists(saved.FilePath), "Save file should be created.");
        Assert.True(new FileInfo(saved.FilePath!).Length > 0, "Save file should contain JSON.");
    }

    public void SaveMetadataWritten()
    {
        var saved = SavePreparedCareer();

        Assert.Equal(SaveGameVersion.CurrentSaveFormatVersion, saved.SaveGame!.Metadata.Version.SaveFormatVersion);
        Assert.True(saved.SaveGame.Metadata.GmName.Length > 0, "Metadata should include GM name.");
        Assert.True(saved.SaveGame.Metadata.TeamName.Length > 0, "Metadata should include team name.");
        Assert.Equal(saved.SaveGame.ScenarioSnapshot.CurrentDate, saved.SaveGame.Metadata.CurrentDate);
    }

    public void LoadRestoresCurrentDate()
    {
        var loaded = LoadPreparedCareer();

        Assert.Equal(loaded.Saved.SaveGame!.ScenarioSnapshot.CurrentDate, loaded.Loaded.SaveGame!.ScenarioSnapshot.CurrentDate);
    }

    public void LoadRestoresRoster()
    {
        var loaded = LoadPreparedCareer();

        Assert.Equal(
            loaded.Saved.SaveGame!.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count,
            loaded.Loaded.SaveGame!.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Count);
    }

    public void LoadRestoresStaff()
    {
        var loaded = LoadPreparedCareer();

        Assert.Equal(
            loaded.Saved.SaveGame!.ScenarioSnapshot.StaffMembers.Count,
            loaded.Loaded.SaveGame!.ScenarioSnapshot.StaffMembers.Count);
    }

    public void LoadRestoresInboxStatuses()
    {
        var loaded = LoadPreparedCareer();
        var savedMessage = loaded.Saved.SaveGame!.InboxMessages.First(message => message.Status == InboxMessageStatus.Archived);
        var loadedMessage = loaded.Loaded.SaveGame!.InboxMessages.Single(message => message.InboxItemId == savedMessage.InboxItemId);

        Assert.Equal(InboxMessageStatus.Archived, loadedMessage.Status);
    }

    public void LoadRestoresPendingActions()
    {
        var loaded = LoadPreparedCareer();

        Assert.True(loaded.Loaded.SaveGame!.ScenarioSnapshot.PendingActions.Any(action => action.IsOpen), "Pending GM actions should survive save/load.");
    }

    public void LoadRestoresScheduleStandingsAndStats()
    {
        var loaded = LoadPreparedCareer();
        var scenario = loaded.Loaded.SaveGame!.ScenarioSnapshot;

        Assert.True(scenario.Schedule is not null && scenario.Schedule.Games.Count > 0, "Schedule should be restored.");
        Assert.True(scenario.Standings is not null && scenario.Standings.Teams.Count > 0, "Standings should be restored.");
        Assert.True(scenario.PlayerStats.Count > 0, "Player stats should be restored.");
        Assert.True(scenario.GoalieStats.Count > 0, "Goalie stats should be restored.");
    }

    public void LoadRestoresCareerHistory()
    {
        var loaded = LoadPreparedCareer();

        Assert.True(loaded.Loaded.SaveGame!.ScenarioSnapshot.CareerTimeline.Entries.Count > 0, "Career history should be restored.");
        Assert.Equal(
            loaded.Saved.SaveGame!.ScenarioSnapshot.CareerTimeline.Entries.Count,
            loaded.Loaded.SaveGame.ScenarioSnapshot.CareerTimeline.Entries.Count);
    }

    public void LoadRestoresDraftHistory()
    {
        var loaded = LoadPreparedCareer();

        Assert.True(loaded.Loaded.SaveGame!.ScenarioSnapshot.DraftPickHistory.Count > 0, "Draft history should be restored.");
        Assert.Equal(
            loaded.Saved.SaveGame!.ScenarioSnapshot.DraftPickHistory.Count,
            loaded.Loaded.SaveGame.ScenarioSnapshot.DraftPickHistory.Count);
    }

    public void LoadRestoresGmProfileAndTeam()
    {
        var loaded = LoadPreparedCareer();

        Assert.Equal(
            loaded.Saved.SaveGame!.ScenarioSnapshot.GeneralManagerProfile.Person.Identity.DisplayName,
            loaded.Loaded.SaveGame!.ScenarioSnapshot.GeneralManagerProfile.Person.Identity.DisplayName);
        Assert.Equal(
            loaded.Saved.SaveGame.ScenarioSnapshot.Organization.Name,
            loaded.Loaded.SaveGame.ScenarioSnapshot.Organization.Name);
    }

    public void VersionMismatchHandledClearly()
    {
        var saved = SavePreparedCareer();
        var mismatched = File.ReadAllText(saved.FilePath!).Replace(SaveGameVersion.CurrentSaveFormatVersion, "alpha-save-old");
        File.WriteAllText(saved.FilePath!, mismatched);

        var loaded = new SaveGameService().LoadFromFile(saved.FilePath!, RulebookPresets.CreateJuniorMajor());

        Assert.False(loaded.Success, "Version mismatch should not load.");
        Assert.True(loaded.Message.Contains("not compatible", StringComparison.OrdinalIgnoreCase), "Version mismatch should explain compatibility.");
        Assert.True(!string.IsNullOrWhiteSpace(loaded.CompatibilityWarning), "Version mismatch should include a warning.");
    }

    public void AlphaDesktopExposesSaveLoadControls()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Save Career", StringComparison.Ordinal), "Desktop should expose Save Career.");
        Assert.True(source.Contains("Load Career", StringComparison.Ordinal), "Desktop should expose Load Career.");
        Assert.True(source.Contains("Save As", StringComparison.Ordinal), "Desktop should expose Save As.");
        Assert.True(source.Contains("Last saved", StringComparison.Ordinal), "Desktop should show save status.");
    }

    public void Alpha35HasNoGodotDatabaseOrCloudSave()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Save*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Save/load should not add Godot.");
        Assert.False(text.Contains("DbContext", StringComparison.Ordinal), "Save/load should not add database persistence.");
        Assert.False(text.Contains("Cloud", StringComparison.Ordinal), "Save/load should not add cloud save.");
    }

    private static (SaveLoadResult Saved, SaveLoadResult Loaded) LoadPreparedCareer()
    {
        var saved = SavePreparedCareer();
        var loaded = new SaveGameService().LoadFromFile(saved.FilePath!, RulebookPresets.CreateJuniorMajor());

        Assert.True(loaded.Success, loaded.Message);
        Assert.True(loaded.Registry is not null, "Load should restore an engine registry.");
        return (saved, loaded);
    }

    private static SaveLoadResult SavePreparedCareer()
    {
        var service = new SaveGameService();
        var scenario = PreparedScenario();
        var inbox = new InboxManager();
        inbox.AddRange(scenario.FirstDayInbox);
        var archived = inbox.Query(new InboxFilter()).First();
        inbox.ApplyAction(archived.InboxItemId, InboxMessageAction.Archive);

        var leagueNews = new[]
        {
            new LeagueTransaction(
                "transaction:test-save-load",
                new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 12, 0, 0, TimeSpan.Zero),
                "org-test",
                "Regina Plainsmen",
                "person-test",
                "Test Player",
                LeagueTransactionType.PlayerSigned,
                LeagueNewsCategory.Signings,
                "Regina signed Test Player.")
        };
        var actionStatuses = new Dictionary<string, ActionCenterStatus>(StringComparer.Ordinal)
        {
            ["action:test-save-load"] = ActionCenterStatus.Deferred
        };
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha35-{Guid.NewGuid():N}.json");

        var saved = service.SaveCareer(
            scenario,
            inbox.AllMessages,
            leagueNews,
            actionStatuses,
            budget,
            path,
            "Alpha 3.5 Test Save");

        Assert.True(saved.Success, saved.Message);
        return saved;
    }

    private static NewGmScenarioSnapshot PreparedScenario()
    {
        var created = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var drafted = new AlphaDraftExperienceService().SimulateToCompletion(created.Registry, created.ScenarioSnapshot).ScenarioSnapshot;
        var seasonReady = new SeasonFrameworkService().EnsureSeasonFramework(created.Registry, drafted);
        var recruit = seasonReady.AlphaSnapshot.Recruits.First();
        return new PendingGmActionService()
            .CreateForRecruitCommitment(created.Registry, seasonReady, recruit.RecruitPersonId)
            .ScenarioSnapshot;
    }

    private static NewGmScenarioResult AdvanceToDraftDay(NewGmScenarioResult scenario)
    {
        var snapshot = scenario.AlphaSnapshot;
        var scenarioSnapshot = scenario.ScenarioSnapshot;
        var coordinator = new DailySimulationCoordinator();

        while (snapshot.CurrentDate < scenarioSnapshot.DraftDate)
        {
            var result = coordinator.AdvanceOneDay(scenario.Registry, snapshot);
            snapshot = result.WorldSnapshot;
            scenarioSnapshot = scenarioSnapshot with
            {
                AlphaSnapshot = snapshot,
                Season = snapshot.Season ?? scenarioSnapshot.Season
            };
        }

        return scenario with
        {
            AlphaSnapshot = snapshot,
            ScenarioSnapshot = scenarioSnapshot
        };
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
