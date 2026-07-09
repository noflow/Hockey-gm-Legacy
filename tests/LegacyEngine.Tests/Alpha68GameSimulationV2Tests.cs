using LegacyEngine.Injuries;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;

internal sealed class Alpha68GameSimulationV2Tests
{
    public void GameSimulationContextCreated()
    {
        var scenario = CreateScenario();
        var game = PlayerGame(scenario, "alpha68-context");

        var context = new GameSimulationService().CreateContext(scenario, game);

        Assert.Equal(game.HomeOrganizationId, context.HomeTeam.OrganizationId);
        Assert.Equal(game.AwayOrganizationId, context.AwayTeam.OrganizationId);
        Assert.True(context.HomeTeam.OffenseScore > 0, "Home team profile should include offense score.");
        Assert.True(context.HomeTeam.Notes.Count > 0, "Team profile should include explainable notes.");
    }

    public void LineupAffectsSimulationProfile()
    {
        var scenario = CreateScenario();
        var game = PlayerGame(scenario, "alpha68-lineup");
        var context = new GameSimulationService().CreateContext(scenario, game);

        Assert.True(context.HomeTeam.Lines.Any(line => line.UnitName.Contains("Forward line 1", StringComparison.Ordinal)), "Line profile should include forward line one.");
        Assert.True(context.HomeTeam.Lines.First(line => line.UnitName.Contains("Forward line 1", StringComparison.Ordinal)).UsageWeight
            > context.HomeTeam.Lines.First(line => line.UnitName.Contains("Forward line 4", StringComparison.Ordinal)).UsageWeight,
            "Top line should carry more usage weight than fourth line.");
    }

    public void ChemistryAffectsSimulationProfile()
    {
        var scenario = CreateScenario();
        var game = PlayerGame(scenario, "alpha68-chemistry");
        var context = new GameSimulationService().CreateContext(scenario, game);

        Assert.Equal(scenario.CurrentLineChemistry!.Overall.Score.Value, context.HomeTeam.ChemistryScore);
        Assert.Equal(BandFromScore(scenario.CurrentLineChemistry.Overall.Score.Value), context.HomeTeam.Chemistry);
    }

    public void SpecialTeamsAffectResult()
    {
        var result = SimulateUntil("alpha68-special", simulation => simulation.HomePowerPlayGoals > 0 || simulation.AwayPowerPlayGoals > 0);

        Assert.True(result.HomePowerPlayChances > 0 && result.AwayPowerPlayChances > 0, "Power-play chances should be part of the result.");
        Assert.True(result.SpecialTeamsNote.Contains("PP", StringComparison.Ordinal), result.SpecialTeamsNote);
    }

    public void TacticsAffectResultTendencies()
    {
        var scenario = CreateScenario();
        var game = PlayerGame(scenario, "alpha68-tactics");
        var result = new GameSimulationService().Simulate(scenario, game);

        Assert.False(string.IsNullOrWhiteSpace(result.Context.HomeTeam.TacticalProfile.Style), "Tactical style should be included.");
        Assert.True(result.TacticalNote.Contains(result.Context.HomeTeam.TacticalProfile.Style, StringComparison.Ordinal), result.TacticalNote);
    }

    public void InjuredPlayersExcluded()
    {
        var scenario = CreateScenario();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot is LineupSlot.Line1LW or LineupSlot.Line1C or LineupSlot.Line1RW);
        var injury = new InjuryEngine().CreateInjury(
            player.PersonId,
            scenario.CurrentDate,
            InjuryBodyPart.Knee,
            InjuryType.Strain,
            InjurySeverity.Moderate).Injury;
        scenario = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                Injuries = scenario.AlphaSnapshot.Injuries.Append(injury).ToArray()
            }
        };

        var context = new GameSimulationService().CreateContext(scenario, PlayerGame(scenario, "alpha68-injury"));

        Assert.False(context.HomeTeam.AvailablePlayerIds.Contains(player.PersonId, StringComparer.Ordinal), "Injured player should not be available.");
        Assert.True(context.HomeTeam.UnavailablePlayerIds.Contains(player.PersonId, StringComparer.Ordinal), "Injured player should be tracked as unavailable.");
    }

    public void GoalieUsageAffectsRecap()
    {
        var scenario = CreateScenario();
        var game = PlayerGame(scenario, "alpha68-goalie");
        var simulation = new GameSimulationService().Simulate(scenario, game);
        var completed = game.Complete(simulation.Result);
        var recap = new GameRecapService().CreateRecap(scenario, completed, simulation);

        Assert.True(recap.GoalieUsageNote.Contains(simulation.PlayerTeamGoalieStats!.PlayerName, StringComparison.Ordinal), recap.GoalieUsageNote);
    }

    public void TopLinePlayersReceiveMoreScoringOpportunityThanDepthPlayers()
    {
        var scenario = CreateScenario();
        var result = new GameSimulationService().Simulate(scenario, PlayerGame(scenario, "alpha68-opportunity"));
        var topLineIds = scenario.CurrentLineup!.ForwardLines.First(line => line.LineNumber == 1).Players().Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal);
        var depthIds = scenario.CurrentLineup.ForwardLines.First(line => line.LineNumber == 4).Players().Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal);
        var topOpportunity = result.PlayerTeamSkaterStats.Where(stat => topLineIds.Contains(stat.PersonId)).Select(stat => stat.OpportunityWeight).DefaultIfEmpty(0).Max();
        var depthOpportunity = result.PlayerTeamSkaterStats.Where(stat => depthIds.Contains(stat.PersonId)).Select(stat => stat.OpportunityWeight).DefaultIfEmpty(0).Max();

        Assert.True(topOpportunity > depthOpportunity, $"Top opportunity {topOpportunity} should exceed depth opportunity {depthOpportunity}.");
    }

    public void PowerPlayPlayersCanReceivePowerPlayPoints()
    {
        var result = SimulateUntil("alpha68-pp-point", simulation => simulation.PlayerTeamSkaterStats.Any(stat => stat.IncludedPowerPlayPoint));

        Assert.True(result.PlayerTeamSkaterStats.Any(stat => stat.IncludedPowerPlayPoint), "Power-play usage should be reflected in player stat allocation.");
    }

    public void GameRecapIncludesTacticalChemistryAndSpecialTeamsNotes()
    {
        var scenario = CreateScenario();
        var game = PlayerGame(scenario, "alpha68-recap");
        var simulation = new GameSimulationService().Simulate(scenario, game);
        var recap = new GameRecapService().CreateRecap(scenario, game.Complete(simulation.Result), simulation);

        Assert.False(string.IsNullOrWhiteSpace(recap.TacticalNote), "Recap should include tactical note.");
        Assert.False(string.IsNullOrWhiteSpace(recap.ChemistryNote), "Recap should include chemistry note.");
        Assert.False(string.IsNullOrWhiteSpace(recap.SpecialTeamsNote), "Recap should include special teams note.");
    }

    public void PlayerMilestoneTriggeredFromGame()
    {
        var scenario = CreateScenario() with
        {
            PlayerStats = new SeasonStatsService().CreatePlayerStats(CreateScenario().AlphaSnapshot),
            GoalieStats = new SeasonStatsService().CreateGoalieStats(CreateScenario().AlphaSnapshot),
            PlayerMilestones = Array.Empty<PlayerMilestone>()
        };
        var service = new GameSimulationService();
        GameSimulationResultV2? result = null;
        for (var index = 0; index < 40; index++)
        {
            result = service.Simulate(scenario, PlayerGame(scenario, $"alpha68-milestone-{index}"));
            if (result.NewMilestones.Count > 0)
            {
                break;
            }
        }

        Assert.True(result is not null && result.NewMilestones.Count > 0, "Game simulation should create a milestone when first tracked point/goal criteria are met.");
    }

    public void StandingsAndStatsStillUpdate()
    {
        var scenario = CreateScenario();
        var game = PlayerGame(scenario, "alpha68-season-update", scenario.CurrentDate.AddDays(1));
        var teams = SeasonFrameworkService.LeagueTeams(scenario);
        var stats = new SeasonStatsService();
        scenario = scenario with
        {
            SeasonReadiness = new SeasonReadinessState(ReviewsGenerated: true, SeasonBegun: true),
            Schedule = new GameSchedule("alpha68-schedule", scenario.Season.LeagueId, new[] { game }),
            Standings = stats.CreateStandings(scenario.Season.LeagueId, teams),
            TeamStats = stats.CreateTeamStats(teams),
            PlayerStats = stats.CreatePlayerStats(scenario.AlphaSnapshot),
            GoalieStats = stats.CreateGoalieStats(scenario.AlphaSnapshot)
        };

        var result = new DailySimulationCoordinator().AdvanceScenarioOneDay(NewGmScenarioBootstrapper.CreateScenario().Registry, scenario);

        Assert.True(result.ScenarioSnapshot.Standings!.Teams.Sum(team => team.GamesPlayed) == 2, "Standings should still update.");
        Assert.True(result.ScenarioSnapshot.PlayerStats.Any(stat => stat.GamesPlayed > 0), "Skater stats should still update.");
        Assert.True(result.ScenarioSnapshot.GoalieStats.Any(stat => stat.GamesPlayed > 0), "Goalie stats should still update.");
    }

    public void AlphaDesktopExposesEnhancedRecap()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Special teams:", StringComparison.Ordinal), "Schedule recap should show special teams note.");
        Assert.True(source.Contains("Tactics:", StringComparison.Ordinal), "Schedule recap should show tactical note.");
        Assert.True(source.Contains("Chemistry:", StringComparison.Ordinal), "Schedule recap should show chemistry note.");
        Assert.True(source.Contains("Last game concern", StringComparison.Ordinal), "Dashboard should show last game concern.");
    }

    public void NoForbiddenGameSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var text = string.Join(Environment.NewLine, new[]
        {
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "GameSimulationModels.cs")),
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "GameSimulationService.cs")),
            File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "SeasonFrameworkService.cs"))
        });

        Assert.False(text.Contains("ShotByShot", StringComparison.OrdinalIgnoreCase), "Alpha 6.8 should not add shot-by-shot simulation.");
        Assert.False(text.Contains("ShiftSimulation", StringComparison.OrdinalIgnoreCase), "Alpha 6.8 should not add shift simulation.");
        Assert.False(text.Contains("LineMatching", StringComparison.OrdinalIgnoreCase), "Alpha 6.8 should not add line matching.");
        Assert.False(text.Contains("PlayByPlay", StringComparison.OrdinalIgnoreCase), "Alpha 6.8 should not add play-by-play.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 6.8 should not add Godot.");
    }

    private static GameSimulationResultV2 SimulateUntil(string idPrefix, Func<GameSimulationResultV2, bool> predicate)
    {
        var scenario = CreateScenario();
        var service = new GameSimulationService();
        for (var index = 0; index < 40; index++)
        {
            var result = service.Simulate(scenario, PlayerGame(scenario, $"{idPrefix}-{index}"));
            if (predicate(result))
            {
                return result;
            }
        }

        return service.Simulate(scenario, PlayerGame(scenario, $"{idPrefix}-fallback"));
    }

    private static NewGmScenarioSnapshot CreateScenario()
    {
        var created = new MultiLeagueCareerService().CreateScenario(new MultiLeagueCareerService().SelectLeagueAndTeam(LeagueExperience.Nhl, "org-seattle-cascades"));
        return new TacticsService().EnsureTactics(new GameUsageService().EnsureGameUsage(new LineChemistryService().EnsureChemistry(created.ScenarioSnapshot)));
    }

    private static ScheduledGame PlayerGame(NewGmScenarioSnapshot scenario, string gameId) =>
        PlayerGame(scenario, gameId, scenario.CurrentDate);

    private static ScheduledGame PlayerGame(NewGmScenarioSnapshot scenario, string gameId, DateOnly date)
    {
        var opponent = SeasonFrameworkService.LeagueTeams(scenario).First(team => team.OrganizationId != scenario.Organization.OrganizationId);
        return new ScheduledGame(gameId, date, scenario.Organization.OrganizationId, opponent.OrganizationId);
    }

    private static TeamStrengthBand BandFromScore(int score) =>
        score switch
        {
            < 40 => TeamStrengthBand.Weak,
            < 50 => TeamStrengthBand.BelowAverage,
            < 66 => TeamStrengthBand.Average,
            < 82 => TeamStrengthBand.Strong,
            _ => TeamStrengthBand.Elite
        };

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

file static class Alpha68LineupTestExtensions
{
    public static IReadOnlyList<LineupRoleAssignment> Players(this ForwardLine line) =>
        new[] { line.LeftWing, line.Center, line.RightWing }
            .Where(player => player is not null)
            .Select(player => player!)
            .ToArray();
}
