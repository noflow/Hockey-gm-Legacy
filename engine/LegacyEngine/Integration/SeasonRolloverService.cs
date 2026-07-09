using LegacyEngine.Contracts;
using LegacyEngine.Draft;
using LegacyEngine.Events;
using LegacyEngine.Names;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.Seasons;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed class SeasonRolloverService
{
    private const int NextDraftClassSize = 60;

    public bool IsRegularSeasonComplete(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return scenario.Schedule is { Games.Count: > 0 }
            && scenario.Schedule.Games.All(game => game.Status == GameStatus.Completed);
    }

    public SeasonCompletionResult CompleteSeasonAndEnterOffseason(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (!IsRegularSeasonComplete(scenario))
        {
            return Result(false, scenario, null, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), "Season cannot be completed until every regular season game is finished.");
        }

        var archive = BuildArchive(scenario);
        var endReview = new ExecutiveReportService().GenerateEndOfSeasonExecutiveReview(registry, scenario with
        {
            Season = scenario.Season with { Status = SeasonStatus.Completed }
        });
        var reportScenario = endReview.Success ? endReview.ScenarioSnapshot : scenario;
        var transition = BuildTransition(reportScenario, archive);
        var nextSeason = registry.SeasonEngine.CreateSeason(
            transition.ToSeasonId,
            reportScenario.Season.LeagueId,
            transition.ToSeasonYear,
            reportScenario.Season.Settings,
            registry.Rulebook,
            reportScenario.CurrentDate);
        nextSeason = nextSeason with
        {
            CurrentPhase = SeasonPhase.Offseason,
            Status = SeasonStatus.Upcoming
        };
        registry.WorldEngine.SetPhase(WorldPhase.Offseason);

        var expiredContracts = ExpiringContracts(reportScenario, reportScenario.CurrentDate).ToArray();
        var pending = CreateContractPendingActions(reportScenario, expiredContracts);
        var draftClass = GenerateNextDraftClass(reportScenario, transition.ToSeasonYear);
        var stats = new SeasonStatsService();
        var teams = SeasonFrameworkService.LeagueTeams(reportScenario);
        var resetStandings = stats.CreateStandings(reportScenario.Season.LeagueId, teams);
        var resetTeamStats = stats.CreateTeamStats(teams);
        var resetPlayerStats = stats.CreatePlayerStats(reportScenario.AlphaSnapshot);
        var resetGoalieStats = stats.CreateGoalieStats(reportScenario.AlphaSnapshot);
        var priorStats = BuildPriorSeasonStats(reportScenario);
        var careerStats = RollCareerStats(reportScenario, priorStats);
        var organizationHistory = AppendOrganizationHistory(reportScenario, archive);
        var state = new SeasonRolloverState(
            CurrentSeasonCompleted: true,
            CompletedOn: reportScenario.CurrentDate,
            LastTransition: transition,
            Archives: reportScenario.SeasonRollover.SeasonArchives.Append(archive).ToArray(),
            OffseasonChecklist: BuildOffseasonChecklist(expiredContracts.Length, draftClass.People.Count, reportScenario.StaffMembers.Count),
            ExpiringContractPersonIds: expiredContracts.Select(contract => contract.PersonId).Distinct(StringComparer.Ordinal).ToArray(),
            DraftClassSummary: draftClass.Summary);

        var alpha = reportScenario.AlphaSnapshot with
        {
            WorldState = registry.WorldEngine.State,
            Season = nextSeason,
            People = reportScenario.AlphaSnapshot.People.Concat(draftClass.People).DistinctBy(person => person.PersonId).ToArray(),
            Recruits = draftClass.Recruits,
            DraftBoard = draftClass.DraftBoard,
            Contracts = reportScenario.Contracts
                .Select(contract => expiredContracts.Any(expired => expired.ContractId == contract.ContractId) ? contract.Expire(reportScenario.CurrentDate) : contract)
                .ToArray()
        };
        var updated = reportScenario with
        {
            AlphaSnapshot = alpha,
            Season = nextSeason,
            DraftDate = transition.NextDraftDate,
            Contracts = alpha.Contracts,
            PendingActions = reportScenario.PendingActions.Concat(pending).ToArray(),
            CompletedScoutingReports = Array.Empty<ScoutingReport>(),
            Schedule = null,
            Standings = resetStandings,
            TeamStats = resetTeamStats,
            PlayerStats = resetPlayerStats,
            GoalieStats = resetGoalieStats,
            GameRecaps = Array.Empty<GameRecap>(),
            Playoffs = PlayoffState.Empty,
            PriorSeasonStats = MergePriorStats(reportScenario.PriorSeasonStats, priorStats),
            CareerStatSummaries = careerStats,
            OrganizationSeasonHistory = organizationHistory,
            SeasonReadiness = new SeasonReadinessState(),
            TrainingCamp = null,
            DraftExperience = null,
            CurrentDraftClassProfile = draftClass.Profile,
            DraftRights = Array.Empty<DraftPickSummary>(),
            SeasonRollover = state
        };

        QueueSeasonEnded(registry, updated, archive);
        var inbox = BuildInbox(updated, archive, expiredContracts.Length);
        var transactions = new[]
        {
            new LeagueTransaction(
                $"transaction:season-complete:{archive.SeasonId}:{Guid.NewGuid():N}",
                ToDateTimeOffset(updated.CurrentDate, 18),
                updated.Organization.OrganizationId,
                updated.Organization.Name,
                null,
                archive.ChampionTeamName,
                LeagueTransactionType.SeasonCompleted,
                LeagueNewsCategory.League,
                scenario.Playoffs.Bracket?.Status == PlayoffStatus.Completed
                    ? $"{archive.ChampionTeamName} won the {archive.SeasonYear} championship."
                    : $"{archive.ChampionTeamName} finished as the tracked regular-season leader for {archive.SeasonYear}.")
        };
        updated.Validate();
        return Result(true, updated, archive, inbox.Concat(endReview.InboxItems).ToArray(), transactions, $"Season {archive.SeasonYear} archived. Offseason for {transition.ToSeasonYear} is ready.");
    }

    private static SeasonArchive BuildArchive(NewGmScenarioSnapshot scenario)
    {
        var standings = scenario.Standings ?? new SeasonStatsService().CreateStandings(scenario.Season.LeagueId, SeasonFrameworkService.LeagueTeams(scenario));
        var champion = standings.OrderedTeams().FirstOrDefault();
        var playoffChampion = scenario.Playoffs.Bracket?.Status == PlayoffStatus.Completed
            ? scenario.Playoffs.Bracket.ChampionTeamName
            : null;
        var playerStanding = standings.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.OrganizationId);
        var summary = playerStanding is null
            ? $"{scenario.Organization.Name} completed the {scenario.Season.Year} season."
            : $"{scenario.Organization.Name} finished {playerStanding.Wins}-{playerStanding.Losses}-{playerStanding.OvertimeLosses} with {playerStanding.Points} point(s).";
        if (scenario.Playoffs.Bracket?.Status == PlayoffStatus.Completed)
        {
            summary = $"{summary} Champion: {scenario.Playoffs.Bracket.ChampionTeamName}.";
        }

        var archive = new SeasonArchive(
            ArchiveId: $"season-archive:{scenario.Season.SeasonId}",
            SeasonId: scenario.Season.SeasonId,
            SeasonYear: scenario.Season.Year,
            CompletedOn: scenario.CurrentDate,
            LeagueId: scenario.Season.LeagueId,
            OrganizationId: scenario.Organization.OrganizationId,
            OrganizationName: scenario.Organization.Name,
            FinalStandings: standings,
            TeamStats: scenario.TeamStats,
            PlayerStats: scenario.PlayerStats,
            GoalieStats: scenario.GoalieStats,
            GameResults: scenario.Schedule?.Games.Where(game => game.Status == GameStatus.Completed).ToArray() ?? Array.Empty<ScheduledGame>(),
            GameRecaps: scenario.GameRecaps,
            ChampionTeamName: playoffChampion ?? champion?.TeamName ?? "champion unavailable",
            Summary: summary);
        archive.Validate();
        return archive;
    }

    private static SeasonYearTransition BuildTransition(NewGmScenarioSnapshot scenario, SeasonArchive archive)
    {
        var nextYear = scenario.Season.Year + 1;
        var nextDraft = SeasonCalendar.Build(nextYear, scenario.Season.Settings)
            .Milestones
            .Single(milestone => milestone.Type == SeasonMilestoneType.Draft)
            .Date
            .Value;
        var transition = new SeasonYearTransition(
            FromSeasonYear: scenario.Season.Year,
            ToSeasonYear: nextYear,
            FromSeasonId: scenario.Season.SeasonId,
            ToSeasonId: $"{scenario.Season.LeagueId}-{nextYear}",
            TransitionDate: scenario.CurrentDate,
            NextDraftDate: nextDraft,
            Summary: $"{archive.OrganizationName} moved from {scenario.Season.Year} into the {nextYear} offseason.");
        transition.Validate();
        return transition;
    }

    private static IReadOnlyList<Contract> ExpiringContracts(NewGmScenarioSnapshot scenario, DateOnly onDate) =>
        scenario.Contracts
            .Where(contract => contract.Status == ContractStatus.Signed && contract.Term.EndDate <= onDate)
            .ToArray();

    private static IReadOnlyList<PendingGmAction> CreateContractPendingActions(NewGmScenarioSnapshot scenario, IReadOnlyList<Contract> expiredContracts) =>
        expiredContracts
            .Where(contract => scenario.PendingActions.All(action => action.PersonId != contract.PersonId || !action.IsOpen))
            .Select(contract =>
            {
                var name = PersonName(scenario, contract.PersonId);
                return new PendingGmAction(
                    ActionId: $"pending-gm:contract-rollover:{contract.ContractId}:{Guid.NewGuid():N}",
                    ActionType: PendingGmActionType.ApproveContract,
                    Status: PendingGmActionStatus.Pending,
                    CreatedOn: scenario.CurrentDate,
                    PersonId: contract.PersonId,
                    PersonName: name,
                    OrganizationId: contract.OrganizationId,
                    Title: $"Renew contract: {name}",
                    Reason: $"{name}'s contract expired on {contract.Term.EndDate:yyyy-MM-dd}. The GM must approve renewal, walk away, or replace the role/player.",
                    RecommendedAction: "Review the expiring contract before making a renewal or release decision.",
                    Position: PositionFor(scenario, contract.PersonId),
                    ContractType: contract.ContractType);
            })
            .ToArray();

    private static GeneratedDraftClass GenerateNextDraftClass(NewGmScenarioSnapshot scenario, int seasonYear)
    {
        var draftClassGenerator = new DraftClassGenerator();
        var profile = draftClassGenerator.GenerateProfile(scenario.LeagueProfile.Rulebook, seasonYear, scenario.Season.LeagueId, NextDraftClassSize);
        var registry = new NameUniquenessRegistry();
        foreach (var person in scenario.AlphaSnapshot.People)
        {
            registry.RegisterExisting(Scope(seasonYear), person.Identity.DisplayName);
        }

        var generator = new NameGenerator(NameGenerationSettings.CreateDefault(seasonYear + 4040));
        var people = new List<Person>();
        var recruits = new List<RecruitProfile>();
        var board = DraftBoard.Create($"draft-board-{scenario.Organization.OrganizationId}-{seasonYear}", scenario.Organization.OrganizationId);

        for (var index = 0; index < profile.TotalProspects; index++)
        {
            var name = generator.Generate(registry, Scope(seasonYear), PlayerOrigins());
            var personId = $"person-draft-{seasonYear}-{index + 1:000}";
            var position = draftClassGenerator.PositionFor(profile, index);
            var birthYear = DraftBirthYearFor(seasonYear, scenario.LeagueProfile.Rulebook, index);
            var person = NewGmScenarioBootstrapper.CreateScenarioPersonForGeneratedSystems(
                personId,
                name.FirstName,
                name.LastName,
                new DateOnly(birthYear, 1, Math.Min(24, (index % 25) + 1)),
                name.Nationality,
                name.Birthplace,
                "unassigned");
            people.Add(person);

            var recruit = RecruitProfile.Create(personId, PrioritiesFor(index))
                .ChangeInterest(scenario.Organization.OrganizationId, 25 + (index % 18), scenario.CurrentDate);
            recruits.Add(recruit);
            board = board.AddProspect(new DraftBoardEntry(
                ProspectPersonId: personId,
                Rank: index + 1,
                ScoutingReportId: null,
                ScoutingConfidence: draftClassGenerator.StartingConfidence(profile, index + 1, inheritedScouting: false),
                ProjectionText: draftClassGenerator.ProjectionFor(profile, position, index),
                IsStarred: index < 3,
                PersonalNotes: "",
                AnalyticsSummary: draftClassGenerator.AnalyticsFor(profile, position, index),
                Bio: draftClassGenerator.BuildBio(profile, position, index, birthYear, name.Birthplace),
                RiskSummary: draftClassGenerator.RiskFor(profile, position, index),
                ClassContextNote: draftClassGenerator.ClassContextFor(profile, position, index + 1)));
        }

        var summary = draftClassGenerator.BuildSummary(profile, board);
        return new GeneratedDraftClass(people, recruits, board, profile, $"{profile.PreviewText} {summary.Profile.ScoutQuote}");
    }

    private static int DraftBirthYearFor(int seasonYear, Rulebook rulebook, int index)
    {
        var age = rulebook.LeagueType == "nhl_style"
            ? NhlDraftAgeFor(index)
            : 16 + (index % 2);
        return seasonYear - age;
    }

    private static int NhlDraftAgeFor(int index)
    {
        var slot = index % 20;
        if (slot == 0)
        {
            return 17;
        }

        if (slot <= 8)
        {
            return 18;
        }

        return slot <= 17 ? 19 : 20;
    }

    private static string DraftClassSummary(DraftBoard board)
    {
        var forwards = board.Entries.Count(entry => entry.Bio?.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing);
        var defense = board.Entries.Count(entry => entry.Bio?.Position == RosterPosition.Defense);
        var goalies = board.Entries.Count(entry => entry.Bio?.Position == RosterPosition.Goalie);
        if (goalies >= 8)
        {
            return "Strong goalie class with several early names worth extra viewings.";
        }

        if (defense < 12)
        {
            return "Weak defense class; staff should identify blue-line targets early.";
        }

        return forwards >= 35
            ? "Deep forward class with useful range through the middle rounds."
            : "Balanced class with no clear weakness from the early list.";
    }

    private static IReadOnlyList<PriorSeasonStatLine> BuildPriorSeasonStats(NewGmScenarioSnapshot scenario)
    {
        var skaters = scenario.PlayerStats.Select(stat =>
        {
            var position = PositionFor(scenario, stat.PersonId);
            return new PriorSeasonStatLine(
                stat.PersonId,
                stat.PlayerName,
                scenario.Season.Year,
                scenario.Organization.Name,
                scenario.Season.LeagueId,
                position == RosterPosition.Unknown ? RosterPosition.Center : position,
                stat.GamesPlayed,
                stat.Goals,
                stat.Assists,
                stat.PlusMinus,
                stat.PenaltyMinutes);
        });
        var goalies = scenario.GoalieStats.Select(stat => new PriorSeasonStatLine(
            stat.PersonId,
            stat.PlayerName,
            scenario.Season.Year,
            scenario.Organization.Name,
            scenario.Season.LeagueId,
            RosterPosition.Goalie,
            stat.GamesPlayed,
            Wins: stat.Wins,
            Losses: stat.Losses,
            SavePercentage: stat.SavePercentage,
            GoalsAgainstAverage: stat.GoalsAgainstAverage));
        return skaters.Concat(goalies).ToArray();
    }

    private static IReadOnlyList<PriorSeasonStatLine> MergePriorStats(IReadOnlyList<PriorSeasonStatLine> existing, IReadOnlyList<PriorSeasonStatLine> additions) =>
        existing
            .Where(stat => additions.All(addition => addition.PersonId != stat.PersonId || addition.SeasonYear != stat.SeasonYear))
            .Concat(additions)
            .OrderByDescending(stat => stat.SeasonYear)
            .ThenBy(stat => stat.PlayerName, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<CareerStatSummary> RollCareerStats(NewGmScenarioSnapshot scenario, IReadOnlyList<PriorSeasonStatLine> additions)
    {
        var existing = scenario.CareerStatSummaries.ToDictionary(summary => summary.PersonId, StringComparer.Ordinal);
        foreach (var stat in additions)
        {
            if (existing.TryGetValue(stat.PersonId, out var current))
            {
                existing[stat.PersonId] = stat.IsGoalie
                    ? current with
                    {
                        Seasons = current.Seasons + 1,
                        GamesPlayed = current.GamesPlayed + stat.GamesPlayed,
                        Wins = current.Wins + stat.Wins,
                        Losses = current.Losses + stat.Losses,
                        SummaryText = ""
                    }
                    : current with
                    {
                        Seasons = current.Seasons + 1,
                        GamesPlayed = current.GamesPlayed + stat.GamesPlayed,
                        Goals = current.Goals + stat.Goals,
                        Assists = current.Assists + stat.Assists,
                        PenaltyMinutes = current.PenaltyMinutes + stat.PenaltyMinutes,
                        SummaryText = ""
                    };
            }
            else
            {
                existing[stat.PersonId] = new CareerStatSummary(
                    stat.PersonId,
                    stat.PlayerName,
                    stat.Position,
                    1,
                    stat.GamesPlayed,
                    stat.Goals,
                    stat.Assists,
                    stat.PenaltyMinutes,
                    stat.Wins,
                    stat.Losses,
                    PrimaryLeague: stat.LeagueName);
            }
        }

        return existing.Values.OrderBy(summary => summary.PlayerName, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<OrganizationSeasonHistory> AppendOrganizationHistory(NewGmScenarioSnapshot scenario, SeasonArchive archive)
    {
        var standing = archive.PlayerTeamStanding;
        var record = standing is null ? "0-0-0" : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}, {standing.Points} pts";
        var bracket = scenario.Playoffs.Bracket;
        var playoffResult = bracket?.Status == PlayoffStatus.Completed
            ? bracket.ChampionOrganizationId == archive.OrganizationId
                ? "Won championship"
                : bracket.RunnerUpOrganizationId == archive.OrganizationId
                    ? "Lost in final"
                    : bracket.Results.Any(result => result.LoserOrganizationId == archive.OrganizationId)
                        ? "Eliminated in playoffs"
                        : "Missed playoffs"
            : "No tracked playoff bracket.";
        var championships = bracket?.Status == PlayoffStatus.Completed && bracket.ChampionOrganizationId == archive.OrganizationId
            ? "1 championship"
            : "None.";
        var history = new OrganizationSeasonHistory(
            archive.SeasonYear,
            archive.OrganizationId,
            archive.OrganizationName,
            record,
            playoffResult,
            scenario.DraftClassHistory.OrderByDescending(item => item.Year).FirstOrDefault()?.Summary ?? "Draft class tracked in history.",
            TopPlayerSummary(scenario),
            $"{scenario.StaffMembers.Count} staff members carried into the offseason.",
            "No owner change.",
            championships,
            archive.Summary);
        history.Validate();
        return scenario.OrganizationSeasonHistory
            .Where(item => item.SeasonYear != archive.SeasonYear)
            .Append(history)
            .OrderByDescending(item => item.SeasonYear)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildOffseasonChecklist(int expiringContracts, int draftClassCount, int staffCount) =>
    [
        expiringContracts > 0 ? $"Review {expiringContracts} expiring contract decision(s)." : "No immediate contract expiries require action.",
        "Review the free agent market before training camp.",
        staffCount >= 4 ? "Confirm staff roles and scouting coverage." : "Fill staff vacancies before camp.",
        $"Assign scouts to the new draft class ({draftClassCount} prospects).",
        "Prepare training camp and opening roster plan."
    ];

    private static IReadOnlyList<AlphaInboxItem> BuildInbox(NewGmScenarioSnapshot scenario, SeasonArchive archive, int expiringContractCount) =>
    [
        new AlphaInboxItem(
            $"inbox:season-complete:{archive.SeasonId}:{Guid.NewGuid():N}",
            ToDateTimeOffset(scenario.CurrentDate, 17),
            LegacyEventType.SeasonEnded,
            LegacyEventSeverity.Notice,
            $"Season complete: {archive.SeasonYear}",
            $"{archive.Summary} The offseason checklist is ready and the next draft class has been generated.",
            null),
        new AlphaInboxItem(
            $"inbox:offseason-start:{archive.SeasonId}:{Guid.NewGuid():N}",
            ToDateTimeOffset(scenario.CurrentDate, 17),
            LegacyEventType.PhaseChanged,
            expiringContractCount > 0 ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            "Offseason checklist ready",
            expiringContractCount > 0
                ? $"{expiringContractCount} expiring contract decision(s) need GM review. No players or staff were automatically signed or released."
                : "Offseason prep is open. No players or staff were automatically signed or released.",
            null)
    ];

    private static void QueueSeasonEnded(EngineRegistry registry, NewGmScenarioSnapshot scenario, SeasonArchive archive)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            ToDateTimeOffset(scenario.CurrentDate, 17),
            LegacyEventType.SeasonEnded,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            "Season completed",
            archive.Summary,
            new LegacyEventContext(OrganizationId: scenario.Organization.OrganizationId, LeagueId: scenario.Season.LeagueId, SeasonId: archive.SeasonId),
            new Dictionary<string, object?>
            {
                ["season_year"] = archive.SeasonYear,
                ["champion"] = archive.ChampionTeamName,
                ["alpha_6_9"] = true
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static SeasonCompletionResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        SeasonArchive? archive,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        IReadOnlyList<LeagueTransaction> transactions,
        string message)
    {
        var result = new SeasonCompletionResult(success, scenario, archive, inboxItems, transactions, message);
        result.Validate();
        return result;
    }

    private static RosterPosition PositionFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? RosterPosition.Unknown;

    private static RosterPosition PositionForIndex(int index) =>
        index switch
        {
            0 or 11 or 23 or 37 or 49 => RosterPosition.Goalie,
            _ when index % 4 == 0 || index % 4 == 1 => RosterPosition.Defense,
            _ when index % 4 == 2 => RosterPosition.Center,
            _ when index % 6 == 3 => RosterPosition.LeftWing,
            _ => RosterPosition.RightWing
        };

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private static string TopPlayerSummary(NewGmScenarioSnapshot scenario)
    {
        var topSkater = scenario.PlayerStats.OrderByDescending(stat => stat.Points).FirstOrDefault();
        if (topSkater is not null)
        {
            return $"{topSkater.PlayerName} led tracked skaters with {topSkater.Points} point(s).";
        }

        var topGoalie = scenario.GoalieStats.OrderByDescending(stat => stat.Wins).FirstOrDefault();
        return topGoalie is null ? "No player stat leader recorded." : $"{topGoalie.PlayerName} led goalies with {topGoalie.Wins} win(s).";
    }

    private static DraftProspectBio BioFor(RosterPosition position, int index, int birthYear, string birthplace)
    {
        var hometownParts = birthplace.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var hometown = hometownParts.ElementAtOrDefault(0) ?? "Saskatoon";
        var region = hometownParts.ElementAtOrDefault(1) ?? "SK";
        var country = hometownParts.ElementAtOrDefault(2) ?? "Canada";
        var height = position switch
        {
            RosterPosition.Goalie => 72 + (index % 7),
            RosterPosition.Defense => 70 + (index % 8),
            _ => 68 + (index % 8)
        };
        var weight = position switch
        {
            RosterPosition.Goalie => 178 + (index % 52),
            RosterPosition.Defense => 176 + (index % 55),
            _ => 162 + (index % 50)
        };
        return new DraftProspectBio(
            position,
            position == RosterPosition.Goalie ? (index % 2 == 0 ? "Catches L" : "Catches R") : (index % 3 == 0 ? "Shoots R" : "Shoots L"),
            height,
            weight,
            birthYear,
            hometown,
            region,
            country,
            $"{hometown} {TeamNickname(index)}",
            LeagueFor(country, index),
            CharacterFor(index),
            ProjectionRoleFor(position));
    }

    private static string ProjectionFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => "Goalie prospect with a wide projection band; scout viewings should clarify starter or backup path.",
            RosterPosition.Defense => index % 2 == 0 ? "Defense prospect with top-four traits and puck-moving upside." : "Defense prospect with second-pair tools and a conservative development path.",
            RosterPosition.Center => "Center prospect with middle-six upside and a two-way junior projection.",
            RosterPosition.LeftWing or RosterPosition.RightWing => "Winger prospect with scoring-line flashes and adjustment risk.",
            _ => "Draft prospect with incomplete information."
        };

    private static string ProjectionRoleFor(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "development goalie with starter upside",
            RosterPosition.Defense => "top-four or second-pair defense projection",
            RosterPosition.Center => "middle-six center projection",
            RosterPosition.LeftWing or RosterPosition.RightWing => "scoring-line winger projection",
            _ => "depth lineup projection"
        };

    private static string CharacterFor(int index) =>
        index % 5 == 0 ? "High-energy personality; leadership traits are emerging." :
        index % 3 == 0 ? "Driven player who wants a clear development plan." :
        "Solid character profile; staff need more viewings before a firm recommendation.";

    private static string LeagueFor(string country, int index) =>
        country switch
        {
            "USA" => index % 2 == 0 ? "USHL Futures" : "High School",
            "Finland" => "U18 SM-sarja",
            "Sweden" => "J18 Nationell",
            "Czechia" => "Czech U20",
            "Switzerland" => "U20-Elit",
            _ => index % 3 == 0 ? "CSSHL U18" : index % 3 == 1 ? "SMAAAHL" : "AEHL U18"
        };

    private static string TeamNickname(int index) =>
        new[] { "Raiders", "Blazers", "Kings", "Flyers", "Storm", "Royals", "Tigers", "Saints" }[index % 8];

    private static IReadOnlyDictionary<RecruitPriority, int> PrioritiesFor(int index) =>
        new Dictionary<RecruitPriority, int>
        {
            [RecruitPriority.IceTime] = 65 + (index % 16),
            [RecruitPriority.Development] = 75 + (index % 12),
            [RecruitPriority.Education] = 50 + (index % 14),
            [RecruitPriority.Winning] = 54 + (index % 15),
            [RecruitPriority.DistanceFromHome] = 40 + (index % 25),
            [RecruitPriority.Facilities] = 58 + (index % 14),
            [RecruitPriority.Coaching] = 70 + (index % 12),
            [RecruitPriority.PathwayToHigherHockey] = 72 + (index % 14),
            [RecruitPriority.FamilyComfort] = 55 + (index % 16),
            [RecruitPriority.TeamCulture] = 52 + (index % 18),
            [RecruitPriority.TrustInGm] = 48 + (index % 16),
            [RecruitPriority.PlayingRole] = 60 + (index % 18)
        };

    private static string Scope(int seasonYear) => $"alpha-4-draft-class:{seasonYear}";

    private static NameOrigin[] PlayerOrigins() =>
    [
        NameOrigin.CanadaEnglish,
        NameOrigin.CanadaEnglish,
        NameOrigin.CanadaFrench,
        NameOrigin.Usa,
        NameOrigin.Finland,
        NameOrigin.Sweden,
        NameOrigin.Czechia,
        NameOrigin.Slovakia,
        NameOrigin.Germany,
        NameOrigin.Switzerland,
        NameOrigin.Latvia,
        NameOrigin.GenericEuropean
    ];

    private static DateTimeOffset ToDateTimeOffset(DateOnly date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.Zero);

    private sealed record GeneratedDraftClass(
        IReadOnlyList<Person> People,
        IReadOnlyList<RecruitProfile> Recruits,
        DraftBoard DraftBoard,
        DraftClassProfile Profile,
        string Summary);
}
