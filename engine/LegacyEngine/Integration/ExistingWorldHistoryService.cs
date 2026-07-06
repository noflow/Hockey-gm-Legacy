using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class ExistingWorldHistoryService
{
    public ExistingWorldHistorySeed CreateHistory(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var priorStats = BuildPriorStats(scenario).ToArray();
        var careerStats = BuildCareerStats(scenario, priorStats).ToArray();
        var teamHistory = BuildTeamHistory(scenario).ToArray();
        var timelines = BuildTimelines(scenario, priorStats, teamHistory).ToArray();
        var organizationHistory = BuildOrganizationHistory(scenario);
        var draftHistory = BuildDraftHistory(scenario).ToArray();

        var seed = new ExistingWorldHistorySeed(
            priorStats,
            careerStats,
            teamHistory,
            timelines,
            organizationHistory,
            draftHistory);
        seed.Validate();
        return seed;
    }

    private static IEnumerable<PriorSeasonStatLine> BuildPriorStats(NewGmScenarioSnapshot scenario)
    {
        foreach (var player in scenario.AlphaSnapshot.Roster.Players)
        {
            var person = FindPerson(scenario, player.PersonId);
            if (person is null)
            {
                continue;
            }

            yield return StatLineFor(
                person,
                player.Position,
                scenario.Season.Year - 1,
                TeamForRosterIndex(scenario, player.PersonId),
                "Prairie Junior League",
                scenario.CurrentDate);
        }

        foreach (var entry in scenario.AlphaSnapshot.DraftBoard.Entries.Take(60))
        {
            var person = FindPerson(scenario, entry.ProspectPersonId);
            if (person is null || entry.Bio is null)
            {
                continue;
            }

            yield return StatLineFor(
                person,
                entry.Bio.Position,
                scenario.Season.Year - 1,
                entry.Bio.CurrentTeam,
                entry.Bio.League,
                scenario.CurrentDate,
                youthLevel: true);
        }
    }

    private static IEnumerable<CareerStatSummary> BuildCareerStats(NewGmScenarioSnapshot scenario, IReadOnlyList<PriorSeasonStatLine> priorStats)
    {
        foreach (var stat in priorStats)
        {
            var age = FindPerson(scenario, stat.PersonId)?.CalculateAge(scenario.CurrentDate) ?? 16;
            var seasons = Math.Clamp(age - 15, 1, age >= 20 ? 4 : 2);
            var multiplier = seasons == 1 ? 1 : seasons + 1;

            yield return new CareerStatSummary(
                stat.PersonId,
                stat.PlayerName,
                stat.Position,
                seasons,
                stat.GamesPlayed * multiplier / 2,
                stat.Goals * multiplier / 2,
                stat.Assists * multiplier / 2,
                stat.PenaltyMinutes * multiplier / 2,
                stat.Wins * multiplier / 2,
                stat.Losses * multiplier / 2,
                stat.IsGoalie ? Math.Max(0, seasons - 1) : 0,
                stat.LeagueName,
                CareerSummaryText(stat, seasons, age));
        }
    }

    private static IEnumerable<PlayerTeamHistory> BuildTeamHistory(NewGmScenarioSnapshot scenario)
    {
        foreach (var player in scenario.AlphaSnapshot.Roster.Players)
        {
            var person = FindPerson(scenario, player.PersonId);
            if (person is null)
            {
                continue;
            }

            var age = person.CalculateAge(scenario.CurrentDate);
            yield return new PlayerTeamHistory(
                person.PersonId,
                person.Identity.DisplayName,
                scenario.Organization.Name,
                "Prairie Junior League",
                Math.Max(scenario.Season.Year - Math.Clamp(age - 16, 1, 4), scenario.Season.Year - 4),
                scenario.Season.Year - 1,
                RoleFor(player.Position, age),
                age >= 20
                    ? "Veteran player returning with leadership expectations and a contract decision pending."
                    : "Returning player already familiar with the organization and staff expectations.");
        }

        foreach (var entry in scenario.AlphaSnapshot.DraftBoard.Entries.Take(60))
        {
            var person = FindPerson(scenario, entry.ProspectPersonId);
            if (person is null || entry.Bio is null)
            {
                continue;
            }

            yield return new PlayerTeamHistory(
                person.PersonId,
                person.Identity.DisplayName,
                entry.Bio.CurrentTeam,
                entry.Bio.League,
                scenario.Season.Year - 1,
                scenario.Season.Year - 1,
                RoleFor(entry.Bio.Position, person.CalculateAge(scenario.CurrentDate)),
                "Prior youth/junior production is visible to the GM without exposing hidden ratings.");
        }
    }

    private static IEnumerable<PlayerCareerTimeline> BuildTimelines(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<PriorSeasonStatLine> priorStats,
        IReadOnlyList<PlayerTeamHistory> teamHistory)
    {
        foreach (var person in scenario.AlphaSnapshot.Players.Concat(scenario.AlphaSnapshot.People).DistinctBy(person => person.PersonId))
        {
            var entries = new List<string>();
            var stat = priorStats.FirstOrDefault(item => item.PersonId == person.PersonId);
            var history = teamHistory.FirstOrDefault(item => item.PersonId == person.PersonId);
            if (history is not null)
            {
                entries.Add($"{history.FromSeasonYear}-{history.ToSeasonYear}: {history.Role} with {history.TeamName} ({history.LeagueName}).");
            }

            if (stat is not null)
            {
                entries.Add(stat.SummaryText);
            }

            foreach (var role in person.Roles.OrderBy(role => role.StartDate).TakeLast(2))
            {
                entries.Add($"{role.StartDate:yyyy-MM-dd}: {role.Title} role started.");
            }

            if (entries.Count > 0)
            {
                yield return new PlayerCareerTimeline(person.PersonId, person.Identity.DisplayName, entries);
            }
        }

        foreach (var staff in scenario.StaffMembers)
        {
            var person = FindPerson(scenario, staff.PersonId);
            if (person is null)
            {
                continue;
            }

            yield return new PlayerCareerTimeline(
                person.PersonId,
                person.Identity.DisplayName,
                new[]
                {
                    $"{scenario.Season.Year - Math.Clamp(staff.PerformanceHistory.Count + 3, 1, 8)}: Began tracked hockey operations work before the current GM arrived.",
                    $"{staff.CurrentRole}: {StaffRoles.Title(staff.CurrentRole)} with {scenario.Organization.Name}.",
                    $"Reputation: local {person.Reputation.Local}, league {person.Reputation.League}; history is role-based and does not expose hidden ratings."
                });
        }
    }

    private static OrganizationHistorySnapshot BuildOrganizationHistory(NewGmScenarioSnapshot scenario) =>
        new(
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.Season.Year - 1,
            Wins: 31,
            Losses: 25,
            OvertimeLosses: 4,
            Points: 66,
            GoalsFor: 214,
            GoalsAgainst: 205,
            PlayoffResult: "Lost in second round",
            PreviousLeagueChampion: "Regina Plainsmen",
            Summary: $"{scenario.Organization.Name} finished as a competitive but uneven club last season. Ownership wants development without losing the room.");

    private static IEnumerable<DraftHistoryRecord> BuildDraftHistory(NewGmScenarioSnapshot scenario)
    {
        var previousPicks = scenario.AlphaSnapshot.Roster.Players
            .Take(8)
            .Select((player, index) => (Player: player, Index: index));

        foreach (var item in previousPicks)
        {
            var person = FindPerson(scenario, item.Player.PersonId);
            if (person is null)
            {
                continue;
            }

            yield return new DraftHistoryRecord(
                scenario.Season.Year - 1 - (item.Index % 3),
                (item.Index % 4) + 1,
                item.Index + 3,
                person.PersonId,
                person.Identity.DisplayName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                item.Player.Age is >= 20
                    ? "Developed into an overage roster leader."
                    : "Remains in the organization as a developing roster player.");
        }
    }

    private static PriorSeasonStatLine StatLineFor(
        Person person,
        RosterPosition position,
        int seasonYear,
        string teamName,
        string leagueName,
        DateOnly currentDate,
        bool youthLevel = false)
    {
        var seed = Math.Abs(HashCode.Combine(person.PersonId, seasonYear, teamName));
        var age = person.CalculateAge(currentDate);
        var games = youthLevel ? 28 + (seed % 18) : 42 + (seed % 18);
        if (position == RosterPosition.Goalie)
        {
            var wins = Math.Min(games, 11 + (seed % Math.Max(1, games - 10)));
            var losses = Math.Max(0, games - wins - (seed % 4));
            return new PriorSeasonStatLine(
                person.PersonId,
                person.Identity.DisplayName,
                seasonYear,
                teamName,
                leagueName,
                position,
                games,
                Wins: wins,
                Losses: losses,
                SavePercentage: 0.887m + (seed % 29) / 1000m,
                GoalsAgainstAverage: Math.Round(2.55m + (seed % 90) / 100m, 2));
        }

        var roleBoost = position == RosterPosition.Center ? 4 : position == RosterPosition.Defense ? -5 : 2;
        var goals = Math.Max(1, (seed % 18) + (age >= 19 ? 6 : 2) + roleBoost);
        var assists = Math.Max(2, (seed % 22) + (position == RosterPosition.Defense ? 12 : 8));
        return new PriorSeasonStatLine(
            person.PersonId,
            person.Identity.DisplayName,
            seasonYear,
            teamName,
            leagueName,
            position,
            games,
            Goals: goals,
            Assists: assists,
            PlusMinus: (seed % 23) - 8,
            PenaltyMinutes: 8 + (seed % 52));
    }

    private static string TeamForRosterIndex(NewGmScenarioSnapshot scenario, string personId)
    {
        var index = scenario.AlphaSnapshot.Roster.Players.ToList().FindIndex(player => player.PersonId == personId);
        return index < 8 ? scenario.Organization.Name : new[] { "Moose Jaw U18", "Regina Valley", "Saskatoon North", "Brandon Academy" }[Math.Abs(index) % 4];
    }

    private static string CareerSummaryText(PriorSeasonStatLine stat, int seasons, int age) =>
        stat.IsGoalie
            ? $"{age}-year-old {stat.Position} with {seasons} tracked season(s); last year was {stat.Wins}-{stat.Losses} with a {stat.SavePercentage:0.000} save percentage."
            : $"{age}-year-old {stat.Position} with {seasons} tracked season(s); last year produced {stat.Goals}-{stat.Assists}-{stat.Points}.";

    private static string RoleFor(RosterPosition position, int age) =>
        position switch
        {
            RosterPosition.Goalie => age >= 19 ? "returning goalie" : "developing goalie",
            RosterPosition.Defense => age >= 19 ? "veteran defenseman" : "developing defenseman",
            RosterPosition.Center => age >= 19 ? "two-way center" : "developing center",
            RosterPosition.LeftWing or RosterPosition.RightWing => age >= 19 ? "veteran winger" : "developing winger",
            _ => "tracked player"
        };

    private static Person? FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId);
}

public sealed record ExistingWorldHistorySeed(
    IReadOnlyList<PriorSeasonStatLine> PriorSeasonStats,
    IReadOnlyList<CareerStatSummary> CareerStatSummaries,
    IReadOnlyList<PlayerTeamHistory> PlayerTeamHistories,
    IReadOnlyList<PlayerCareerTimeline> PlayerCareerTimelines,
    OrganizationHistorySnapshot OrganizationHistory,
    IReadOnlyList<DraftHistoryRecord> DraftHistory)
{
    public void Validate()
    {
        foreach (var stat in PriorSeasonStats)
        {
            stat.Validate();
        }

        foreach (var summary in CareerStatSummaries)
        {
            summary.Validate();
        }

        foreach (var history in PlayerTeamHistories)
        {
            history.Validate();
        }

        foreach (var timeline in PlayerCareerTimelines)
        {
            timeline.Validate();
        }

        OrganizationHistory.Validate();

        foreach (var record in DraftHistory)
        {
            record.Validate();
        }
    }
}
