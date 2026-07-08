using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

public sealed class Alpha67TacticsCoachingStyleTests
{
    public void TacticsProfileCreated()
    {
        var tactics = CreateScenario().CurrentTactics!;

        Assert.False(string.IsNullOrWhiteSpace(tactics.TacticsId), "Tactics profile should have an identity.");
        Assert.False(string.IsNullOrWhiteSpace(tactics.Summary), "Tactics profile should have readable summary.");
        Assert.True(tactics.ModifierProfile.RiskTendency is >= -10 and <= 10, "Tactical modifiers should stay small.");
    }

    public void CoachPhilosophySetsDefaultTactics()
    {
        var tactics = CreateScenario().CurrentTactics!;

        Assert.False(string.IsNullOrWhiteSpace(tactics.CoachName), "Tactics should reference the head coach.");
        Assert.True(tactics.ChangeHistory.Any(item => item.Contains("Auto-set", StringComparison.OrdinalIgnoreCase)), "Default tactics should be coach-derived.");
        Assert.True(tactics.Summary.Contains(tactics.Style.ToString(), StringComparison.OrdinalIgnoreCase), tactics.Summary);
    }

    public void EvenStrengthSettingsCanChange()
    {
        var scenario = CreateScenario();
        var service = new TacticsService();

        scenario = service.SetForecheck(scenario, ForecheckSetting.Aggressive).ScenarioSnapshot;
        scenario = service.SetNeutralZone(scenario, NeutralZoneSetting.Pressure).ScenarioSnapshot;
        scenario = service.SetDefensiveZone(scenario, DefensiveZoneSetting.Collapse).ScenarioSnapshot;
        scenario = service.SetBreakout(scenario, BreakoutSetting.FastTransition).ScenarioSnapshot;
        scenario = service.SetShotPreference(scenario, ShotPreference.QualityChances).ScenarioSnapshot;
        scenario = service.SetPhysicality(scenario, TacticalIntensity.High).ScenarioSnapshot;
        scenario = service.SetRisk(scenario, TacticalRiskLevel.High).ScenarioSnapshot;

        var settings = scenario.CurrentTactics!.Settings;
        Assert.Equal(ForecheckSetting.Aggressive, settings.Forecheck);
        Assert.Equal(NeutralZoneSetting.Pressure, settings.NeutralZone);
        Assert.Equal(DefensiveZoneSetting.Collapse, settings.DefensiveZone);
        Assert.Equal(BreakoutSetting.FastTransition, settings.Breakout);
        Assert.Equal(ShotPreference.QualityChances, settings.ShotPreference);
        Assert.Equal(TacticalIntensity.High, settings.Physicality);
        Assert.Equal(TacticalRiskLevel.High, settings.RiskLevel);
    }

    public void PowerPlayAndPenaltyKillStylesCanChange()
    {
        var scenario = CreateScenario();
        var service = new TacticsService();

        scenario = service.SetPowerPlayStyle(scenario, PowerPlayTacticalStyle.NetFront).ScenarioSnapshot;
        scenario = service.SetPenaltyKillStyle(scenario, PenaltyKillTacticalStyle.ShotBlocking).ScenarioSnapshot;

        Assert.Equal(PowerPlayTacticalStyle.NetFront, scenario.CurrentTactics!.Settings.PowerPlayStyle);
        Assert.Equal(PenaltyKillTacticalStyle.ShotBlocking, scenario.CurrentTactics.Settings.PenaltyKillStyle);
    }

    public void TacticalFitReportGenerated()
    {
        var report = CreateScenario().CurrentTactics!.FitReport;

        Assert.True(report.Score is >= 0 and <= 100, "Tactical fit score should be clamped.");
        Assert.True(report.Strengths.Count > 0, "Fit report should include strengths.");
        Assert.True(report.Weaknesses.Count > 0, "Fit report should include weaknesses.");
        Assert.False(string.IsNullOrWhiteSpace(report.CoachRecommendation), "Fit report should include coach recommendation.");
    }

    public void RosterMismatchWarningGenerated()
    {
        var scenario = CreateScenario();
        var youngLineup = scenario.CurrentLineup! with
        {
            Assignments = scenario.CurrentLineup.Assignments.Select(assignment => assignment with { Age = 18 }).ToArray()
        };
        scenario = scenario with { CurrentLineup = youngLineup, CurrentTactics = null };

        var risky = new TacticsService().SetRisk(scenario, TacticalRiskLevel.High).ScenarioSnapshot.CurrentTactics!;

        Assert.True(risky.FitReport.RiskWarnings.Any(warning => warning.Contains("young roster", StringComparison.OrdinalIgnoreCase)), "High-risk tactics should warn on a young roster.");
    }

    public void TacticalRecommendationGenerated()
    {
        var tactics = CreateScenario().CurrentTactics!;

        Assert.True(tactics.Recommendations.Count > 0, "Tactics should produce coach recommendations.");
        Assert.True(tactics.Recommendations.All(item => !string.IsNullOrWhiteSpace(item.SuggestedAction)), "Tactical recommendations should be explainable.");
    }

    public void PlayerRoleDevelopmentNotesCanReferenceTactics()
    {
        var scenario = CreateScenario();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot != LineupSlot.HealthyScratch);

        var impact = new TacticsService().BuildPlayerImpact(scenario, player.PersonId);

        Assert.True(impact.DevelopmentModifier is >= -10 and <= 10, "Tactical development modifier should be modest.");
        Assert.True(impact.Summary.Contains("Tactical", StringComparison.OrdinalIgnoreCase), impact.Summary);
    }

    public void PlayerDossierIncludesTactics()
    {
        var scenario = CreateScenario();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot != LineupSlot.HealthyScratch);

        var dossier = new PlayerDossierService().CreateDossier(scenario, player.PersonId);

        Assert.True(dossier.Sections.Any(section => section.Title == "Tactics"), "Player dossier should include tactics section.");
    }

    public void ActionCenterOnlyShowsMajorTacticIssues()
    {
        var created = CreateNhlScenario();
        var scenario = CreateScenario();
        var stableItems = BuildActionCenterItems(created.Registry, scenario)
            .Where(item => item.ActionCenterItemId.Contains("tactics", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(stableItems.Length <= 1, "Stable tactics should not flood Action Center.");

        var rec = new TacticalRecommendation(
            "tactics-rec:test-risk",
            TacticalRecommendationType.ReduceRisk,
            "High-risk tactics on young roster",
            "High-risk tactics are hurting the development environment.",
            "Lower tactical risk.",
            true);
        scenario = scenario with
        {
            CurrentTactics = scenario.CurrentTactics! with
            {
                FitReport = scenario.CurrentTactics.FitReport with { Grade = TacticalFitGrade.Problem, RiskWarnings = new[] { "High-risk tactics may overload a young roster." } },
                Recommendations = new[] { rec }
            }
        };

        var issueItems = BuildActionCenterItems(created.Registry, scenario)
            .Where(item => item.Title.Contains("High-risk tactics", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(1, issueItems.Length);
    }

    public void AlphaDesktopExposesTacticsView()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("new(\"Tactics\", CreateSelectablePeopleContent(\"Tactics\"))", StringComparison.Ordinal), "Hockey Operations should expose Tactics view.");
        Assert.True(source.Contains("Set Forecheck", StringComparison.Ordinal), "Tactics UI should expose forecheck action.");
        Assert.True(source.Contains("Set PP Style", StringComparison.Ordinal), "Tactics UI should expose power-play style action.");
        Assert.True(source.Contains("Auto Set From Coach", StringComparison.Ordinal), "Tactics UI should expose coach default action.");
    }

    public void SaveLoadPreservesTactics()
    {
        var created = CreateNhlScenario();
        var scenario = new TacticsService().SetStyle(CreateScenario(), TacticalStyle.Speed).ScenarioSnapshot;
        var service = new SaveGameService();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-tactics-{Guid.NewGuid():N}.json");
        try
        {
            var saved = service.SaveCareer(
                scenario,
                Array.Empty<InboxMessage>(),
                Array.Empty<LeagueTransaction>(),
                new Dictionary<string, ActionCenterStatus>(),
                new BudgetOverviewService().Build(scenario, created.Registry.Rulebook ?? RulebookPresets.CreateNhlStyle()),
                path);

            Assert.True(saved.Success, saved.Message);
            var loaded = service.LoadFromFile(path, created.Registry.Rulebook);

            Assert.True(loaded.Success, loaded.Message);
            Assert.Equal(TacticalStyle.Speed, loaded.SaveGame!.ScenarioSnapshot.CurrentTactics!.Style);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public void NoFullTacticsEnginePlayByPlayOrGodotAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join(Environment.NewLine, new[]
        {
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "TacticsModels.cs")),
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "TacticsService.cs"))
        });

        Assert.False(text.Contains("PlayByPlay", StringComparison.OrdinalIgnoreCase), "Alpha 6.7 should not add play-by-play.");
        Assert.False(text.Contains("LineMatching", StringComparison.OrdinalIgnoreCase), "Alpha 6.7 should not add line matching.");
        Assert.False(text.Contains("MatchupEngine", StringComparison.OrdinalIgnoreCase), "Alpha 6.7 should not add matchup engine.");
        Assert.False(text.Contains("FaceoffStrategy", StringComparison.OrdinalIgnoreCase), "Alpha 6.7 should not add faceoff strategy.");
        Assert.False(text.Contains("BasicGameSimulator", StringComparison.OrdinalIgnoreCase), "Alpha 6.7 should not overhaul game simulation.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 6.7 should not add Godot.");
    }

    private static NewGmScenarioSnapshot CreateScenario() =>
        new TacticsService().EnsureTactics(new GameUsageService().EnsureGameUsage(new LineChemistryService().EnsureChemistry(CreateNhlScenario().ScenarioSnapshot)));

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
