using LegacyEngine.Integration;
using LegacyEngine.Seasons;

internal sealed class NewGmScenarioTests
{
    public void ScenarioStartsTwoWeeksBeforeDraft()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal(new DateOnly(2026, 6, 15), scenario.ScenarioSnapshot.CurrentDate);
        Assert.Equal(new DateOnly(2026, 6, 29), scenario.ScenarioSnapshot.DraftDate);
        Assert.Equal(14, scenario.ScenarioSnapshot.DaysUntilDraft);
    }

    public void PlayerGmExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal("person-player-gm-001", scenario.AlphaSnapshot.GeneralManager.PersonId);
        Assert.True(
            scenario.AlphaSnapshot.GeneralManager.Roles.Any(role => role.Title == "General Manager"),
            "Player GM should hold the GM role.");
    }

    public void OrganizationExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal("Prairie Falcons", scenario.ScenarioSnapshot.Organization.Name);
        Assert.Equal(scenario.AlphaSnapshot.OrganizationId, scenario.ScenarioSnapshot.Organization.OrganizationId);
        Assert.True(scenario.ScenarioSnapshot.Organization.Culture.DevelopmentFocus > 0, "Organization culture should be populated.");
    }

    public void OwnerExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal("owner-prairie-falcons", scenario.AlphaSnapshot.Owner.OwnerId);
        Assert.True(scenario.AlphaSnapshot.Owner.Goals.Count >= 3, "Owner expectations should be populated.");
    }

    public void StaffExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var roles = scenario.ScenarioSnapshot.StaffMembers.Select(member => member.CurrentRole).ToArray();

        Assert.True(roles.Contains(LegacyEngine.Staff.StaffRole.HeadCoach), "Scenario should include a head coach.");
        Assert.True(roles.Contains(LegacyEngine.Staff.StaffRole.AssistantCoach), "Scenario should include an assistant coach.");
        Assert.True(roles.Contains(LegacyEngine.Staff.StaffRole.HeadScout), "Scenario should include a head scout.");
        Assert.True(roles.Contains(LegacyEngine.Staff.StaffRole.Scout), "Scenario should include at least one scout.");
    }

    public void FullRosterExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal(22, scenario.AlphaSnapshot.Roster.Players.Count);
    }

    public void RecruitPoolExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.True(scenario.AlphaSnapshot.Recruits.Count >= 8, "Scenario should include a recruit pool.");
    }

    public void DraftBoardExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal(scenario.AlphaSnapshot.Recruits.Count, scenario.AlphaSnapshot.DraftBoard.Entries.Count);
    }

    public void RelationshipsExist()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.True(scenario.AlphaSnapshot.Relationships.Count >= 8, "Scenario should include GM, owner, staff, and scout relationships.");
    }

    public void FirstDayInboxIsPopulated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.True(scenario.FirstDayInbox.Count >= 4, "Scenario should preload first-day inbox items.");
        Assert.True(scenario.FirstDayInbox.Any(item => item.Title.Contains("Welcome", StringComparison.Ordinal)), "Owner welcome message should be present.");
        Assert.True(scenario.FirstDayInbox.Any(item => item.Title.Contains("Draft", StringComparison.Ordinal)), "Draft timeline or board message should be present.");
    }

    public void SeasonPhaseAndDateAreCorrect()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal(SeasonPhase.Offseason, scenario.ScenarioSnapshot.Season.CurrentPhase);
        Assert.Equal(scenario.ScenarioSnapshot.CurrentDate, scenario.ScenarioSnapshot.Season.CurrentDate.Value);
    }

    public void AdvanceDayStillWorks()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var result = new DailySimulationCoordinator().AdvanceOneDay(scenario.Registry, scenario.AlphaSnapshot);

        Assert.Equal(new DateOnly(2026, 6, 16), result.CurrentDate);
        Assert.True(result.WorldSnapshot.Season is not null, "Scenario season should advance with the snapshot.");
    }

    public void PlaytestSurfacesCanReadScenarioSnapshot()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var snapshot = scenario.AlphaSnapshot;

        Assert.True(snapshot.Organization is not null, "Alpha snapshot should expose organization data.");
        Assert.True(snapshot.Season is not null, "Alpha snapshot should expose season data.");
        Assert.True(snapshot.StaffMembers.Count > 0, "Alpha snapshot should expose staff data.");
        Assert.True(snapshot.Contracts.Count > 0, "Alpha snapshot should expose contract references.");
        Assert.True(scenario.ScenarioSnapshot.FirstDayInbox.Count > 0, "Scenario snapshot should expose first-day inbox data.");
    }

    public void ScenarioHasNoGodotDependency()
    {
        var integrationFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in integrationFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Integration layer should not reference Godot.");
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
