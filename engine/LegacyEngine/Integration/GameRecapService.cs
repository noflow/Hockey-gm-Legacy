using LegacyEngine.Events;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class GameRecapService
{
    public GameRecap CreateRecap(NewGmScenarioSnapshot scenario, ScheduledGame completedGame) =>
        CreateRecap(scenario, completedGame, null);

    public GameRecap CreateRecap(NewGmScenarioSnapshot scenario, ScheduledGame completedGame, GameSimulationResultV2? simulation)
    {
        if (completedGame.Status != GameStatus.Completed || completedGame.Result is null)
        {
            throw new ArgumentException("A game recap requires a completed game with a result.", nameof(completedGame));
        }

        var result = completedGame.Result;
        var home = TeamSummary(scenario, completedGame.HomeOrganizationId, completedGame.HomeOrganizationId == result.WinnerOrganizationId, result.HomeGoals, completedGame.GameId, simulation);
        var away = TeamSummary(scenario, completedGame.AwayOrganizationId, completedGame.AwayOrganizationId == result.WinnerOrganizationId, result.AwayGoals, completedGame.GameId, simulation);
        var notablePlayers = NotablePlayers(scenario, completedGame, simulation).ToArray();
        var goalieSummaries = GoalieSummaries(scenario, completedGame, simulation).ToArray();
        var threeStars = ThreeStars(scenario, completedGame, notablePlayers, goalieSummaries).ToArray();
        var winnerTeam = TeamName(scenario, result.WinnerOrganizationId);
        var loserTeam = TeamName(scenario, result.LoserOrganizationId);
        var topSkater = notablePlayers.FirstOrDefault()?.PlayerName ?? "the top line";
        var topGoalie = goalieSummaries.FirstOrDefault(goalie => goalie.Won)?.PlayerName ?? goalieSummaries.FirstOrDefault()?.PlayerName ?? "the starter";
        var injuryNotes = InjuryNotes(scenario, completedGame).ToArray();
        var developmentNotes = DevelopmentNotes(scenario, completedGame, notablePlayers).ToArray();

        var recap = new GameRecap(
            GameId: completedGame.GameId,
            Date: completedGame.Date,
            BoxScore: new GameBoxScore(
                Home: home,
                Away: away,
                FinalScore: $"{home.TeamName} {home.Goals}, {away.TeamName} {away.Goals}",
                ShotsSummary: $"{home.TeamName} {home.Shots}, {away.TeamName} {away.Shots}",
                PowerPlaySummary: $"{home.TeamName} {home.PowerPlay}; {away.TeamName} {away.PowerPlay}"),
            WinnerOrganizationId: result.WinnerOrganizationId,
            WinnerTeam: winnerTeam,
            PeriodScoringSummary: PeriodScoringSummary(home, away),
            ThreeStars: threeStars,
            NotablePlayers: notablePlayers,
            GoalieSummaries: goalieSummaries,
            KeyMoments: KeyMoments(winnerTeam, loserTeam, topSkater),
            InjuryNotes: injuryNotes,
            DevelopmentNotes: developmentNotes,
            NarrativeSummary: simulation?.NarrativeSummary ?? $"{winnerTeam} defeated {loserTeam} {WinningGoals(result)}-{LosingGoals(result)} behind a strong performance from {topSkater} and {goalieSummaries.FirstOrDefault(goalie => goalie.Won)?.Saves ?? 0} saves from {topGoalie}.")
        {
            TopLineSummary = simulation?.TopLineSummary ?? "Top-line impact was not tracked for this game.",
            SpecialTeamsNote = simulation?.SpecialTeamsNote ?? $"Power play: {home.PowerPlay}; {away.PowerPlay}.",
            TacticalNote = simulation?.TacticalNote ?? "Tactical note not tracked for this game.",
            ChemistryNote = simulation?.ChemistryNote ?? "Chemistry note not tracked for this game.",
            GoalieUsageNote = simulation?.GoalieUsageNote ?? "Goalie usage note not tracked for this game.",
            KeyConcern = simulation?.KeyConcern ?? "No key concern recorded."
        };
        recap.Validate();
        return recap;
    }

    public AlphaInboxItem CreatePlayerTeamInbox(NewGmScenarioSnapshot scenario, GameRecap recap)
    {
        var playerTeamId = scenario.Organization.OrganizationId;
        var playerTeam = recap.BoxScore.Home.OrganizationId == playerTeamId ? recap.BoxScore.Home : recap.BoxScore.Away;
        var opponent = recap.BoxScore.Home.OrganizationId == playerTeamId ? recap.BoxScore.Away : recap.BoxScore.Home;
        var resultText = playerTeam.IsWinner ? "Win" : "Loss";
        var record = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == playerTeamId);
        var topPerformer = recap.NotablePlayers.FirstOrDefault()?.PlayerName
            ?? recap.GoalieSummaries.FirstOrDefault(goalie => goalie.TeamName == playerTeam.TeamName)?.PlayerName
            ?? "No standout logged";
        var concern = recap.InjuryNotes.FirstOrDefault()
            ?? (recap.KeyConcern == "No key concern recorded." ? (playerTeam.GoalDifferentialAgainst(opponent) < -2 ? "Concern: staff flagged the defensive gap." : "Concern: none from the game report.") : $"Concern: {recap.KeyConcern}");
        var nextGame = scenario.Schedule?.NextGameFor(playerTeamId, recap.Date.AddDays(1));
        var nextGameText = nextGame is null
            ? "Next game: none scheduled."
            : $"Next game: {nextGame.Date:yyyy-MM-dd} vs {OpponentName(scenario, nextGame, playerTeamId)}.";
        var recordText = record is null
            ? "Record after game: not available."
            : $"Record after game: {record.Wins}-{record.Losses}-{record.OvertimeLosses}, {record.Points} pts.";

        return new AlphaInboxItem(
            InboxItemId: $"inbox:game-recap:{recap.GameId}",
            Date: new DateTimeOffset(recap.Date.Year, recap.Date.Month, recap.Date.Day, 22, 0, 0, TimeSpan.Zero),
            EventType: LegacyEventType.GamePlayed,
            Severity: recap.InjuryNotes.Count > 0 ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            Title: $"{resultText}: {playerTeam.TeamName} {playerTeam.Goals}, {opponent.TeamName} {opponent.Goals}",
            Summary: $"Opponent: {opponent.TeamName}. Score: {playerTeam.Goals}-{opponent.Goals}. Result: {resultText}. {recordText} Top performer: {topPerformer}. {concern} {nextGameText} {recap.NarrativeSummary} {recap.SpecialTeamsNote} {recap.GoalieUsageNote}",
            PrimaryPersonId: recap.NotablePlayers.FirstOrDefault()?.PersonId);
    }

    public IReadOnlyList<ScheduledGame> RecentResults(NewGmScenarioSnapshot scenario, int count = 8) =>
        scenario.Schedule?.Games
            .Where(game => game.Status == GameStatus.Completed)
            .OrderByDescending(game => game.Date)
            .ThenByDescending(game => game.GameId, StringComparer.Ordinal)
            .Take(count)
            .ToArray()
        ?? Array.Empty<ScheduledGame>();

    public IReadOnlyList<ScheduledGame> UpcomingGames(NewGmScenarioSnapshot scenario, int count = 8) =>
        scenario.Schedule?.Games
            .Where(game => game.Status == GameStatus.Scheduled && game.Date >= scenario.CurrentDate)
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameId, StringComparer.Ordinal)
            .Take(count)
            .ToArray()
        ?? Array.Empty<ScheduledGame>();

    public IReadOnlyList<ScheduledGame> TodaysGames(NewGmScenarioSnapshot scenario) =>
        scenario.Schedule?.Games
            .Where(game => game.Date == scenario.CurrentDate)
            .OrderBy(game => game.GameId, StringComparer.Ordinal)
            .ToArray()
        ?? Array.Empty<ScheduledGame>();

    private static GameTeamSummary TeamSummary(NewGmScenarioSnapshot scenario, string organizationId, bool won, int goals, string gameId, GameSimulationResultV2? simulation)
    {
        if (simulation is not null)
        {
            var isHome = organizationId == simulation.Context.Game.HomeOrganizationId;
            var simulationShots = isHome ? simulation.HomeShots : simulation.AwayShots;
            var simulationPowerPlayGoals = isHome ? simulation.HomePowerPlayGoals : simulation.AwayPowerPlayGoals;
            var simulationPowerPlayChances = isHome ? simulation.HomePowerPlayChances : simulation.AwayPowerPlayChances;
            return new GameTeamSummary(
                organizationId,
                TeamName(scenario, organizationId),
                goals,
                simulationShots,
                $"{simulationPowerPlayGoals}/{simulationPowerPlayChances}",
                won);
        }

        var seed = Math.Abs(HashCode.Combine(gameId, organizationId));
        var shots = 24 + seed % 16 + goals;
        var powerPlayChances = 2 + seed % 4;
        var powerPlayGoals = Math.Min(goals, seed % Math.Max(1, powerPlayChances));
        return new GameTeamSummary(
            organizationId,
            TeamName(scenario, organizationId),
            goals,
            shots,
            $"{powerPlayGoals}/{powerPlayChances}",
            won);
    }

    private static IEnumerable<GamePlayerSummary> NotablePlayers(NewGmScenarioSnapshot scenario, ScheduledGame game, GameSimulationResultV2? simulation)
    {
        if (!InvolvesPlayerTeam(scenario, game))
        {
            return Array.Empty<GamePlayerSummary>();
        }

        if (simulation is not null)
        {
            return simulation.PlayerTeamSkaterStats
                .Where(stat => stat.Points > 0)
                .OrderByDescending(stat => stat.Points)
                .ThenByDescending(stat => stat.Goals)
                .ThenByDescending(stat => stat.OpportunityWeight)
                .Take(6)
                .Select(stat => new GamePlayerSummary(
                    stat.PersonId,
                    stat.PlayerName,
                    scenario.Organization.Name,
                    stat.Goals,
                    stat.Assists,
                    stat.Points,
                    stat.PlusMinus,
                    stat.IncludedPowerPlayPoint ? $"{stat.UsageNote} Added special-teams offense." : stat.UsageNote))
                .ToArray();
        }

        var playerTeamGoals = game.HomeOrganizationId == scenario.Organization.OrganizationId
            ? game.Result!.HomeGoals
            : game.Result!.AwayGoals;
        return scenario.AlphaSnapshot.Roster.ActivePlayers
            .Where(player => player.Position != RosterPosition.Goalie)
            .Select((player, index) =>
            {
                var goals = index < playerTeamGoals ? 1 : 0;
                var assists = index >= playerTeamGoals && index < playerTeamGoals * 3 ? 1 : 0;
                var points = goals + assists;
                return new GamePlayerSummary(
                    player.PersonId,
                    PlayerName(scenario, player.PersonId),
                    scenario.Organization.Name,
                    goals,
                    assists,
                    points,
                    goals > 0 ? 1 : 0,
                    points > 0 ? "Contributed directly to the offense." : "Played a supporting role.");
            })
            .Where(player => player.Points > 0)
            .OrderByDescending(player => player.Points)
            .ThenByDescending(player => player.Goals)
            .ThenBy(player => player.PlayerName, StringComparer.Ordinal)
            .Take(5)
            .ToArray();
    }

    private static IEnumerable<GameGoalieSummary> GoalieSummaries(NewGmScenarioSnapshot scenario, ScheduledGame game, GameSimulationResultV2? simulation)
    {
        if (!InvolvesPlayerTeam(scenario, game))
        {
            return Array.Empty<GameGoalieSummary>();
        }

        if (simulation?.PlayerTeamGoalieStats is { } goalieStat)
        {
            var simulationSavePercentage = goalieStat.Saves + goalieStat.GoalsAgainst == 0 ? 0m : Math.Round((decimal)goalieStat.Saves / (goalieStat.Saves + goalieStat.GoalsAgainst), 3);
            return new[]
            {
                new GameGoalieSummary(
                    goalieStat.PersonId,
                    goalieStat.PlayerName,
                    scenario.Organization.Name,
                    goalieStat.Saves,
                    goalieStat.GoalsAgainst,
                    simulationSavePercentage,
                    goalieStat.Won,
                    goalieStat.UsageNote)
            };
        }

        var goalie = scenario.AlphaSnapshot.Roster.ActivePlayers.FirstOrDefault(player => player.Position == RosterPosition.Goalie);
        if (goalie is null)
        {
            return Array.Empty<GameGoalieSummary>();
        }

        var goalsAgainst = game.HomeOrganizationId == scenario.Organization.OrganizationId ? game.Result!.AwayGoals : game.Result!.HomeGoals;
        var saves = 24 + Math.Abs(HashCode.Combine(game.GameId, scenario.Organization.OrganizationId)) % 13;
        var savePercentage = saves + goalsAgainst == 0 ? 0m : Math.Round((decimal)saves / (saves + goalsAgainst), 3);
        return new[]
        {
            new GameGoalieSummary(
                goalie.PersonId,
                PlayerName(scenario, goalie.PersonId),
                scenario.Organization.Name,
                saves,
                goalsAgainst,
                savePercentage,
                game.Result!.WinnerOrganizationId == scenario.Organization.OrganizationId,
                goalsAgainst <= 2 ? "Gave the club a stable chance to win." : "Faced pressure and will need support.")
        };
    }

    private static IEnumerable<string> ThreeStars(
        NewGmScenarioSnapshot scenario,
        ScheduledGame game,
        IReadOnlyList<GamePlayerSummary> players,
        IReadOnlyList<GameGoalieSummary> goalies)
    {
        var stars = new List<string>();
        stars.AddRange(players.Take(2).Select(player => $"{player.PlayerName} ({player.Goals}G, {player.Assists}A)"));
        var winningGoalie = goalies.FirstOrDefault(goalie => goalie.Won);
        if (winningGoalie is not null)
        {
            stars.Add($"{winningGoalie.PlayerName} ({winningGoalie.Saves} saves)");
        }

        while (stars.Count < 3)
        {
            stars.Add($"{TeamName(scenario, game.Result!.WinnerOrganizationId)} team effort");
        }

        return stars.Take(3);
    }

    private static IReadOnlyList<string> PeriodScoringSummary(GameTeamSummary home, GameTeamSummary away)
    {
        var homeFirst = Math.Min(home.Goals, Math.Max(0, home.Goals / 2));
        var awayFirst = Math.Min(away.Goals, Math.Max(0, away.Goals / 2));
        return new[]
        {
            $"1st: {home.TeamName} {homeFirst}, {away.TeamName} {awayFirst}",
            $"2nd: {home.TeamName} {Math.Max(0, home.Goals - homeFirst - 1)}, {away.TeamName} {Math.Max(0, away.Goals - awayFirst - 1)}",
            $"3rd: {home.TeamName} {Math.Min(1, home.Goals)}, {away.TeamName} {Math.Min(1, away.Goals)}"
        };
    }

    private static IReadOnlyList<GameKeyMoment> KeyMoments(string winnerTeam, string loserTeam, string topSkater) =>
        new[]
        {
            new GameKeyMoment("1st", "08:42", $"{topSkater} helped establish the pace."),
            new GameKeyMoment("3rd", "14:18", $"{winnerTeam} protected the lead against {loserTeam}.")
        };

    private static IEnumerable<string> InjuryNotes(NewGmScenarioSnapshot scenario, ScheduledGame game)
    {
        if (!InvolvesPlayerTeam(scenario, game))
        {
            return Array.Empty<string>();
        }

        var seed = Math.Abs(HashCode.Combine(game.GameId, "injury"));
        if (seed % 9 != 0)
        {
            return Array.Empty<string>();
        }

        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.ElementAtOrDefault(seed % scenario.AlphaSnapshot.Roster.ActivePlayers.Count);
        return player is null
            ? Array.Empty<string>()
            : new[] { $"{PlayerName(scenario, player.PersonId)} was flagged for a minor post-game medical check. No automatic roster move was made." };
    }

    private static IEnumerable<string> DevelopmentNotes(NewGmScenarioSnapshot scenario, ScheduledGame game, IReadOnlyList<GamePlayerSummary> players)
    {
        if (!InvolvesPlayerTeam(scenario, game) || players.Count == 0)
        {
            return Array.Empty<string>();
        }

        var seed = Math.Abs(HashCode.Combine(game.GameId, "development"));
        if (seed % 4 != 0)
        {
            return Array.Empty<string>();
        }

        var player = players[0];
        return new[] { $"{player.PlayerName}'s game gave staff useful evidence on confidence and puck involvement." };
    }

    private static string TeamName(NewGmScenarioSnapshot scenario, string organizationId)
    {
        if (organizationId == scenario.Organization.OrganizationId)
        {
            return scenario.Organization.Name;
        }

        var standing = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == organizationId);
        if (standing is not null)
        {
            return standing.TeamName;
        }

        var team = SeasonFrameworkService.LeagueTeams(scenario).FirstOrDefault(team => team.OrganizationId == organizationId);
        return string.IsNullOrWhiteSpace(team.TeamName) ? organizationId : team.TeamName;
    }

    private static string OpponentName(NewGmScenarioSnapshot scenario, ScheduledGame game, string organizationId) =>
        TeamName(scenario, game.HomeOrganizationId == organizationId ? game.AwayOrganizationId : game.HomeOrganizationId);

    private static string PlayerName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;

    private static bool InvolvesPlayerTeam(NewGmScenarioSnapshot scenario, ScheduledGame game) =>
        game.HomeOrganizationId == scenario.Organization.OrganizationId || game.AwayOrganizationId == scenario.Organization.OrganizationId;

    private static int WinningGoals(GameResult result) =>
        Math.Max(result.HomeGoals, result.AwayGoals);

    private static int LosingGoals(GameResult result) =>
        Math.Min(result.HomeGoals, result.AwayGoals);
}
