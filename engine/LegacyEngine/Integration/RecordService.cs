namespace LegacyEngine.Integration;

public sealed class RecordService
{
    public NewGmScenarioSnapshot EnsureRecordBook(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var records = GenerateRecords(scenario);
        if (records.Count == 0)
        {
            return scenario;
        }

        var book = scenario.RecordBook.Merge(records);
        var timeline = scenario.CareerTimeline;
        foreach (var record in records.Where(record => record.WasBrokenThisUpdate && record.HolderKind == "Player"))
        {
            timeline = timeline.Add(new CareerTimelineEntry(
                $"timeline:record:{record.RecordId}:{record.HolderId}:{record.SeasonYear}",
                CareerTimelineEntryType.Award,
                record.DateSet,
                record.SeasonYear,
                record.HolderId,
                record.OrganizationId,
                record.OrganizationName,
                $"{Readable(record.RecordType)} record",
                record.Summary,
                null,
                HistoryImportance.Major));
        }

        return scenario with
        {
            RecordBook = book,
            CareerTimeline = timeline
        };
    }

    public IReadOnlyList<RecordEntry> GenerateRecords(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var records = new List<RecordEntry>();

        AddSkaterRecord(scenario, records, RecordType.Goals, RecordScope.SingleSeason, stat => stat.Goals);
        AddSkaterRecord(scenario, records, RecordType.Assists, RecordScope.SingleSeason, stat => stat.Assists);
        AddSkaterRecord(scenario, records, RecordType.Points, RecordScope.SingleSeason, stat => stat.Points);
        AddSkaterRecord(scenario, records, RecordType.GamesPlayed, RecordScope.Career, stat => stat.GamesPlayed);
        AddGoalieRecord(scenario, records, RecordType.GoalieWins, RecordScope.SingleSeason, stat => stat.Wins);
        AddGoalieRecord(scenario, records, RecordType.Shutouts, RecordScope.SingleSeason, stat => stat.Shutouts);
        AddTeamRecord(scenario, records, RecordType.TeamWins, RecordScope.SingleSeason, standing => standing.Wins);
        AddChampionshipRecord(scenario, records);
        AddPlayoffRecord(scenario, records);

        return records
            .Where(record => record.Value > 0)
            .GroupBy(record => record.RecordId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    public IReadOnlyList<string> BuildPlayerDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var records = scenario.RecordBook.ForPerson(personId);
        if (records.Count == 0)
        {
            return new[] { "No records held yet." };
        }

        return records
            .Take(8)
            .Select(record => $"{Readable(record.Scope)} {Readable(record.RecordType)}: {record.Value} - {record.Summary}")
            .ToArray();
    }

    private static void AddSkaterRecord(
        NewGmScenarioSnapshot scenario,
        List<RecordEntry> records,
        RecordType type,
        RecordScope scope,
        Func<PlayerSeasonStatLine, int> value)
    {
        var leader = scenario.PlayerStats
            .OrderByDescending(value)
            .ThenBy(stat => stat.PlayerName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (leader is null)
        {
            return;
        }

        records.Add(CreateRecord(
            scenario,
            type,
            scope,
            leader.PersonId,
            leader.PlayerName,
            "Player",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            value(leader),
            $"{leader.PlayerName} set the tracked {Readable(scope).ToLowerInvariant()} {Readable(type).ToLowerInvariant()} mark with {value(leader)}."));
    }

    private static void AddGoalieRecord(
        NewGmScenarioSnapshot scenario,
        List<RecordEntry> records,
        RecordType type,
        RecordScope scope,
        Func<GoalieSeasonStatLine, int> value)
    {
        var leader = scenario.GoalieStats
            .OrderByDescending(value)
            .ThenBy(stat => stat.PlayerName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (leader is null)
        {
            return;
        }

        records.Add(CreateRecord(
            scenario,
            type,
            scope,
            leader.PersonId,
            leader.PlayerName,
            "Player",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            value(leader),
            $"{leader.PlayerName} set the tracked goalie {Readable(type).ToLowerInvariant()} mark with {value(leader)}."));
    }

    private static void AddTeamRecord(
        NewGmScenarioSnapshot scenario,
        List<RecordEntry> records,
        RecordType type,
        RecordScope scope,
        Func<TeamStanding, int> value)
    {
        var leader = scenario.Standings?.Teams
            .OrderByDescending(value)
            .ThenBy(team => team.TeamName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (leader is null)
        {
            return;
        }

        records.Add(CreateRecord(
            scenario,
            type,
            scope,
            leader.OrganizationId,
            leader.TeamName,
            "Team",
            leader.OrganizationId,
            leader.TeamName,
            value(leader),
            $"{leader.TeamName} set the tracked team wins mark with {value(leader)}."));
    }

    private static void AddChampionshipRecord(NewGmScenarioSnapshot scenario, List<RecordEntry> records)
    {
        var champion = scenario.Playoffs.Bracket?.ChampionTeamName;
        if (string.IsNullOrWhiteSpace(champion))
        {
            champion = scenario.SeasonRollover.SeasonArchives.LastOrDefault()?.ChampionTeamName;
        }

        if (string.IsNullOrWhiteSpace(champion))
        {
            return;
        }

        var orgId = scenario.LeagueProfile.Teams.FirstOrDefault(team => team.TeamName == champion)?.OrganizationId
            ?? scenario.Organization.OrganizationId;
        var existing = scenario.RecordBook.Records
            .Where(record => record.RecordType == RecordType.Championships && record.HolderId == orgId)
            .Sum(record => record.Value);
        var value = Math.Max(1, existing + 1);
        records.Add(CreateRecord(
            scenario,
            RecordType.Championships,
            RecordScope.Team,
            orgId,
            champion,
            "Team",
            orgId,
            champion,
            value,
            $"{champion} has {value} tracked championship(s)."));
    }

    private static void AddPlayoffRecord(NewGmScenarioSnapshot scenario, List<RecordEntry> records)
    {
        var leader = scenario.Playoffs.PlayoffSkaterStats
            .OrderByDescending(stat => stat.Points)
            .ThenBy(stat => stat.PlayerName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (leader is null)
        {
            return;
        }

        records.Add(CreateRecord(
            scenario,
            RecordType.PlayoffPoints,
            RecordScope.Playoff,
            leader.PersonId,
            leader.PlayerName,
            "Player",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            leader.Points,
            $"{leader.PlayerName} set the tracked playoff points mark with {leader.Points}."));
    }

    private static RecordEntry CreateRecord(
        NewGmScenarioSnapshot scenario,
        RecordType type,
        RecordScope scope,
        string holderId,
        string holderName,
        string holderKind,
        string? organizationId,
        string? organizationName,
        int value,
        string summary)
    {
        var recordId = $"record:{scenario.LeagueProfile.Identity.LeagueId}:{scope}:{type}";
        var previous = scenario.RecordBook.Records
            .Where(record => string.Equals(record.RecordId, recordId, StringComparison.Ordinal))
            .OrderByDescending(record => record.Value)
            .FirstOrDefault();
        var broken = previous is null || value > previous.Value;
        var record = new RecordEntry(
            recordId,
            type,
            scope,
            scenario.Season.Year,
            scenario.CurrentDate,
            holderId,
            holderName,
            holderKind,
            organizationId,
            organizationName,
            Math.Max(0, value),
            broken && previous is not null
                ? $"{summary} Previous mark: {previous.Value} by {previous.HolderName}."
                : summary,
            broken);
        record.Validate();
        return record;
    }

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }
}
