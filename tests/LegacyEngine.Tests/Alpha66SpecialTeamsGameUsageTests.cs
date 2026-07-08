using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

public sealed class Alpha66SpecialTeamsGameUsageTests
{
    public void PowerPlayAssignmentsWork()
    {
        var scenario = CreateScenario();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Position == RosterPosition.Center && assignment.Slot != LineupSlot.HealthyScratch);

        var result = new GameUsageService().AssignPowerPlaySlot(scenario, 1, PowerPlaySlot.Center, player.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(player.PersonId, result.ScenarioSnapshot.CurrentGameUsage!.SpecialTeams.PowerPlayUnits.First(unit => unit.UnitNumber == 1).Center!.PersonId);
    }

    public void PenaltyKillAssignmentsWork()
    {
        var scenario = CreateScenario();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Position == RosterPosition.Defense && assignment.Slot != LineupSlot.HealthyScratch);

        var result = new GameUsageService().AssignPenaltyKillSlot(scenario, 1, PenaltyKillSlot.LeftDefense, player.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(player.PersonId, result.ScenarioSnapshot.CurrentGameUsage!.SpecialTeams.PenaltyKillUnits.First(unit => unit.UnitNumber == 1).LeftDefense!.PersonId);
    }

    public void GoalieUsageGenerated()
    {
        var usage = CreateScenario().CurrentGameUsage!;

        Assert.True(usage.GoalieUsage.Count >= 1, "Game usage should include goalie usage profiles.");
        Assert.True(usage.GoalieUsage.All(goalie => goalie.ExpectedStarts > 0), "Goalie usage should track expected starts.");
        Assert.True(usage.GoalieUsage.All(goalie => !string.IsNullOrWhiteSpace(goalie.RestRecommendation)), "Goalie usage should include rest recommendations.");
    }

    public void ShootoutOrderCanChange()
    {
        var scenario = CreateScenario();
        var second = scenario.CurrentGameUsage!.SpecialTeams.ShootoutOrder.Shooters.Skip(1).First();

        var result = new GameUsageService().MoveShootoutPlayer(scenario, second.PersonId, -1);

        Assert.True(result.Success, result.Message);
        Assert.Equal(second.PersonId, result.ScenarioSnapshot.CurrentGameUsage!.SpecialTeams.ShootoutOrder.Shooters.First().PersonId);
    }

    public void UsageSummaryGenerated()
    {
        var usage = CreateScenario().CurrentGameUsage!;

        Assert.True(usage.PlayerProfiles.Count > 0, "Game usage should create player profiles.");
        Assert.True(usage.PlayerProfiles.Any(profile => profile.PowerPlayUsage.Contains("PP", StringComparison.Ordinal)), "Some players should have PP usage.");
        Assert.True(usage.PlayerProfiles.Any(profile => profile.PenaltyKillUsage.Contains("PK", StringComparison.Ordinal)), "Some players should have PK usage.");
    }

    public void CoachRecommendationsGenerated()
    {
        var usage = CreateScenario().CurrentGameUsage!;

        Assert.True(usage.CoachRecommendations.Count > 0, "Game usage should create coach recommendations.");
        Assert.True(usage.CoachRecommendations.All(recommendation => !string.IsNullOrWhiteSpace(recommendation.SuggestedAction)), "Recommendations should be explainable.");
    }

    public void PlayerDossierIncludesGameUsage()
    {
        var scenario = CreateScenario();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot != LineupSlot.HealthyScratch);

        var dossier = new PlayerDossierService().CreateDossier(scenario, player.PersonId);

        Assert.True(dossier.Sections.Any(section => section.Title == "Game Usage"), "Player dossier should expose a Game Usage section.");
    }

    public void DevelopmentModifierAvailable()
    {
        var scenario = CreateScenario();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot != LineupSlot.HealthyScratch);

        var impact = new GameUsageService().BuildDevelopmentImpact(scenario, player.PersonId);

        Assert.True(impact.Modifier is >= -10 and <= 10, "Game usage development modifier should stay modest.");
        Assert.True(impact.Summary.Contains("Game usage", StringComparison.Ordinal), impact.Summary);
    }

    public void DashboardExposesGameUsage()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Game Usage", StringComparison.Ordinal), "Dashboard/Lineup UI should expose game usage.");
        Assert.True(source.Contains("Power Play", StringComparison.Ordinal), "Lineup UI should expose power play usage.");
        Assert.True(source.Contains("Penalty Kill", StringComparison.Ordinal), "Lineup UI should expose penalty kill usage.");
        Assert.True(source.Contains("Shootout Order", StringComparison.Ordinal), "Lineup UI should expose shootout order.");
    }

    public void ActionCenterIncludesImportantGameUsage()
    {
        var created = CreateNhlScenario();
        var scenario = CreateScenario();
        var usage = scenario.CurrentGameUsage!;
        var recommendation = new GameUsageCoachRecommendation(
            "game-usage-rec:test-pp",
            GameUsageRecommendationType.ImprovePowerPlayBalance,
            null,
            "Power Play",
            "Power Play Unit 1 lacks balance.",
            "Review PP1 personnel before the next game.",
            true);
        scenario = scenario with
        {
            CurrentGameUsage = usage with
            {
                CoachRecommendations = new[] { recommendation }
            }
        };

        var items = BuildActionCenterItems(created.Registry, scenario);

        Assert.True(items.Any(item => item.Title.Contains("PP needs adjustment", StringComparison.Ordinal)), "Important game usage recommendation should reach Action Center.");
    }

    public void NoHiddenRatingsExposed()
    {
        var usage = CreateScenario().CurrentGameUsage!;
        var rendered = string.Join(Environment.NewLine, usage.PlayerProfiles.Select(profile => $"{profile.UsageSummary} {profile.CoachComment}"));

        Assert.False(rendered.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase), "Game usage output should not expose hidden current ability.");
        Assert.False(rendered.Contains("Potential =", StringComparison.OrdinalIgnoreCase), "Game usage output should not expose hidden potential ratings.");
        Assert.False(rendered.Contains("hidden rating", StringComparison.OrdinalIgnoreCase), "Game usage output should not expose hidden ratings.");
    }

    public void NoTacticsGameSimulationOrGodotAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join(Environment.NewLine, new[]
        {
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "GameUsageModels.cs")),
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "GameUsageService.cs"))
        });

        Assert.False(text.Contains("Forecheck", StringComparison.OrdinalIgnoreCase), "Alpha 6.6 should not add forecheck tactics.");
        Assert.False(text.Contains("NeutralZone", StringComparison.OrdinalIgnoreCase), "Alpha 6.6 should not add neutral-zone tactics.");
        Assert.False(text.Contains("FaceoffStrategy", StringComparison.OrdinalIgnoreCase), "Alpha 6.6 should not add faceoff strategy.");
        Assert.False(text.Contains("LineMatching", StringComparison.OrdinalIgnoreCase), "Alpha 6.6 should not add line matching.");
        Assert.False(text.Contains("MatchupEngine", StringComparison.OrdinalIgnoreCase), "Alpha 6.6 should not add matchup engine logic.");
        Assert.False(text.Contains("BasicGameSimulator", StringComparison.OrdinalIgnoreCase), "Alpha 6.6 should not overhaul game simulation.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 6.6 should not add Godot.");
    }

    private static NewGmScenarioSnapshot CreateScenario() =>
        new GameUsageService().EnsureGameUsage(new LineChemistryService().EnsureChemistry(CreateNhlScenario().ScenarioSnapshot));

    private static NewGmScenarioResult CreateNhlScenario()
    {
        var service = new MultiLeagueCareerService();
        return service.CreateScenario(service.SelectLeagueAndTeam(LeagueExperience.Nhl, "org-seattle-cascades"));
    }

    private static IReadOnlyList<ActionCenterItem> BuildActionCenterItems(EngineRegistry registry, NewGmScenarioSnapshot snapshot)
    {
        var inbox = new InboxManager();
        inbox.AddRange(snapshot.FirstDayInbox);
        return new ActionCenterService().BuildItems(
            snapshot,
            inbox.AllMessages,
            new BudgetSnapshot(1_000_000m, 900_000m, 100_000m, 0m, 0m, 0m, 0m, BudgetStatus.UnderBudget, "Owner budget status: UnderBudget"),
            new SeasonReadinessService().Evaluate(registry, snapshot),
            new StaffOfficeService().BuildVacancies(snapshot, registry.Rulebook ?? RulebookPresets.CreateNhlStyle()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var marker = Path.Combine(directory.FullName, "HockeyGmLegacy.slnx");
            if (File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
