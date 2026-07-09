using LegacyEngine.Events;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class PlayoffService
{
    public PlayoffFormat FormatFromRulebook(Rulebook? rulebook, LeagueProfile? leagueProfile = null)
    {
        var rules = rulebook?.PlayoffRules;
        if (rules is null || rules.TeamsQualify <= 0)
        {
            return new PlayoffFormat(PlayoffFormatType.Top8, 8, DefaultSeriesLength(leagueProfile), true, "Safe default");
        }

        var type = rules.TeamsQualify switch
        {
            <= 0 => PlayoffFormatType.Disabled,
            <= 8 => PlayoffFormatType.Top8,
            <= 16 => PlayoffFormatType.Top16,
            _ => PlayoffFormatType.ConferenceTop8Placeholder
        };
        var bestOf = rules.SeriesFormat.FirstOrDefault(value => value > 0);
        if (bestOf <= 0)
        {
            bestOf = DefaultSeriesLength(leagueProfile);
        }

        var format = new PlayoffFormat(type, rules.TeamsQualify, bestOf, rules.ReseedEachRound, $"Rulebook {rulebook!.RulebookId}");
        format.Validate();
        return format;
    }

    public PlayoffSimulationResult EnsureBracket(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (scenario.Playoffs.Bracket is not null)
        {
            return Result(true, scenario, scenario.Playoffs.Bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Playoff bracket already exists.");
        }

        var standings = scenario.Standings ?? new SeasonStatsService().CreateStandings(scenario.Season.LeagueId, SeasonFrameworkService.LeagueTeams(scenario));
        var format = FormatFromRulebook(registry.Rulebook, scenario.LeagueProfile);
        if (!format.IsEnabled)
        {
            return Result(false, scenario, null, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Playoffs are disabled by rulebook.");
        }

        var ordered = standings.OrderedTeams()
            .Select((team, index) => new PlayoffTeamSeed(team.OrganizationId, team.TeamName, index + 1, team.Points, team.Wins))
            .ToArray();
        var qualifyCount = SafeQualifyCount(format.TeamsQualify, ordered.Length);
        var seeds = ordered.Take(qualifyCount).ToArray();
        var missed = ordered.Skip(qualifyCount).ToArray();
        var firstRound = CreateRound(scenario, seeds, 1, format.BestOf, scenario.CurrentDate.AddDays(1));
        var bracket = new PlayoffBracket(
            $"playoffs:{scenario.Season.SeasonId}",
            scenario.Season.LeagueId,
            scenario.Season.Year,
            format,
            PlayoffStatus.InProgress,
            seeds,
            missed,
            new[] { firstRound },
            CompletedSeries: Array.Empty<PlayoffSeriesResult>());
        var updated = scenario with
        {
            Season = scenario.Season with { CurrentPhase = SeasonPhase.Playoffs },
            Playoffs = new PlayoffState(
                Bracket: bracket,
                SkaterStats: new SeasonStatsService().CreatePlayerStats(scenario.AlphaSnapshot),
                GoalieStats: new SeasonStatsService().CreateGoalieStats(scenario.AlphaSnapshot),
                TeamStats: new SeasonStatsService().CreateTeamStats(SeasonFrameworkService.LeagueTeams(scenario)),
                GameRecaps: Array.Empty<GameRecap>(),
                LeagueNews: Array.Empty<LeagueTransaction>(),
                InboxItems: Array.Empty<AlphaInboxItem>())
        };
        registry.WorldEngine.SetPhase(WorldPhase.Playoffs);
        QueueGeneric(registry, updated, "Playoff bracket set", $"{seeds.Length} teams qualified for the {updated.Season.Year} playoffs.");
        var inbox = BuildQualificationInbox(updated, bracket).ToArray();
        var news = new[]
        {
            LeagueNews(updated, $"league-news:playoffs:set:{updated.Season.SeasonId}", null, "Playoff bracket set", $"{seeds.Length} teams qualified. Top seed: {seeds.First().TeamName}.")
        };
        updated = AppendPlayoffMessages(updated, inbox, news);
        updated.Validate();
        return Result(true, updated, bracket, Array.Empty<GameRecap>(), inbox, news, "Playoff bracket created.");
    }

    public PlayoffSimulationResult SimulateNextGame(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var ensured = EnsureBracket(registry, scenario);
        if (!ensured.Success || ensured.Bracket is null)
        {
            return ensured;
        }

        var current = ensured.ScenarioSnapshot;
        var bracket = current.Playoffs.Bracket!;
        if (bracket.Status == PlayoffStatus.Completed)
        {
            return Result(true, current, bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Playoffs are already complete.");
        }

        var series = bracket.CurrentSeries;
        if (series is null)
        {
            var advanced = AdvanceBracket(registry, current, bracket);
            return advanced;
        }

        var game = NextGameForSeries(current, series);
        var scheduled = game.ToScheduledGame();
        var simulation = new GameSimulationService().Simulate(current, scheduled);
        var completedGame = game.Complete(simulation.Result);
        series = ApplyGameToSeries(series, completedGame);
        var round = ReplaceSeries(bracket.Rounds.Single(round => round.RoundNumber == series.RoundNumber), series);
        bracket = bracket with
        {
            Rounds = bracket.Rounds.Select(item => item.RoundNumber == round.RoundNumber ? round : item).ToArray()
        };

        var stats = new SeasonStatsService();
        var playoffState = current.Playoffs;
        var playoffTeamStats = stats.ApplyTeamStats(playoffState.PlayoffTeamStats, scheduled, simulation.Result);
        var playoffSkaters = stats.ApplyPlayerStats(current.AlphaSnapshot, playoffState.PlayoffSkaterStats, scheduled, simulation);
        var playoffGoalies = stats.ApplyGoalieStats(current.AlphaSnapshot, playoffState.PlayoffGoalieStats, scheduled, simulation);
        var recapScenario = current with
        {
            Playoffs = playoffState with
            {
                Bracket = bracket,
                TeamStats = playoffTeamStats,
                SkaterStats = playoffSkaters,
                GoalieStats = playoffGoalies
            }
        };
        var recap = new GameRecapService().CreateRecap(recapScenario, scheduled.Complete(simulation.Result), simulation);
        var inbox = new List<AlphaInboxItem>();
        var news = new List<LeagueTransaction>();

        if (InvolvesPlayerTeam(current, completedGame))
        {
            inbox.Add(new AlphaInboxItem(
                $"inbox:playoff-game:{completedGame.PlayoffGameId}",
                ToDateTimeOffset(completedGame.Date, 22),
                LegacyEventType.GamePlayed,
                LegacyEventSeverity.Notice,
                $"Playoff game: {recap.BoxScore.FinalScore}",
                $"{recap.NarrativeSummary} Series: {SeriesScoreText(series)}. {recap.KeyConcern}",
                recap.NotablePlayers.FirstOrDefault()?.PersonId));
        }

        if (series.IsComplete)
        {
            var seriesResult = ToSeriesResult(series);
            bracket = bracket with
            {
                Rounds = bracket.Rounds.Select(item => item.RoundNumber == round.RoundNumber ? ReplaceSeries(round, series) : item).ToArray(),
                CompletedSeries = bracket.Results.Append(seriesResult).ToArray()
            };
            news.Add(LeagueNews(current, $"league-news:playoffs:series:{series.SeriesId}", seriesResult.WinnerOrganizationId, "Playoff series decided", seriesResult.Summary));
            if (seriesResult.WinnerOrganizationId == current.Organization.OrganizationId || seriesResult.LoserOrganizationId == current.Organization.OrganizationId)
            {
                inbox.Add(new AlphaInboxItem(
                    $"inbox:playoff-series:{series.SeriesId}",
                    ToDateTimeOffset(completedGame.Date, 23),
                    LegacyEventType.SeasonEnded,
                    seriesResult.WinnerOrganizationId == current.Organization.OrganizationId ? LegacyEventSeverity.Notice : LegacyEventSeverity.Warning,
                    seriesResult.WinnerOrganizationId == current.Organization.OrganizationId ? $"Series won: {seriesResult.WinnerTeamName}" : $"Series lost: {seriesResult.LoserTeamName}",
                    seriesResult.Summary,
                    null));
            }
        }

        var updated = current with
        {
            Playoffs = current.Playoffs with
            {
                Bracket = bracket,
                TeamStats = playoffTeamStats,
                SkaterStats = playoffSkaters,
                GoalieStats = playoffGoalies,
                GameRecaps = current.Playoffs.PlayoffGameRecaps.Append(recap).ToArray()
            }
        };
        updated = RecordPlayoffDebut(updated, completedGame);
        var advancedResult = AdvanceBracket(registry, updated, bracket);
        var advancedScenario = AppendPlayoffMessages(advancedResult.ScenarioSnapshot, inbox, news);
        var allInbox = ensured.InboxItems.Concat(inbox).Concat(advancedResult.InboxItems).DistinctBy(item => item.InboxItemId).ToArray();
        var allNews = ensured.LeagueNews.Concat(news).Concat(advancedResult.LeagueNews).DistinctBy(item => item.TransactionId).ToArray();
        advancedScenario = AppendPlayoffMessages(advancedScenario, advancedResult.InboxItems, advancedResult.LeagueNews);
        advancedScenario.Validate();
        return Result(true, advancedScenario, advancedScenario.Playoffs.Bracket, new[] { recap }.Concat(advancedResult.GameRecaps).ToArray(), allInbox, allNews, $"Simulated playoff game {completedGame.GameNumber}: {recap.BoxScore.FinalScore}.");
    }

    public PlayoffSimulationResult SimulateRound(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var current = scenario;
        var recaps = new List<GameRecap>();
        var inbox = new List<AlphaInboxItem>();
        var news = new List<LeagueTransaction>();
        var startingRound = current.Playoffs.Bracket?.CurrentSeries?.RoundNumber;
        for (var i = 0; i < 80; i++)
        {
            var result = SimulateNextGame(registry, current);
            current = result.ScenarioSnapshot;
            recaps.AddRange(result.GameRecaps);
            inbox.AddRange(result.InboxItems);
            news.AddRange(result.LeagueNews);
            if (current.Playoffs.Bracket?.Status is PlayoffStatus.Completed)
            {
                break;
            }

            var currentRound = current.Playoffs.Bracket?.CurrentSeries?.RoundNumber;
            if (startingRound is not null && currentRound != startingRound)
            {
                break;
            }
        }

        return Result(true, current, current.Playoffs.Bracket, recaps, inbox, news, "Playoff round simulation advanced.");
    }

    public PlayoffSimulationResult SimulateFullPlayoffs(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var current = scenario;
        var recaps = new List<GameRecap>();
        var inbox = new List<AlphaInboxItem>();
        var news = new List<LeagueTransaction>();
        for (var i = 0; i < 240; i++)
        {
            var result = SimulateNextGame(registry, current);
            current = result.ScenarioSnapshot;
            recaps.AddRange(result.GameRecaps);
            inbox.AddRange(result.InboxItems);
            news.AddRange(result.LeagueNews);
            if (current.Playoffs.Bracket?.Status == PlayoffStatus.Completed)
            {
                break;
            }
        }

        return Result(true, current, current.Playoffs.Bracket, recaps, inbox, news, "Playoffs completed or maximum simulation safety reached.");
    }

    public PlayoffSimulationResult AdvanceForCurrentDate(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        if (scenario.Schedule is null || scenario.Schedule.Games.Any(game => game.Status == GameStatus.Scheduled))
        {
            return Result(true, scenario, scenario.Playoffs.Bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Regular season is not complete.");
        }

        if (scenario.CurrentDate < ScheduleEngine.PlayoffsBeginDate(scenario.Season))
        {
            return Result(true, scenario, scenario.Playoffs.Bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Playoff calendar has not opened yet.");
        }

        if (scenario.Standings is null || scenario.Standings.Teams.Any(team => team.GamesPlayed == 0))
        {
            return Result(true, scenario, scenario.Playoffs.Bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Playoff standings are not ready for seeding.");
        }

        var ensured = EnsureBracket(registry, scenario);
        if (!ensured.Success || ensured.Bracket?.Status == PlayoffStatus.Completed)
        {
            return ensured;
        }

        return SimulateNextGame(registry, ensured.ScenarioSnapshot);
    }

    private static PlayoffSimulationResult AdvanceBracket(EngineRegistry registry, NewGmScenarioSnapshot scenario, PlayoffBracket bracket)
    {
        var currentRound = bracket.Rounds.OrderBy(round => round.RoundNumber).Last();
        if (!currentRound.IsComplete)
        {
            return Result(true, scenario with { Playoffs = scenario.Playoffs with { Bracket = bracket } }, bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Current playoff round is still in progress.");
        }

        var winners = currentRound.Series.Select(series => series.WinnerSeed!).OrderBy(seed => seed.Seed).ToArray();
        if (winners.Length == 1)
        {
            var champion = winners[0];
            var final = currentRound.Series.Single();
            var runnerUp = final.LoserSeed!;
            bracket = bracket with
            {
                Status = PlayoffStatus.Completed,
                ChampionOrganizationId = champion.OrganizationId,
                ChampionTeamName = champion.TeamName,
                RunnerUpOrganizationId = runnerUp.OrganizationId,
                RunnerUpTeamName = runnerUp.TeamName,
                PlayoffMvpPlaceholder = PlayoffMvp(scenario)
            };
            var updated = scenario with
            {
                Season = scenario.Season with { CurrentPhase = SeasonPhase.Championship, Status = SeasonStatus.Completed },
                Playoffs = scenario.Playoffs with { Bracket = bracket }
            };
            registry.WorldEngine.SetPhase(WorldPhase.Playoffs);
            updated = RecordChampionHistory(updated, bracket);
            var inbox = BuildChampionInbox(updated, bracket).ToArray();
            var news = new[]
            {
                LeagueNews(updated, $"league-news:playoffs:champion:{updated.Season.SeasonId}", champion.OrganizationId, "Champion crowned", $"{champion.TeamName} won the {updated.Season.Year} championship over {runnerUp.TeamName}.")
            };
            updated = AppendPlayoffMessages(updated, inbox, news);
            QueueGeneric(registry, updated, "Champion crowned", $"{champion.TeamName} won the {updated.Season.Year} championship.");
            return Result(true, updated, bracket, Array.Empty<GameRecap>(), inbox, news, $"{champion.TeamName} crowned champion.");
        }

        var nextRoundNumber = currentRound.RoundNumber + 1;
        var nextRound = CreateRound(scenario, winners, nextRoundNumber, bracket.Format.BestOf, scenario.CurrentDate.AddDays(nextRoundNumber));
        bracket = bracket with
        {
            Status = PlayoffStatus.InProgress,
            Rounds = bracket.Rounds.Append(nextRound).ToArray()
        };
        var roundNews = new[]
        {
            LeagueNews(scenario, $"league-news:playoffs:round:{scenario.Season.SeasonId}:{nextRoundNumber}", null, "Playoff round set", $"Round {nextRoundNumber} is set with {winners.Length} teams remaining.")
        };
        var updatedScenario = AppendPlayoffMessages(scenario with { Playoffs = scenario.Playoffs with { Bracket = bracket } }, Array.Empty<AlphaInboxItem>(), roundNews);
        return Result(true, updatedScenario, bracket, Array.Empty<GameRecap>(), Array.Empty<AlphaInboxItem>(), roundNews, $"Advanced to playoff round {nextRoundNumber}.");
    }

    private static PlayoffRound CreateRound(NewGmScenarioSnapshot scenario, IReadOnlyList<PlayoffTeamSeed> seeds, int roundNumber, int bestOf, DateOnly firstDate)
    {
        var pairs = new List<PlayoffSeries>();
        var ordered = seeds.OrderBy(seed => seed.Seed).ToArray();
        for (var index = 0; index < ordered.Length / 2; index++)
        {
            var high = ordered[index];
            var low = ordered[ordered.Length - 1 - index];
            var seriesId = $"playoff-series:{scenario.Season.SeasonId}:r{roundNumber}:s{index + 1}";
            pairs.Add(new PlayoffSeries(
                seriesId,
                roundNumber,
                index + 1,
                high,
                low,
                bestOf,
                Games: Array.Empty<PlayoffGame>()));
        }

        var round = new PlayoffRound(roundNumber, RoundName(roundNumber, ordered.Length), PlayoffStatus.InProgress, pairs);
        round.Validate();
        return round;
    }

    private static PlayoffGame NextGameForSeries(NewGmScenarioSnapshot scenario, PlayoffSeries series)
    {
        var nextNumber = series.GamesOrEmpty.Count + 1;
        var highHome = nextNumber is 1 or 2 or 5 or 7;
        var home = highHome ? series.HigherSeed.OrganizationId : series.LowerSeed.OrganizationId;
        var away = highHome ? series.LowerSeed.OrganizationId : series.HigherSeed.OrganizationId;
        var game = new PlayoffGame(
            $"{series.SeriesId}:g{nextNumber}",
            series.SeriesId,
            series.RoundNumber,
            nextNumber,
            scenario.CurrentDate.AddDays(series.GamesOrEmpty.Count),
            home,
            away);
        game.Validate();
        return game;
    }

    private static PlayoffSeries ApplyGameToSeries(PlayoffSeries series, PlayoffGame completedGame)
    {
        var highWon = completedGame.Result!.WinnerOrganizationId == series.HigherSeed.OrganizationId;
        var highWins = series.HigherSeedWins + (highWon ? 1 : 0);
        var lowWins = series.LowerSeedWins + (highWon ? 0 : 1);
        var complete = highWins >= series.WinsRequired || lowWins >= series.WinsRequired;
        return series with
        {
            HigherSeedWins = highWins,
            LowerSeedWins = lowWins,
            Status = complete ? PlayoffStatus.Completed : PlayoffStatus.InProgress,
            Games = series.GamesOrEmpty.Append(completedGame).ToArray(),
            WinnerOrganizationId = complete ? (highWins > lowWins ? series.HigherSeed.OrganizationId : series.LowerSeed.OrganizationId) : null,
            LoserOrganizationId = complete ? (highWins > lowWins ? series.LowerSeed.OrganizationId : series.HigherSeed.OrganizationId) : null
        };
    }

    private static PlayoffRound ReplaceSeries(PlayoffRound round, PlayoffSeries series) =>
        round with
        {
            Status = round.Series.All(item => item.SeriesId == series.SeriesId ? series.IsComplete : item.IsComplete)
                ? PlayoffStatus.RoundComplete
                : PlayoffStatus.InProgress,
            Series = round.Series.Select(item => item.SeriesId == series.SeriesId ? series : item).ToArray()
        };

    private static PlayoffSeriesResult ToSeriesResult(PlayoffSeries series)
    {
        var winner = series.WinnerSeed!;
        var loser = series.LoserSeed!;
        var winnerWins = series.WinnerOrganizationId == series.HigherSeed.OrganizationId ? series.HigherSeedWins : series.LowerSeedWins;
        var loserWins = series.LoserOrganizationId == series.HigherSeed.OrganizationId ? series.HigherSeedWins : series.LowerSeedWins;
        return new PlayoffSeriesResult(series.SeriesId, series.RoundNumber, winner.OrganizationId, winner.TeamName, loser.OrganizationId, loser.TeamName, winnerWins, loserWins, $"{winner.TeamName} defeated {loser.TeamName} {winnerWins}-{loserWins} in round {series.RoundNumber}.");
    }

    private static NewGmScenarioSnapshot RecordPlayoffDebut(NewGmScenarioSnapshot scenario, PlayoffGame game)
    {
        if (!InvolvesPlayerTeam(scenario, game))
        {
            return scenario;
        }

        var timeline = scenario.CareerTimeline;
        foreach (var player in scenario.AlphaSnapshot.Roster.ActivePlayers.Take(20))
        {
            var entryId = $"career:playoff-debut:{scenario.Season.SeasonId}:{player.PersonId}";
            if (timeline.Entries.Any(entry => entry.EntryId == entryId))
            {
                continue;
            }

            timeline = timeline.Add(new CareerTimelineEntry(
                entryId,
                CareerTimelineEntryType.Debut,
                game.Date,
                scenario.Season.Year,
                player.PersonId,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                "Playoff debut",
                $"{PlayerName(scenario, player.PersonId)} appeared in a tracked playoff game for {scenario.Organization.Name}.",
                null,
                HistoryImportance.Normal));
        }

        return scenario with { CareerTimeline = timeline };
    }

    private static NewGmScenarioSnapshot RecordChampionHistory(NewGmScenarioSnapshot scenario, PlayoffBracket bracket)
    {
        var playerTeamWon = bracket.ChampionOrganizationId == scenario.Organization.OrganizationId;
        var playerTeamRunnerUp = bracket.RunnerUpOrganizationId == scenario.Organization.OrganizationId;
        var playoffResult = playerTeamWon
            ? "Won championship"
            : playerTeamRunnerUp
                ? "Lost in final"
                : bracket.Results.Any(result => result.LoserOrganizationId == scenario.Organization.OrganizationId)
                    ? "Eliminated in playoffs"
                    : "Missed playoffs";
        var standing = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.OrganizationId);
        var history = new OrganizationSeasonHistory(
            scenario.Season.Year,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            standing is null ? "Record unavailable" : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}, {standing.Points} pts",
            playoffResult,
            scenario.CurrentDraftClassProfile?.PreviewText ?? "Draft class not summarized.",
            scenario.PlayerStats.OrderByDescending(stat => stat.Points).FirstOrDefault()?.PlayerName ?? "No notable player recorded.",
            $"Staff guided the club to playoff result: {playoffResult}.",
            "No owner change.",
            playerTeamWon ? "1 championship" : "0 championships",
            $"{scenario.Organization.Name}: {playoffResult}. Champion: {bracket.ChampionTeamName}.");
        var timeline = scenario.CareerTimeline;
        if (playerTeamWon)
        {
            foreach (var player in scenario.AlphaSnapshot.Roster.ActivePlayers.Take(24))
            {
                timeline = timeline.Add(new CareerTimelineEntry(
                    $"career:championship:{scenario.Season.SeasonId}:{player.PersonId}",
                    CareerTimelineEntryType.Championship,
                    scenario.CurrentDate,
                    scenario.Season.Year,
                    player.PersonId,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    "Championship",
                    $"{PlayerName(scenario, player.PersonId)} won a tracked championship with {scenario.Organization.Name}.",
                    null,
                    HistoryImportance.Major));
            }
        }

        return scenario with
        {
            CareerTimeline = timeline,
            OrganizationSeasonHistory = scenario.OrganizationSeasonHistory
                .Where(item => !(item.SeasonYear == history.SeasonYear && item.OrganizationId == history.OrganizationId))
                .Append(history)
                .OrderBy(item => item.SeasonYear)
                .ToArray()
        };
    }

    private static IEnumerable<AlphaInboxItem> BuildQualificationInbox(NewGmScenarioSnapshot scenario, PlayoffBracket bracket)
    {
        var playerSeed = bracket.Seeds.FirstOrDefault(seed => seed.OrganizationId == scenario.Organization.OrganizationId);
        if (playerSeed is not null)
        {
            var opponent = bracket.Rounds.First().Series.First(series => series.HigherSeed.OrganizationId == playerSeed.OrganizationId || series.LowerSeed.OrganizationId == playerSeed.OrganizationId);
            var opponentSeed = opponent.HigherSeed.OrganizationId == playerSeed.OrganizationId ? opponent.LowerSeed : opponent.HigherSeed;
            yield return new AlphaInboxItem(
                $"inbox:playoffs:qualified:{scenario.Season.SeasonId}",
                ToDateTimeOffset(scenario.CurrentDate, 9),
                LegacyEventType.SeasonEnded,
                LegacyEventSeverity.Notice,
                $"Playoff berth clinched: seed #{playerSeed.Seed}",
                $"{scenario.Organization.Name} qualified for the playoffs as seed #{playerSeed.Seed}. First opponent: {opponentSeed.TeamName}.",
                null);
        }
        else
        {
            yield return new AlphaInboxItem(
                $"inbox:playoffs:missed:{scenario.Season.SeasonId}",
                ToDateTimeOffset(scenario.CurrentDate, 9),
                LegacyEventType.SeasonEnded,
                LegacyEventSeverity.Warning,
                "Playoffs missed",
                $"{scenario.Organization.Name} missed the playoffs. Offseason review will focus on roster and staff decisions.",
                null);
        }
    }

    private static IEnumerable<AlphaInboxItem> BuildChampionInbox(NewGmScenarioSnapshot scenario, PlayoffBracket bracket)
    {
        if (bracket.ChampionOrganizationId == scenario.Organization.OrganizationId)
        {
            yield return new AlphaInboxItem(
                $"inbox:playoffs:champion:{scenario.Season.SeasonId}",
                ToDateTimeOffset(scenario.CurrentDate, 23),
                LegacyEventType.SeasonEnded,
                LegacyEventSeverity.Notice,
                $"Champions: {bracket.ChampionTeamName}",
                $"{bracket.ChampionTeamName} won the championship. Playoff MVP placeholder: {bracket.PlayoffMvpPlaceholder}.",
                null);
        }
        else if (bracket.RunnerUpOrganizationId == scenario.Organization.OrganizationId)
        {
            yield return new AlphaInboxItem(
                $"inbox:playoffs:runner-up:{scenario.Season.SeasonId}",
                ToDateTimeOffset(scenario.CurrentDate, 23),
                LegacyEventType.SeasonEnded,
                LegacyEventSeverity.Warning,
                "Final series lost",
                $"{scenario.Organization.Name} reached the final but lost to {bracket.ChampionTeamName}.",
                null);
        }
    }

    private static NewGmScenarioSnapshot AppendPlayoffMessages(NewGmScenarioSnapshot scenario, IReadOnlyList<AlphaInboxItem> inbox, IReadOnlyList<LeagueTransaction> news) =>
        scenario with
        {
            Playoffs = scenario.Playoffs with
            {
                InboxItems = scenario.Playoffs.PlayoffInboxItems.Concat(inbox).DistinctBy(item => item.InboxItemId).ToArray(),
                LeagueNews = scenario.Playoffs.PlayoffLeagueNews.Concat(news).DistinctBy(item => item.TransactionId).ToArray()
            }
        };

    private static LeagueTransaction LeagueNews(NewGmScenarioSnapshot scenario, string id, string? organizationId, string title, string description) =>
        new(id, ToDateTimeOffset(scenario.CurrentDate, 18), organizationId ?? scenario.Season.LeagueId, TeamName(scenario, organizationId ?? scenario.Organization.OrganizationId), null, title, LeagueTransactionType.SeasonCompleted, LeagueNewsCategory.League, description);

    private static void QueueGeneric(EngineRegistry registry, NewGmScenarioSnapshot scenario, string title, string summary)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            ToDateTimeOffset(scenario.CurrentDate, 18),
            LegacyEventType.SeasonEnded,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            title,
            summary,
            new LegacyEventContext(OrganizationId: scenario.Organization.OrganizationId, LeagueId: scenario.Season.LeagueId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["season_year"] = scenario.Season.Year });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static PlayoffSimulationResult Result(bool success, NewGmScenarioSnapshot scenario, PlayoffBracket? bracket, IReadOnlyList<GameRecap> recaps, IReadOnlyList<AlphaInboxItem> inbox, IReadOnlyList<LeagueTransaction> news, string message)
    {
        var result = new PlayoffSimulationResult(success, scenario, bracket, recaps, inbox, news, message);
        result.Validate();
        return result;
    }

    private static int SafeQualifyCount(int requested, int available)
    {
        var capped = Math.Min(requested, available);
        var power = 1;
        while (power * 2 <= capped)
        {
            power *= 2;
        }

        return Math.Max(2, power);
    }

    private static int DefaultSeriesLength(LeagueProfile? leagueProfile) =>
        leagueProfile?.Experience == LeagueExperience.Junior ? 7 : 7;

    private static string RoundName(int roundNumber, int teamCount) =>
        teamCount switch
        {
            2 => "Championship Final",
            4 => "Semifinal",
            8 => "Quarterfinal",
            _ => $"Round {roundNumber}"
        };

    private static string SeriesScoreText(PlayoffSeries series) =>
        $"{series.HigherSeed.TeamName} {series.HigherSeedWins}, {series.LowerSeed.TeamName} {series.LowerSeedWins}";

    private static bool InvolvesPlayerTeam(NewGmScenarioSnapshot scenario, PlayoffGame game) =>
        game.HomeOrganizationId == scenario.Organization.OrganizationId || game.AwayOrganizationId == scenario.Organization.OrganizationId;

    private static string PlayoffMvp(NewGmScenarioSnapshot scenario) =>
        scenario.Playoffs.PlayoffSkaterStats.OrderByDescending(stat => stat.Points).ThenByDescending(stat => stat.Goals).FirstOrDefault()?.PlayerName
        ?? scenario.Playoffs.PlayoffGoalieStats.OrderByDescending(stat => stat.Wins).FirstOrDefault()?.PlayerName
        ?? "MVP placeholder pending stats review";

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

        var leagueTeam = SeasonFrameworkService.LeagueTeams(scenario).FirstOrDefault(team => team.OrganizationId == organizationId);
        return string.IsNullOrWhiteSpace(leagueTeam.TeamName) ? organizationId : leagueTeam.TeamName;
    }

    private static string PlayerName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private static DateTimeOffset ToDateTimeOffset(DateOnly date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.Zero);
}
