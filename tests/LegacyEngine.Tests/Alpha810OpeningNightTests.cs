using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.World;

internal sealed class Alpha810OpeningNightTests
{
    public void PreviewExplainsBlockedOpeningNight()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var preview = new OpeningNightService().BuildPreview(created.Registry, created.ScenarioSnapshot);

        Assert.Equal(OpeningNightStatus.Blocked, preview.Status);
        Assert.True(preview.OpeningNightOn > created.ScenarioSnapshot.CurrentDate, "Opening night should come from the season calendar.");
        Assert.True(preview.ActiveRosterCount > 0, "Preview should include the inherited roster.");
        Assert.False(preview.CanBegin, "The default offseason scenario should require GM decisions first.");
        Assert.True(preview.Summary.Length > 20, "Blocked previews should explain the state in plain language.");
    }

    public void ReadyOrganizationBeginsSeasonWithBriefing()
    {
        var ready = ReadyScenario();
        var service = new OpeningNightService();
        var result = service.Begin(ready.Registry, ready.ScenarioSnapshot);

        Assert.True(result.Success, result.Message);
        Assert.Equal(OpeningNightStatus.Begun, result.Preview.Status);
        Assert.True(result.ScenarioSnapshot.SeasonReadiness.SeasonBegun, "Opening Night should mark the season as begun.");
        Assert.Equal(SeasonPhase.RegularSeason, result.ScenarioSnapshot.Season.CurrentPhase);
        Assert.Equal(WorldPhase.RegularSeason, result.ScenarioSnapshot.AlphaSnapshot.WorldState.CurrentPhase);
        Assert.Equal(1, result.InboxItems.Count(item => item.Title == "Opening Night Briefing"));
        Assert.Equal(ready.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count, result.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count);
    }

    public void BeginSeasonDoesNotApproveOrMovePlayers()
    {
        var ready = ReadyScenario();
        var beforeRoster = ready.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count;
        var beforeContracts = ready.ScenarioSnapshot.Contracts.Count;
        var result = new OpeningNightService().Begin(ready.Registry, ready.ScenarioSnapshot);

        Assert.Equal(beforeRoster, result.ScenarioSnapshot.AlphaSnapshot.Roster.CurrentPlayers.Count);
        Assert.Equal(beforeContracts, result.ScenarioSnapshot.Contracts.Count);
        Assert.False(result.ScenarioSnapshot.PendingActions.Any(action => action.Status == PendingGmActionStatus.Completed), "Opening Night must not complete GM actions.");
    }

    public void OpeningNightStatePreservesThroughSaveLoad()
    {
        var ready = ReadyScenario();
        var started = new OpeningNightService().Begin(ready.Registry, ready.ScenarioSnapshot);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha810-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(started.ScenarioSnapshot, ready.Registry.Rulebook!);
        var saved = new SaveGameService().SaveCareer(started.ScenarioSnapshot, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, ready.Registry.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(started.ScenarioSnapshot.OpeningNight, loaded.SaveGame!.ScenarioSnapshot.OpeningNight);
        Assert.True(loaded.SaveGame.ScenarioSnapshot.SeasonReadiness.SeasonBegun, "Loaded career should remain in the regular season.");
    }

    public void ActionCenterExposesSeasonLaunchWhenReady()
    {
        var ready = ReadyScenario();
        var readiness = new SeasonReadinessService().Evaluate(ready.Registry, ready.ScenarioSnapshot);
        var budget = new BudgetOverviewService().Build(ready.ScenarioSnapshot, ready.Registry.Rulebook!);
        var items = new ActionCenterService().BuildItems(ready.ScenarioSnapshot, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.ActionCenterItemId == "action-center:opening-night:begin"), "Ready organizations should have a clear season-launch action.");
    }

    public void DesktopExposesOpeningNightSurface()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Opening Night", StringComparison.Ordinal), "Desktop should expose the Opening Night workspace.");
        Assert.True(source.Contains("Opening Night Preview", StringComparison.Ordinal), "Desktop should show an explicit launch preview.");
        Assert.True(source.Contains("MessageBoxButton.YesNo", StringComparison.Ordinal), "Season start should require explicit confirmation.");
    }

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyScenario()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var roster = created.ScenarioSnapshot.AlphaSnapshot.Roster;
        var rulebook = WithRosterRules(
            RulebookPresets.Create(DraftLeaguePreset.JuniorMajor),
            new RosterRules
            {
                MinRoster = roster.CurrentPlayers.Count,
                MaxRoster = roster.CurrentPlayers.Count,
                ActiveRoster = roster.ActivePlayers.Count,
                GoaliesRequired = roster.CurrentPlayers.Count(player => player.IsGoalie),
                OverageSlots = Math.Max(3, roster.CurrentPlayers.Count(player => player.IsOverage())),
                ImportSlots = Math.Max(2, roster.CurrentPlayers.Count(player => player.IsImport)),
                InjuredReserveEnabled = true,
                ReserveListEnabled = true
            });
        var registry = created.Registry with { Rulebook = rulebook };
        var camp = new TrainingCamp(
            "camp-alpha810-ready",
            created.ScenarioSnapshot.Organization.OrganizationId,
            created.ScenarioSnapshot.CurrentDate,
            Array.Empty<TrainingCampPlayer>(),
            Array.Empty<TrainingCampEvaluation>(),
            CompletedOn: created.ScenarioSnapshot.CurrentDate);
        var snapshot = created.ScenarioSnapshot with
        {
            TrainingCamp = camp,
            PendingActions = Array.Empty<PendingGmAction>(),
            ProspectRights = Array.Empty<DraftRightsRecord>(),
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true)
        };
        snapshot.Validate();
        return (registry, snapshot);
    }

    private static Rulebook WithRosterRules(Rulebook source, RosterRules rosterRules) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = rosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules,
            SeasonRules = source.SeasonRules,
            AffiliateRules = source.AffiliateRules,
            FreeAgentRightsRules = source.FreeAgentRightsRules,
            ArbitrationRules = source.ArbitrationRules,
            SalaryCapRules = source.SalaryCapRules,
            WaiverRules = source.WaiverRules
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be found.");
    }
}
