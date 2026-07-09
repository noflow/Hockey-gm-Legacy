using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class AwardService
{
    public NewGmScenarioSnapshot EnsureAwards(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var awards = GenerateAwards(scenario);
        if (awards.Count == 0)
        {
            return scenario;
        }

        var history = scenario.AwardHistory.Merge(awards);
        var timeline = scenario.CareerTimeline;
        foreach (var award in awards.Where(award => award.Winner.RecipientKind is "Player" or "Staff" or "GM"))
        {
            timeline = timeline.Add(new CareerTimelineEntry(
                $"timeline:award:{award.AwardId}:{award.Winner.RecipientId}",
                CareerTimelineEntryType.Award,
                award.AwardDate,
                award.SeasonYear,
                award.Winner.RecipientId,
                award.Winner.OrganizationId,
                award.Winner.OrganizationName,
                $"{Readable(award.AwardType)} winner",
                award.Reasoning,
                null,
                award.IsMajor ? HistoryImportance.Major : HistoryImportance.Important));
        }

        return scenario with
        {
            AwardHistory = history,
            CareerTimeline = timeline
        };
    }

    public IReadOnlyList<Award> GenerateAwards(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var awards = new List<Award>();
        var date = AwardDate(scenario);
        if (!HasAwardEvidence(scenario))
        {
            return awards;
        }

        AddSkaterAward(scenario, awards, AwardType.Mvp, AwardCategory.League, date, stat => (stat.Points * 3) + stat.Goals + Math.Max(0, stat.PlusMinus), "season scoring, goal impact, and plus/minus");
        AddSkaterAward(scenario, awards, AwardType.TopScorer, AwardCategory.League, date, stat => stat.Points, "league points leader");
        AddSkaterAward(scenario, awards, AwardType.TeamMvp, AwardCategory.Team, date, stat => (stat.Points * 2) + stat.Goals + stat.GamesPlayed, "team scoring and reliability");
        AddSkaterAward(scenario, awards, AwardType.BestDefenseman, AwardCategory.League, date, stat => DefenseScore(scenario, stat), "defense position context, plus/minus, and scoring");
        AddRookieAward(scenario, awards, date);
        AddMostImprovedAward(scenario, awards, date);
        AddGoalieAward(scenario, awards, date);
        AddPlayoffMvp(scenario, awards, date);
        AddStaffAward(scenario, awards, AwardType.CoachOfTheYear, AwardCategory.Staff, StaffRole.HeadCoach, date, "team success, staff role, and season context");
        AddStaffAward(scenario, awards, AwardType.TopScout, AwardCategory.Scouting, StaffRole.HeadScout, date, "scouting role, draft context, and prospect story importance");
        AddStaffAward(scenario, awards, AwardType.DevelopmentStaffAward, AwardCategory.Development, StaffRole.DevelopmentCoach, date, "development role and player progress context");
        AddGmAward(scenario, awards, date);

        return awards
            .GroupBy(award => award.AwardId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    public IReadOnlyList<string> BuildPlayerDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var awards = scenario.AwardHistory.ForPerson(personId);
        var records = scenario.RecordBook.ForPerson(personId);
        var lines = new List<string>();
        if (awards.Count == 0)
        {
            lines.Add("No award wins or finalist history yet.");
        }
        else
        {
            foreach (var award in awards.Take(8))
            {
                var finalist = award.Winner.RecipientId == personId ? "Winner" : "Finalist";
                lines.Add($"{award.SeasonYear} | {Readable(award.AwardType)} | {finalist} - {award.Reasoning}");
            }
        }

        if (records.Count > 0)
        {
            lines.Add("Records held:");
            lines.AddRange(records.Take(6).Select(record => $"  {Readable(record.Scope)} {Readable(record.RecordType)}: {record.Value} - {record.Summary}"));
        }

        return lines;
    }

    private static void AddSkaterAward(
        NewGmScenarioSnapshot scenario,
        List<Award> awards,
        AwardType type,
        AwardCategory category,
        DateOnly date,
        Func<PlayerSeasonStatLine, int> score,
        string reason)
    {
        var candidates = scenario.PlayerStats
            .Where(stat => stat.GamesPlayed > 0 || stat.Points > 0)
            .OrderByDescending(score)
            .ThenByDescending(stat => stat.Goals)
            .ThenBy(stat => stat.PlayerName, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        var winner = candidates.FirstOrDefault();
        if (winner is null)
        {
            return;
        }

        awards.Add(CreatePlayerAward(scenario, type, category, date, winner, score(winner), reason, candidates, type is AwardType.Mvp or AwardType.TopScorer));
    }

    private static void AddRookieAward(NewGmScenarioSnapshot scenario, List<Award> awards, DateOnly date)
    {
        var candidates = scenario.PlayerStats
            .Where(stat => Age(scenario, stat.PersonId) <= 20)
            .OrderByDescending(stat => stat.Points)
            .ThenByDescending(stat => stat.Goals)
            .ThenBy(stat => stat.PlayerName, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        var winner = candidates.FirstOrDefault();
        if (winner is null)
        {
            return;
        }

        awards.Add(CreatePlayerAward(scenario, AwardType.RookieOfTheYear, AwardCategory.Rookie, date, winner, winner.Points, "rookie-age production and role impact", candidates, true));
    }

    private static void AddMostImprovedAward(NewGmScenarioSnapshot scenario, List<Award> awards, DateOnly date)
    {
        var candidates = scenario.PlayerStats
            .Select(stat =>
            {
                var prior = scenario.PriorSeasonStats.FirstOrDefault(item => item.PersonId == stat.PersonId);
                var priorPoints = prior?.Points ?? 0;
                return (Stat: stat, Improvement: stat.Points - priorPoints);
            })
            .Where(item => item.Improvement > 0)
            .OrderByDescending(item => item.Improvement)
            .ThenByDescending(item => item.Stat.Points)
            .Take(3)
            .ToArray();
        var winner = candidates.FirstOrDefault();
        if (winner.Stat is null)
        {
            return;
        }

        awards.Add(CreatePlayerAward(scenario, AwardType.MostImproved, AwardCategory.Development, date, winner.Stat, winner.Improvement, "year-over-year public stat improvement", candidates.Select(item => item.Stat).ToArray(), false));
    }

    private static void AddGoalieAward(NewGmScenarioSnapshot scenario, List<Award> awards, DateOnly date)
    {
        var candidates = scenario.GoalieStats
            .Where(stat => stat.GamesPlayed > 0 || stat.Wins > 0)
            .OrderByDescending(stat => (stat.Wins * 5) + stat.Shutouts + (int)(stat.SavePercentage * 100))
            .ThenBy(stat => stat.GoalsAgainstAverage)
            .Take(3)
            .ToArray();
        var winner = candidates.FirstOrDefault();
        if (winner is null)
        {
            return;
        }

        awards.Add(CreateAward(
            scenario,
            AwardType.BestGoalie,
            AwardCategory.League,
            date,
            new AwardRecipient(winner.PersonId, winner.PlayerName, "Player", scenario.Organization.OrganizationId, scenario.Organization.Name),
            candidates.Select(stat => new AwardRecipient(stat.PersonId, stat.PlayerName, "Player", scenario.Organization.OrganizationId, scenario.Organization.Name)).ToArray(),
            (winner.Wins * 5) + winner.Shutouts + (int)(winner.SavePercentage * 100),
            $"{winner.PlayerName} won Best Goalie using wins, save percentage, shutouts, and goals-against context.",
            true));
    }

    private static void AddPlayoffMvp(NewGmScenarioSnapshot scenario, List<Award> awards, DateOnly date)
    {
        var playoffStats = scenario.Playoffs.PlayoffSkaterStats;
        if (playoffStats.Count == 0)
        {
            return;
        }

        var candidates = playoffStats.OrderByDescending(stat => stat.Points).ThenByDescending(stat => stat.Goals).Take(3).ToArray();
        var winner = candidates.FirstOrDefault();
        if (winner is null)
        {
            return;
        }

        awards.Add(CreatePlayerAward(scenario, AwardType.PlayoffMvp, AwardCategory.Playoff, date, winner, winner.Points, "playoff scoring and championship-run impact", candidates, true));
    }

    private static void AddStaffAward(NewGmScenarioSnapshot scenario, List<Award> awards, AwardType type, AwardCategory category, StaffRole role, DateOnly date, string reason)
    {
        var staff = scenario.StaffMembers.FirstOrDefault(member => member.CurrentRole == role);
        if (staff is null)
        {
            return;
        }

        var standing = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.OrganizationId);
        var score = (standing?.Points ?? 0) + (staff.Profile.Reputation / 2);
        var staffName = StaffName(scenario, staff.PersonId);
        awards.Add(CreateAward(
            scenario,
            type,
            category,
            date,
            new AwardRecipient(staff.PersonId, staffName, "Staff", scenario.Organization.OrganizationId, scenario.Organization.Name),
            Array.Empty<AwardRecipient>(),
            score,
            $"{staffName} received {Readable(type)} recognition using {reason}.",
            type is AwardType.CoachOfTheYear));
    }

    private static void AddGmAward(NewGmScenarioSnapshot scenario, List<Award> awards, DateOnly date)
    {
        var gm = scenario.GeneralManagerProfile.Person;
        var standing = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.OrganizationId);
        var score = (standing?.Points ?? 0) + scenario.ProspectRights.Count + scenario.DraftPickHistory.Count;
        awards.Add(CreateAward(
            scenario,
            AwardType.GmOfTheYear,
            AwardCategory.GM,
            date,
            new AwardRecipient(gm.PersonId, gm.Identity.DisplayName, "GM", scenario.Organization.OrganizationId, scenario.Organization.Name),
            Array.Empty<AwardRecipient>(),
            score,
            $"{gm.Identity.DisplayName} received GM of the Year consideration from team results, prospect depth, and roster-building context.",
            true));
    }

    private static Award CreatePlayerAward(
        NewGmScenarioSnapshot scenario,
        AwardType type,
        AwardCategory category,
        DateOnly date,
        PlayerSeasonStatLine winner,
        int score,
        string reason,
        IReadOnlyList<PlayerSeasonStatLine> candidates,
        bool major)
    {
        return CreateAward(
            scenario,
            type,
            category,
            date,
            new AwardRecipient(winner.PersonId, winner.PlayerName, "Player", scenario.Organization.OrganizationId, scenario.Organization.Name),
            candidates.Select(stat => new AwardRecipient(stat.PersonId, stat.PlayerName, "Player", scenario.Organization.OrganizationId, scenario.Organization.Name)).ToArray(),
            score,
            $"{winner.PlayerName} won {Readable(type)} based on {reason}. Public line: {winner.Goals} goals, {winner.Assists} assists, {winner.Points} points.",
            major);
    }

    private static Award CreateAward(
        NewGmScenarioSnapshot scenario,
        AwardType type,
        AwardCategory category,
        DateOnly date,
        AwardRecipient winner,
        IReadOnlyList<AwardRecipient> finalists,
        int score,
        string reasoning,
        bool major)
    {
        var award = new Award(
            $"award:{scenario.Season.Year}:{type}:{winner.RecipientId}",
            scenario.Season.Year,
            date,
            type,
            category,
            winner,
            finalists.DistinctBy(finalist => finalist.RecipientId).ToArray(),
            score,
            reasoning,
            major);
        award.Validate();
        return award;
    }

    private static int DefenseScore(NewGmScenarioSnapshot scenario, PlayerSeasonStatLine stat)
    {
        var position = scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == stat.PersonId)?.Position;
        var positionBonus = position == RosterPosition.Defense ? 50 : 0;
        return positionBonus + stat.Points + Math.Max(0, stat.PlusMinus);
    }

    private static int Age(NewGmScenarioSnapshot scenario, string personId)
    {
        var person = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId);
        return person?.CalculateAge(scenario.CurrentDate) ?? 99;
    }

    private static string StaffName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private static DateOnly AwardDate(NewGmScenarioSnapshot scenario) =>
        scenario.CurrentDate >= scenario.DraftDate ? scenario.CurrentDate : scenario.DraftDate.AddDays(-3);

    private static bool HasAwardEvidence(NewGmScenarioSnapshot scenario) =>
        scenario.PlayerStats.Any(stat => stat.GamesPlayed > 0 || stat.Points > 0)
        || scenario.GoalieStats.Any(stat => stat.GamesPlayed > 0 || stat.Wins > 0)
        || scenario.Standings?.Teams.Any(team => team.GamesPlayed > 0) == true
        || scenario.Playoffs.PlayoffSkaterStats.Any(stat => stat.GamesPlayed > 0 || stat.Points > 0);

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }
}
