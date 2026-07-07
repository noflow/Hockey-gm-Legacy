using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class TradeDeadlineService
{
    private static readonly IReadOnlyList<(string First, string Last, string Nationality, string Birthplace)> DeadlineNames =
    [
        ("Ryan", "Cole", "Canada", "Medicine Hat, AB, Canada"),
        ("Evan", "Morin", "Canada", "Sherbrooke, QC, Canada"),
        ("Dylan", "Stone", "Canada", "Regina, SK, Canada"),
        ("Markus", "Lindholm", "Sweden", "Vasteras, Sweden"),
        ("Tomas", "Novak", "Czechia", "Brno, Czechia"),
        ("Tyler", "Reid", "USA", "Grand Forks, ND, USA"),
        ("Oskar", "Lehtinen", "Finland", "Turku, Finland"),
        ("Nicolas", "Bouchard", "Canada", "Quebec City, QC, Canada")
    ];

    public TradeDeadlineSettings BuildSettings(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var deadline = scenario.Season.Calendar.Milestones
            .SingleOrDefault(item => item.Type == SeasonMilestoneType.TradeDeadline)
            ?.Date.Value;
        if (deadline is null)
        {
            var settings = rulebook is null ? SeasonSettings.Default : SeasonSettings.FromRulebook(rulebook);
            deadline = SeasonCalendar.Build(scenario.Season.Year, settings)
                .Milestones.Single(item => item.Type == SeasonMilestoneType.TradeDeadline)
                .Date.Value;
        }

        var output = new TradeDeadlineSettings(deadline.Value, AllowPostDeadlineTrades: false);
        output.Validate();
        return output;
    }

    public TradeDeadlineWindow GetWindow(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var settings = BuildSettings(scenario, rulebook);
        var daysRemaining = settings.DeadlineDate.DayNumber - scenario.CurrentDate.DayNumber;
        var playoffsBegin = scenario.Season.Calendar.Milestones
            .SingleOrDefault(item => item.Type == SeasonMilestoneType.PlayoffsBegin)
            ?.Date.Value ?? settings.DeadlineDate.AddDays(60);
        var afterDeadlineClosedWindow = scenario.CurrentDate > settings.DeadlineDate && scenario.CurrentDate < playoffsBegin;
        var afterSeasonDeadlineWindow = scenario.CurrentDate >= playoffsBegin;
        var status = afterSeasonDeadlineWindow
            ? TradeDeadlineStatus.NotStarted
            : daysRemaining switch
        {
            < 0 => TradeDeadlineStatus.Closed,
            0 => TradeDeadlineStatus.DeadlineDay,
            <= 7 => TradeDeadlineStatus.DeadlineWeek,
            <= 30 => TradeDeadlineStatus.Approaching,
            _ => TradeDeadlineStatus.NotStarted
        };
        if (afterDeadlineClosedWindow)
        {
            status = TradeDeadlineStatus.Closed;
        }

        var tradesAllowed = status != TradeDeadlineStatus.Closed || settings.AllowPostDeadlineTrades;
        var summary = status switch
        {
            TradeDeadlineStatus.NotStarted when afterSeasonDeadlineWindow => "Trade deadline window has passed for the completed season; offseason trading is open.",
            TradeDeadlineStatus.NotStarted => $"Trade deadline is {settings.DeadlineDate:yyyy-MM-dd}.",
            TradeDeadlineStatus.Approaching => $"Trade deadline approaching: {daysRemaining} day(s) remaining.",
            TradeDeadlineStatus.DeadlineWeek => $"Trade deadline week: {daysRemaining} day(s) remaining.",
            TradeDeadlineStatus.DeadlineDay => "Trade Deadline: Today.",
            _ => "Trade deadline has passed. New trade proposals are closed until the playoff/offseason window."
        };
        var window = new TradeDeadlineWindow(settings.DeadlineDate, daysRemaining, status, tradesAllowed, summary);
        window.Validate();
        return window;
    }

    public BuyerSellerAssessment AssessBuyerSeller(NewGmScenarioSnapshot scenario)
    {
        var teams = SeasonFrameworkService.LeagueTeams(scenario);
        var strategies = teams
            .Select((team, index) => BuildStrategy(scenario, team.OrganizationId, team.TeamName, index))
            .ToArray();
        var player = strategies.Single(item => item.OrganizationId == scenario.Organization.OrganizationId);
        var summary = player.Direction switch
        {
            TeamTradeDirection.Contender or TeamTradeDirection.Buyer => $"{scenario.Organization.Name} profiles as a buyer; staff should protect premium prospects but look for playoff help.",
            TeamTradeDirection.Seller or TeamTradeDirection.Rebuild => $"{scenario.Organization.Name} profiles as a seller; expiring veterans and budget pressure deserve review.",
            _ => $"{scenario.Organization.Name} profiles as neutral; discipline matters more than forcing a move."
        };
        var assessment = new BuyerSellerAssessment(scenario.CurrentDate, player, strategies, summary);
        assessment.Validate();
        return assessment;
    }

    public TradeDeadlineResult AdvanceDeadline(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);

        var window = GetWindow(scenario, registry.Rulebook);
        var previous = scenario.TradeDeadlineState;
        var assessment = AssessBuyerSeller(scenario);
        var rumors = previous?.Rumors.ToList() ?? new List<DeadlineRumor>();
        var inbox = new List<AlphaInboxItem>();
        var transactions = new List<LeagueTransaction>();
        var state = new TradeDeadlineState(
            window.DeadlineDate,
            scenario.CurrentDate,
            window.DaysRemaining,
            window.Status,
            previous?.HasExpandedTradeBlock ?? false,
            previous?.HasPostedClosed ?? false,
            previous?.MessageStatusesCreated ?? Array.Empty<TradeDeadlineStatus>(),
            rumors,
            previous?.LastTradeBlockUpdate,
            assessment);

        var current = scenario;
        if (window.Status != TradeDeadlineStatus.NotStarted && previous?.Status != window.Status)
        {
            AddStatusEvent(registry, current, window, inbox, transactions);
        }

        if (window.Status is TradeDeadlineStatus.Approaching or TradeDeadlineStatus.DeadlineWeek or TradeDeadlineStatus.DeadlineDay
            && !state.HasExpandedTradeBlock)
        {
            var expanded = ExpandTradeBlock(registry, current, state, assessment);
            current = expanded.ScenarioSnapshot;
            state = expanded.ScenarioSnapshot.TradeDeadlineState!;
            inbox.AddRange(expanded.InboxItems);
            transactions.AddRange(expanded.LeagueTransactions);
        }

        if (window.Status is TradeDeadlineStatus.DeadlineWeek or TradeDeadlineStatus.DeadlineDay
            && !state.MessageStatusesCreated.Contains(window.Status))
        {
            var messages = CreatePressureMessages(registry, current, window, assessment);
            inbox.AddRange(messages.InboxItems);
            transactions.AddRange(messages.LeagueTransactions);
            state = state with { MessageStatusesCreated = state.MessageStatusesCreated.Append(window.Status).Distinct().ToArray() };
        }

        if (window.Status == TradeDeadlineStatus.Closed && !state.HasPostedClosed)
        {
            QueueDeadlineEvent(registry, current, LegacyEventType.TradeDeadlineClosed, "Trade Deadline Closed", "The league trade deadline has passed. New trade proposals are locked.", LegacyEventSeverity.Warning);
            transactions.Add(DeadlineTransaction(current, "Trade Deadline Closed", "League", "Trade Deadline Closed. New trade proposals are locked."));
            inbox.Add(DeadlineInbox(current, LegacyEventType.TradeDeadlineClosed, "Trade Deadline Closed", "New trade proposals are locked. Accepted pre-deadline pending trades may still be approved.", LegacyEventSeverity.Warning));
            state = state with { HasPostedClosed = true };
        }

        state = state with
        {
            DeadlineDate = window.DeadlineDate,
            LastEvaluatedOn = current.CurrentDate,
            DaysRemaining = window.DaysRemaining,
            Status = window.Status,
            BuyerSellerAssessment = assessment
        };
        current = current with { TradeDeadlineState = state };
        current.Validate();

        var result = new TradeDeadlineResult(current, window, inbox.Take(5).ToArray(), transactions, window.Summary);
        result.Validate();
        return result;
    }

    private TradeDeadlineResult ExpandTradeBlock(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        TradeDeadlineState state,
        BuyerSellerAssessment assessment)
    {
        var existing = scenario.TradeBlock?.Entries.ToList() ?? new List<TradeBlockEntry>();
        var existingIds = existing.Select(item => item.PersonId).ToHashSet(StringComparer.Ordinal);
        var teams = assessment.LeagueStrategies
            .Where(item => item.OrganizationId != scenario.Organization.OrganizationId)
            .ToArray();
        var people = scenario.AlphaSnapshot.People.ToList();
        var added = new List<TradeBlockEntry>();

        for (var index = 0; index < DeadlineNames.Count; index++)
        {
            var personId = $"person-deadline-block-{index + 1:000}";
            if (existingIds.Contains(personId))
            {
                continue;
            }

            var name = DeadlineNames[index];
            var team = teams[index % teams.Length];
            var age = index % 3 == 0 ? 20 : index % 2 == 0 ? 19 : 18;
            var birthDate = scenario.CurrentDate.AddYears(-age).AddDays(-(index * 23 + 11));
            var person = NewGmScenarioBootstrapper.CreateScenarioPersonForGeneratedSystems(
                personId,
                name.First,
                name.Last,
                birthDate,
                name.Nationality,
                name.Birthplace,
                team.OrganizationId);
            people.Add(person);
            var position = DeadlinePosition(index);
            added.Add(new TradeBlockEntry(
                $"trade-block-entry:{personId}",
                personId,
                team.OrganizationId,
                team.TeamName,
                person.Identity.DisplayName,
                position,
                age,
                DeadlinePlayerType(position, index),
                DeadlineRole(position, index),
                index % 2 == 0 ? "Expiring contract" : "Signed but available before deadline",
                2_000m + index * 450m,
                team.Direction is TeamTradeDirection.Seller or TeamTradeDirection.Rebuild ? "pick or young prospect placeholder" : "roster player who helps now",
                team.Direction is TeamTradeDirection.Seller or TeamTradeDirection.Rebuild ? "seller listening before deadline" : "roster fit and deadline pressure",
                index < 3 ? TradeInterest.High : TradeInterest.Medium,
                52 + index * 4));
        }

        var update = new DeadlineTradeBlockUpdate(
            scenario.CurrentDate,
            added.Count,
            $"{added.Count} deadline-driven player(s) were added to the trade block.");
        var rumors = CreateRumors(scenario, added).ToArray();
        var nextBlock = new TradeBlock(
            $"trade-block:{scenario.Season.SeasonId}:{scenario.CurrentDate:yyyyMMdd}:deadline",
            scenario.CurrentDate,
            existing.Concat(added).ToArray());
        var alpha = scenario.AlphaSnapshot with { People = people.DistinctBy(person => person.PersonId).ToArray() };
        var nextState = state with
        {
            HasExpandedTradeBlock = true,
            Rumors = state.Rumors.Concat(rumors).ToArray(),
            LastTradeBlockUpdate = update
        };
        var next = scenario with
        {
            AlphaSnapshot = alpha,
            TradeBlock = nextBlock,
            TradeDeadlineState = nextState
        };

        QueueDeadlineEvent(registry, next, LegacyEventType.DeadlineTradeBlockExpanded, "Trade block expanded", update.Summary, LegacyEventSeverity.Notice);
        foreach (var rumor in rumors.Take(3))
        {
            QueueDeadlineEvent(registry, next, LegacyEventType.DeadlineRumorCreated, "Deadline rumor", rumor.Summary, LegacyEventSeverity.Notice, rumor.TeamName);
        }

        var inbox = added.Count == 0
            ? Array.Empty<AlphaInboxItem>()
            : new[]
            {
                DeadlineInbox(next, LegacyEventType.DeadlineTradeBlockExpanded, "Trade block expanded near deadline", update.Summary, LegacyEventSeverity.Notice)
            };
        var transactions = rumors.Take(3)
            .Select(rumor => DeadlineTransaction(next, rumor.TeamName, "Rumor", rumor.Summary))
            .Append(DeadlineTransaction(next, scenario.Organization.Name, "Trade block", update.Summary))
            .ToArray();
        return new TradeDeadlineResult(next, GetWindow(next, registry.Rulebook), inbox, transactions, update.Summary);
    }

    private DeadlineMessageResult CreatePressureMessages(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        TradeDeadlineWindow window,
        BuyerSellerAssessment assessment)
    {
        var items = new List<AlphaInboxItem>();
        var transactions = new List<LeagueTransaction>();
        var statusText = window.Status == TradeDeadlineStatus.DeadlineDay ? "today" : $"{window.DaysRemaining} day(s) away";
        var owner = assessment.PlayerTeamStrategy.Direction is TeamTradeDirection.Buyer or TeamTradeDirection.Contender
            ? "Owner expects a disciplined playoff push before the deadline."
            : "Owner wants the club to protect the future and avoid panic buying.";
        var coach = assessment.PlayerTeamStrategy.Direction is TeamTradeDirection.Buyer or TeamTradeDirection.Contender
            ? "Head coach believes defense depth would help the room."
            : "Head coach wants roster stability unless the return is clearly worth it.";
        var assistant = assessment.PlayerTeamStrategy.Direction is TeamTradeDirection.Seller or TeamTradeDirection.Rebuild
            ? "Assistant GM recommends shopping expiring contracts and protecting top prospects."
            : "Assistant GM recommends reviewing veteran defense and goaltending options.";

        QueueDeadlineEvent(registry, scenario, LegacyEventType.DeadlineOwnerExpectationCreated, "Owner deadline expectation", owner, LegacyEventSeverity.Warning);
        QueueDeadlineEvent(registry, scenario, LegacyEventType.DeadlineAssistantRecommendationCreated, "Assistant GM deadline recommendation", assistant, LegacyEventSeverity.Warning);
        items.Add(DeadlineInbox(scenario, LegacyEventType.DeadlineOwnerExpectationCreated, $"Owner deadline expectation: {statusText}", owner, LegacyEventSeverity.Warning));
        items.Add(DeadlineInbox(scenario, LegacyEventType.DeadlineAssistantRecommendationCreated, $"Assistant GM deadline note: {statusText}", assistant, LegacyEventSeverity.Warning));
        items.Add(DeadlineInbox(scenario, LegacyEventType.DeadlineBuyerSellerAssessmentCreated, $"Coach deadline note: {statusText}", coach, LegacyEventSeverity.Notice));
        transactions.Add(DeadlineTransaction(scenario, scenario.Organization.Name, "Deadline pressure", assistant));
        return new DeadlineMessageResult(items.Take(3).ToArray(), transactions);
    }

    private static DeadlineTeamStrategy BuildStrategy(NewGmScenarioSnapshot scenario, string organizationId, string teamName, int index)
    {
        var standing = scenario.Standings?.OrderedTeams()
            .Select((team, rank) => new { Team = team, Rank = rank + 1 })
            .FirstOrDefault(item => item.Team.OrganizationId == organizationId);
        var rank = standing?.Rank ?? index + 1;
        var teamCount = scenario.Standings?.Teams.Count ?? SeasonFrameworkService.LeagueTeams(scenario).Count;
        var avgAge = organizationId == scenario.Organization.OrganizationId && scenario.AlphaSnapshot.Roster.ActivePlayers.Count > 0
            ? scenario.AlphaSnapshot.Roster.ActivePlayers.Average(player => player.Age ?? 18)
            : 18.6 + index % 4;
        var expiring = organizationId == scenario.Organization.OrganizationId
            ? scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts).Count(contract => contract.Term.EndDate <= scenario.CurrentDate.AddDays(90))
            : index % 3;
        var direction = rank <= Math.Max(2, teamCount / 3)
            ? TeamTradeDirection.Contender
            : rank >= teamCount - 1
                ? TeamTradeDirection.Rebuild
                : expiring >= 4 || avgAge >= 19.4
                    ? TeamTradeDirection.Seller
                    : index % 2 == 0 ? TeamTradeDirection.Buyer : TeamTradeDirection.Neutral;
        var need = direction switch
        {
            TeamTradeDirection.Contender or TeamTradeDirection.Buyer => "seeking veterans, goalies, defense depth, and playoff help",
            TeamTradeDirection.Seller => "listening on veterans, expensive players, and expiring contracts",
            TeamTradeDirection.Rebuild => "prefers picks, prospects, and younger players",
            _ => "monitoring value without forcing a move"
        };
        var preference = direction switch
        {
            TeamTradeDirection.Contender => "may overpay slightly for immediate help",
            TeamTradeDirection.Buyer => "prefers roster help",
            TeamTradeDirection.Seller => "prefers picks and prospect rights",
            TeamTradeDirection.Rebuild => "prefers young players and picks",
            _ => "prefers balanced hockey trades"
        };
        var strategy = new DeadlineTeamStrategy(organizationId, teamName, direction, need, preference, Math.Clamp(35 + (teamCount - rank) * 6 + expiring * 2, 15, 92));
        strategy.Validate();
        return strategy;
    }

    private static IEnumerable<DeadlineRumor> CreateRumors(NewGmScenarioSnapshot scenario, IReadOnlyList<TradeBlockEntry> added)
    {
        foreach (var entry in added.Take(5))
        {
            var summary = entry.Position == RosterPosition.Goalie
                ? $"{entry.TeamName} is believed to be listening on goaltender {entry.Name}."
                : $"{entry.TeamName} may be shopping {entry.Age}-year-old {entry.Position} {entry.Name}.";
            yield return new DeadlineRumor($"deadline-rumor:{entry.PersonId}", scenario.CurrentDate, entry.TeamName, summary, entry.InterestLevel == TradeInterest.High ? DeadlineRumorConfidence.High : DeadlineRumorConfidence.Medium);
        }
    }

    private static void AddStatusEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        TradeDeadlineWindow window,
        List<AlphaInboxItem> inbox,
        List<LeagueTransaction> transactions)
    {
        var eventType = window.Status switch
        {
            TradeDeadlineStatus.Approaching => LegacyEventType.TradeDeadlineApproaching,
            TradeDeadlineStatus.DeadlineWeek => LegacyEventType.TradeDeadlineWeekStarted,
            TradeDeadlineStatus.DeadlineDay => LegacyEventType.TradeDeadlineDayStarted,
            TradeDeadlineStatus.Closed => LegacyEventType.TradeDeadlineClosed,
            _ => LegacyEventType.Generic
        };
        if (eventType == LegacyEventType.Generic)
        {
            return;
        }

        QueueDeadlineEvent(registry, scenario, eventType, window.Status.ToString(), window.Summary, window.Status == TradeDeadlineStatus.Approaching ? LegacyEventSeverity.Notice : LegacyEventSeverity.Warning);
        transactions.Add(DeadlineTransaction(scenario, scenario.Organization.Name, "Deadline status", window.Summary));
        if (window.Status is TradeDeadlineStatus.DeadlineWeek or TradeDeadlineStatus.DeadlineDay)
        {
            inbox.Add(DeadlineInbox(scenario, eventType, window.Status == TradeDeadlineStatus.DeadlineDay ? "Trade Deadline: Today" : "Trade Deadline Week", window.Summary, LegacyEventSeverity.Warning));
        }
    }

    private static void QueueDeadlineEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string description,
        LegacyEventSeverity severity,
        string? teamName = null)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 10, 0, 0, TimeSpan.Zero),
            eventType,
            severity,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["team_name"] = teamName ?? scenario.Organization.Name,
                ["person_name"] = "Trade Deadline",
                ["reason"] = description
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem DeadlineInbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, LegacyEventSeverity severity) =>
        new($"inbox:deadline:{Guid.NewGuid():N}", new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 10, 0, 0, TimeSpan.Zero), eventType, severity, title, summary, null);

    private static LeagueTransaction DeadlineTransaction(NewGmScenarioSnapshot scenario, string teamName, string subject, string description) =>
        new($"transaction:deadline:{Guid.NewGuid():N}", new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 10, 0, 0, TimeSpan.Zero), scenario.Organization.OrganizationId, teamName, null, subject, LeagueTransactionType.TradeDeadline, LeagueNewsCategory.Deadline, description);

    private static RosterPosition DeadlinePosition(int index) =>
        index switch
        {
            0 => RosterPosition.Defense,
            1 => RosterPosition.Goalie,
            2 => RosterPosition.Center,
            3 => RosterPosition.LeftWing,
            4 => RosterPosition.Defense,
            5 => RosterPosition.RightWing,
            6 => RosterPosition.Goalie,
            _ => RosterPosition.Defense
        };

    private static string DeadlinePlayerType(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => "Veteran goalie",
            RosterPosition.Defense => index % 2 == 0 ? "Veteran defense" : "Depth defense",
            RosterPosition.Center => "Two-way center",
            _ => "Deadline winger"
        };

    private static string DeadlineRole(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => "goalie insurance",
            RosterPosition.Defense => index % 2 == 0 ? "top-four rental option" : "third-pair depth",
            RosterPosition.Center => "middle-six support",
            _ => "scoring depth"
        };

    private sealed record DeadlineMessageResult(
        IReadOnlyList<AlphaInboxItem> InboxItems,
        IReadOnlyList<LeagueTransaction> LeagueTransactions);
}
