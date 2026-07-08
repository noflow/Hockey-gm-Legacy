using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

public sealed class Alpha65LineChemistryTests
{
    public void ForwardLineChemistryGenerated()
    {
        var report = CreateScenario().CurrentLineChemistry!;

        Assert.Equal(4, report.ForwardLines.Count);
        Assert.True(report.ForwardLines.All(line => line.Score.Value is >= 0 and <= 100), "Forward line chemistry scores should be clamped.");
    }

    public void DefensePairChemistryGenerated()
    {
        var report = CreateScenario().CurrentLineChemistry!;

        Assert.Equal(3, report.DefensePairs.Count);
        Assert.True(report.DefensePairs.All(pair => pair.Label.Contains("Pair", StringComparison.Ordinal)), "Defense pair chemistry should label each pair.");
    }

    public void GoalieDepthChemistryGenerated()
    {
        var report = CreateScenario().CurrentLineChemistry!;

        Assert.Equal(LineChemistryUnitType.GoalieDepth, report.GoalieDepth.UnitType);
        Assert.True(report.GoalieDepth.PlayerNames.Count > 0, "Goalie depth chemistry should include goalie names.");
    }

    public void PlaymakerShooterPowerMixImprovesChemistry()
    {
        var scenario = CreateScenario();
        var line = scenario.CurrentLineup!.ForwardLines.First();
        var mixed = line with
        {
            LeftWing = line.LeftWing! with { PlayerType = "Playmaker", ShootsCatches = "Shoots L" },
            Center = line.Center! with { PlayerType = "Scoring Shooter", ShootsCatches = "Shoots R" },
            RightWing = line.RightWing! with { PlayerType = "Power Forward", ShootsCatches = "Shoots L" }
        };
        var duplicate = line with
        {
            LeftWing = line.LeftWing! with { PlayerType = "Scoring Shooter", ShootsCatches = "Shoots L" },
            Center = line.Center! with { PlayerType = "Scoring Shooter", ShootsCatches = "Shoots L" },
            RightWing = line.RightWing! with { PlayerType = "Scoring Shooter", ShootsCatches = "Shoots L" }
        };
        var service = new LineChemistryService();

        Assert.True(
            service.EvaluateForwardLine(scenario, 1, mixed).Score.Value > service.EvaluateForwardLine(scenario, 1, duplicate).Score.Value,
            "Playmaker/shooter/power mix should beat a line of duplicate shooters.");
    }

    public void DuplicatePlayerTypesCanReduceChemistry()
    {
        var scenario = CreateScenario();
        var line = scenario.CurrentLineup!.ForwardLines.First();
        var duplicate = line with
        {
            LeftWing = line.LeftWing! with { PlayerType = "Scoring Shooter" },
            Center = line.Center! with { PlayerType = "Scoring Shooter" },
            RightWing = line.RightWing! with { PlayerType = "Scoring Shooter" }
        };

        var result = new LineChemistryService().EvaluateForwardLine(scenario, 1, duplicate);

        Assert.True(result.Weaknesses.Any(text => text.Contains("similar shooters", StringComparison.OrdinalIgnoreCase)), "Duplicate player types should create an explainable weakness.");
    }

    public void LeftRightDefenseBalanceImprovesChemistry()
    {
        var scenario = CreateScenario();
        var pair = scenario.CurrentLineup!.DefensePairs.First();
        var balanced = pair with
        {
            LeftDefense = pair.LeftDefense! with { ShootsCatches = "Shoots L" },
            RightDefense = pair.RightDefense! with { ShootsCatches = "Shoots R" }
        };
        var sameHanded = pair with
        {
            LeftDefense = pair.LeftDefense! with { ShootsCatches = "Shoots L" },
            RightDefense = pair.RightDefense! with { ShootsCatches = "Shoots L" }
        };
        var service = new LineChemistryService();

        Assert.True(
            service.EvaluateDefensePair(scenario, 1, balanced).Score.Value > service.EvaluateDefensePair(scenario, 1, sameHanded).Score.Value,
            "Left/right defensive balance should improve chemistry.");
    }

    public void VeteranProspectPairingGivesDevelopmentNote()
    {
        var scenario = CreateScenario();
        var pair = scenario.CurrentLineup!.DefensePairs.First();
        var mentorPair = pair with
        {
            LeftDefense = pair.LeftDefense! with { Age = 18 },
            RightDefense = pair.RightDefense! with { Age = 30 }
        };

        var result = new LineChemistryService().EvaluateDefensePair(scenario, 1, mentorPair);

        Assert.True(result.DevelopmentNote.Contains("veteran", StringComparison.OrdinalIgnoreCase), "Veteran/prospect pairing should produce a development note.");
    }

    public void PoorRelationshipReducesChemistry()
    {
        var scenario = CreateScenario();
        var line = scenario.CurrentLineup!.ForwardLines.First();
        var neutral = new LineChemistryService().EvaluateForwardLine(scenario, 1, line);
        var badRelationship = new ExpandedRelationshipProfile(
            "relationship:bad-line-fit",
            ExpandedRelationshipType.PlayerPlayer,
            line.LeftWing!.PersonId,
            line.LeftWing.PlayerName,
            line.Center!.PersonId,
            line.Center.PlayerName,
            15,
            20,
            15,
            90,
            20,
            ExpandedRelationshipTrend.Strained,
            new[] { "Practice tension affected communication." },
            new[] { "Chemistry problem identified." },
            "Poor player-player fit.");
        var strained = scenario with { RelationshipProfiles = scenario.RelationshipProfiles.Append(badRelationship).ToArray() };

        var strainedScore = new LineChemistryService().EvaluateForwardLine(strained, 1, line).Score.Value;

        Assert.True(strainedScore < neutral.Score.Value, "Poor relationship should reduce line chemistry.");
    }

    public void CoachRecommendationGenerated()
    {
        var report = CreateScenario().CurrentLineChemistry!;

        Assert.True(report.CoachRecommendations.Count > 0, "Line chemistry report should include coach recommendations.");
    }

    public void TeamChemistrySummaryGenerated()
    {
        var report = CreateScenario().CurrentLineChemistry!;

        Assert.Equal(LineChemistryUnitType.Team, report.Overall.UnitType);
        Assert.False(string.IsNullOrWhiteSpace(report.BestLine), "Team chemistry should identify best unit.");
        Assert.False(string.IsNullOrWhiteSpace(report.WorstLine), "Team chemistry should identify weakest unit.");
    }

    public void LineupUiExposesChemistry()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("LineChemistry", StringComparison.Ordinal), "Lineup UI should include selectable line chemistry rows.");
        Assert.True(source.Contains("Team Chemistry", StringComparison.Ordinal), "Lineup UI should include a team chemistry card/summary.");
        Assert.True(source.Contains("ChemistryTextForSlot", StringComparison.Ordinal), "Lineup slots should expose chemistry grades.");
    }

    public void PlayerDossierExposesChemistryNote()
    {
        var scenario = CreateScenario();
        var playerId = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot != LineupSlot.HealthyScratch).PersonId;

        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);
        var roleUsage = dossier.Sections.First(section => section.Title == "Role / Usage");
        var rendered = string.Join(Environment.NewLine, roleUsage.Lines);

        Assert.True(rendered.Contains("Current line chemistry", StringComparison.Ordinal), "Dossier should show current line chemistry.");
        Assert.True(rendered.Contains("Chemistry notes", StringComparison.Ordinal), "Dossier should show chemistry notes.");
    }

    public void ActionCenterOnlyCreatesMajorChemistryIssues()
    {
        var created = CreateNhlScenario();
        var scenario = created.ScenarioSnapshot;
        var report = new LineChemistryService().BuildReport(scenario);
        var firstLine = report.ForwardLines.First();
        var problemLine = firstLine with
        {
            Score = new LineChemistryScore(25, LineChemistryGrade.Problem, "0-29"),
            Weaknesses = new[] { "Deliberate test chemistry problem." }
        };
        var problemReport = report with
        {
            ForwardLines = report.ForwardLines.Select(line => line.UnitId == firstLine.UnitId ? problemLine : line).ToArray()
        };
        scenario = scenario with { CurrentLineChemistry = problemReport };

        var items = BuildActionCenterItems(created.Registry, scenario)
            .Where(item => item.Title.Contains("Line chemistry problem", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(1, items.Length);
        Assert.True(items[0].Reason.Contains("chemistry problem", StringComparison.OrdinalIgnoreCase), "Chemistry action should describe the major issue.");
    }

    public void NoHiddenRatingsExposed()
    {
        var report = CreateScenario().CurrentLineChemistry!;
        var rendered = string.Join(Environment.NewLine, report.Units.SelectMany(unit =>
            new[] { unit.Label, unit.Recommendation, unit.DevelopmentNote, unit.RelationshipNote, unit.RolePromiseNote }
                .Concat(unit.Strengths)
                .Concat(unit.Weaknesses)
                .Concat(unit.Factors.Select(factor => factor.Summary))));

        Assert.False(rendered.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase), "Chemistry output should not expose hidden current ability.");
        Assert.False(rendered.Contains("Potential =", StringComparison.OrdinalIgnoreCase), "Chemistry output should not expose hidden potential ratings.");
        Assert.False(rendered.Contains("hidden rating", StringComparison.OrdinalIgnoreCase), "Chemistry output should not expose hidden ratings.");
    }

    public void NoTacticsEngineAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join(Environment.NewLine, new[]
        {
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "LineChemistryModels.cs")),
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "LineChemistryService.cs"))
        });

        Assert.False(text.Contains("SpecialTeams", StringComparison.OrdinalIgnoreCase), "Alpha 6.5 should not add special teams.");
        Assert.False(text.Contains("PowerPlay", StringComparison.OrdinalIgnoreCase), "Alpha 6.5 should not add power play logic.");
        Assert.False(text.Contains("PenaltyKill", StringComparison.OrdinalIgnoreCase), "Alpha 6.5 should not add penalty kill logic.");
        Assert.False(text.Contains("MatchupEngine", StringComparison.OrdinalIgnoreCase), "Alpha 6.5 should not add matchup engine logic.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 6.5 should not add Godot.");
    }

    private static NewGmScenarioSnapshot CreateScenario() =>
        new LineChemistryService().EnsureChemistry(CreateNhlScenario().ScenarioSnapshot);

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
