using LegacyEngine.Integration;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Scouting;

internal sealed class GmCreationAndActionsTests
{
    public void GmCreationCreatesPerson()
    {
        var result = new GmProfileFactory().Create(CreateSettings(), new DateOnly(2026, 6, 15));

        Assert.Equal("person-player-gm-001", result.Person.PersonId);
        Assert.Equal("Brad", result.Person.Identity.FirstName);
        Assert.Equal("Bradley", result.PreferredName);
    }

    public void CreatedGmReplacesDefaultGm()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario(new NewGmScenarioSettings
        {
            GmCreationSettings = CreateSettings()
        });

        Assert.Equal("Brad Testerman", scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName);
        Assert.True(!scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName.Contains("Jordan", StringComparison.Ordinal), "Created GM should replace fallback GM.");
    }

    public void ScenarioUsesCreatedGmName()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario(new NewGmScenarioSettings
        {
            GmCreationSettings = CreateSettings()
        });

        Assert.True(scenario.Summary.Contains("Bradley", StringComparison.Ordinal), "Scenario summary should use preferred GM name.");
        Assert.True(scenario.FirstDayInbox.Any(item => item.Summary.Contains("Bradley", StringComparison.Ordinal)), "Inbox should use preferred GM name.");
    }

    public void FallbackGmStillWorks()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        Assert.Equal("Jordan Hayes", scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName);
    }

    public void GmStyleMapsToHumanProfile()
    {
        var result = new GmProfileFactory().Create(CreateSettings() with { Style = GmStyle.AggressiveBuilder }, new DateOnly(2026, 6, 15));

        Assert.True(result.HumanIntelligenceProfile.RiskTolerance > 70, "Aggressive builder style should raise risk tolerance.");
        Assert.True(result.Person.Personality.Ambition > 70, "Aggressive builder style should raise ambition.");
    }

    public void DraftBoardRerankWorks()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var second = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).Skip(1).First();
        var result = new NewGmScenarioActions().MoveDraftBoardPlayer(scenario.Registry, scenario.ScenarioSnapshot, second.ProspectPersonId, -1);

        var moved = result.AlphaSnapshot.DraftBoard.Entries.Single(entry => entry.ProspectPersonId == second.ProspectPersonId);
        Assert.Equal(1, moved.Rank);
    }

    public void ScoutFocusAssignmentWorks()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var result = new NewGmScenarioActions().AssignScoutFocus(scenario.Registry, scenario.ScenarioSnapshot, ScoutSpecialty.Character);

        Assert.Equal(1, result.ScenarioSnapshot.ScoutingAssignments.Count);
        Assert.True(result.ScenarioSnapshot.ScoutingAssignments[0].FocusAreas.Contains(ScoutSpecialty.Character), "Scouting assignment should store the chosen focus.");
    }

    public void RecruitingOfferWorks()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits[0];
        var result = new NewGmScenarioActions().MakeRecruitingOffer(scenario.Registry, scenario.ScenarioSnapshot, recruit.RecruitPersonId);

        var updated = result.AlphaSnapshot.Recruits.Single(item => item.RecruitPersonId == recruit.RecruitPersonId);
        Assert.Equal(RecruitStatus.Offered, updated.Status);
    }

    public void ActionsCreateEventsMessagesAndInboxItems()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits[0];
        var result = new NewGmScenarioActions().MakeRecruitingOffer(scenario.Registry, scenario.ScenarioSnapshot, recruit.RecruitPersonId);

        Assert.True(scenario.Registry.EventEngine.Queue.Count > 0, "Action should queue an event.");
        Assert.True(result.InboxItems.Count > 0, "Action should produce an immediate inbox item.");
    }

    public void AdvanceDayStillWorksAfterAction()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruit = scenario.AlphaSnapshot.Recruits[0];
        var action = new NewGmScenarioActions().MakeRecruitingOffer(scenario.Registry, scenario.ScenarioSnapshot, recruit.RecruitPersonId);
        var result = new DailySimulationCoordinator().AdvanceOneDay(scenario.Registry, action.AlphaSnapshot);

        Assert.Equal(new DateOnly(2026, 6, 16), result.CurrentDate);
        Assert.True(result.InboxItems.Count > 0, "Queued action event should become an inbox item after advancing.");
    }

    public void GmCreationHasNoGodotDependency()
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

    private static GmProfileCreationSettings CreateSettings() =>
        new(
            FirstName: "Brad",
            LastName: "Testerman",
            PreferredName: "Bradley",
            Gender: Gender.Male,
            BirthDate: null,
            Age: 41,
            Nationality: "Canada",
            Birthplace: "Saskatoon, SK",
            Background: GmBackground.Scout,
            Style: GmStyle.ScoutDriven,
            Strengths: new[] { "scouting", "communication" },
            Weaknesses: new[] { "contract negotiation" });

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
