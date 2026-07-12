using LegacyEngine.Contracts;
using LegacyEngine.People;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

/// <summary>Builds lightweight league-facing roster context without creating a second full player simulation for every club.</summary>
public sealed class ExistingWorkforceGenerator
{
    public NewGmScenarioSnapshot EnsureWorkforce(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.LeagueProfile.Experience != LeagueExperience.Nhl || scenario.LeagueWorkforce is not null)
        {
            return scenario;
        }

        var teams = scenario.LeagueProfile.Teams
            .Select((team, index) => BuildTeam(scenario, team, index))
            .ToArray();
        var players = teams.SelectMany(team => team.Players).ToArray();
        var workforce = new LeagueWorkforceProfile(
            scenario.LeagueProfile.Identity.LeagueId,
            scenario.Season.Year,
            AgeDistribution(players.Select(player => player.Age)),
            StageDistribution(players.Select(player => player.CareerStage)),
            teams,
            players,
            $"{scenario.LeagueProfile.Identity.Name} begins with {players.Length} tracked NHL workforce records across rookies, prime players, veterans, and late-career players.");
        workforce.Validate();

        var updated = scenario with { LeagueWorkforce = workforce };
        updated.Validate();
        return updated;
    }

    private static TeamWorkforceProfile BuildTeam(NewGmScenarioSnapshot scenario, TeamSelectionOption team, int teamIndex)
    {
        var selectedTeam = team.OrganizationId == scenario.Organization.OrganizationId;
        var players = selectedTeam
            ? SelectedTeamPlayers(scenario, team).ToArray()
            : SyntheticTeamPlayers(scenario, team, teamIndex).ToArray();
        var profile = new TeamWorkforceProfile(
            team.OrganizationId,
            team.TeamName,
            team.DisplayCurrentStrategy,
            AgeDistribution(players.Select(player => player.Age)),
            StageDistribution(players.Select(player => player.CareerStage)),
            players,
            $"{team.TeamName} has a {ReadableStrategy(team.DisplayCurrentStrategy)} age curve with {players.Count(player => player.Age >= 30)} veteran roster players and {players.Count(player => player.ContractYearsRemaining <= 1)} upcoming contract decisions.");
        profile.Validate();
        return profile;
    }

    private static IEnumerable<WorkforcePlayerRecord> SelectedTeamPlayers(NewGmScenarioSnapshot scenario, TeamSelectionOption team)
    {
        foreach (var player in scenario.AlphaSnapshot.Roster.Players)
        {
            var person = scenario.AlphaSnapshot.People.FirstOrDefault(item => item.PersonId == player.PersonId);
            var age = person?.CalculateAge(scenario.CurrentDate) ?? player.Age ?? 25;
            var contract = scenario.Contracts
                .Where(item => item.PersonId == player.PersonId)
                .OrderByDescending(item => item.Term.EndDate)
                .FirstOrDefault();
            var rating = scenario.PlayerRatings.FirstOrDefault(item => item.PersonId == player.PersonId);
            yield return Record(
                player.PersonId,
                person?.Identity.DisplayName ?? player.PersonId,
                team,
                player.Position,
                age,
                rating?.Overall.Midpoint ?? EstimateOverall(age, player.Position, 0),
                rating?.Potential.Midpoint ?? EstimatePotential(age, player.Position, 0),
                contract,
                scenario);
        }
    }

    private static IEnumerable<WorkforcePlayerRecord> SyntheticTeamPlayers(NewGmScenarioSnapshot scenario, TeamSelectionOption team, int teamIndex)
    {
        for (var rosterIndex = 0; rosterIndex < 23; rosterIndex++)
        {
            var position = PositionFor(rosterIndex);
            var age = AgeFor(team.DisplayCurrentStrategy, teamIndex, rosterIndex);
            var years = YearsRemaining(teamIndex, rosterIndex);
            var expiry = ContractExpiryCalendar.CommonExpiryDate(scenario.Season.Year + years, scenario.Season.Settings);
            var stage = StageFor(age, position);
            var rights = years <= 1
                ? age >= 27 ? FreeAgentRightsStatus.PendingUfa : FreeAgentRightsStatus.PendingRfa
                : FreeAgentRightsStatus.UnderContract;
            var overall = EstimateOverall(age, position, teamIndex + rosterIndex);
            var potential = EstimatePotential(age, position, teamIndex + rosterIndex);
            yield return new WorkforcePlayerRecord(
                $"league-workforce:{team.OrganizationId}:{rosterIndex + 1:00}",
                $"{team.TeamName} {PositionShort(position)} {rosterIndex + 1}",
                team.OrganizationId,
                team.TeamName,
                position,
                age,
                stage,
                overall,
                potential,
                years,
                expiry,
                rights,
                CapHitFor(stage, rosterIndex),
                RetirementRiskFor(age, rosterIndex),
                $"League-facing {ReadableStage(stage).ToLowerInvariant()} record for {team.TeamName}; contract and age context are available without exposing private AI negotiation details.");
        }
    }

    private static WorkforcePlayerRecord Record(string personId, string name, TeamSelectionOption team, RosterPosition position, int age, int overall, int potential, Contract? contract, NewGmScenarioSnapshot scenario)
    {
        var years = contract is null ? 0 : Math.Max(0, contract.Term.EndDate.Year - scenario.CurrentDate.Year);
        return new WorkforcePlayerRecord(
            personId,
            name,
            team.OrganizationId,
            team.TeamName,
            position,
            age,
            StageFor(age, position),
            Math.Clamp(overall, 55, 96),
            Math.Clamp(Math.Max(overall, potential), 55, 98),
            years,
            contract?.Term.EndDate ?? ContractExpiryCalendar.CommonExpiryDate(scenario.Season.Year + 1, scenario.Season.Settings),
            years <= 1 ? age >= 27 ? FreeAgentRightsStatus.PendingUfa : FreeAgentRightsStatus.PendingRfa : FreeAgentRightsStatus.UnderContract,
            contract?.Money.SalaryOrStipend ?? 850_000m,
            RetirementRiskFor(age, StableHash(personId)),
            $"Current {ReadableStage(StageFor(age, position)).ToLowerInvariant()} player in the organization's existing NHL workforce.");
    }

    private static int AgeFor(string strategy, int teamIndex, int rosterIndex)
    {
        var contender = strategy.Contains("win", StringComparison.OrdinalIgnoreCase);
        var builder = strategy.Contains("prospect", StringComparison.OrdinalIgnoreCase);
        var ages = contender
            ? new[] { 36, 34, 32, 31, 30, 29, 28, 28, 27, 27, 26, 26, 25, 25, 24, 24, 23, 22, 22, 21, 21, 20, 19 }
            : builder
                ? new[] { 34, 31, 29, 28, 27, 26, 25, 24, 24, 23, 23, 22, 22, 21, 21, 20, 20, 20, 19, 19, 18, 18, 21 }
                : new[] { 35, 33, 31, 30, 29, 28, 27, 27, 26, 26, 25, 25, 24, 24, 23, 23, 22, 22, 21, 21, 20, 20, 19 };
        var age = ages[(rosterIndex + teamIndex % 3) % ages.Length];
        return rosterIndex == 0 && teamIndex % 9 == 0 ? 38 : age;
    }

    private static int YearsRemaining(int teamIndex, int rosterIndex) => rosterIndex % 7 switch
    {
        0 or 1 or 2 => 1,
        3 or 4 or 5 => 2,
        _ => 3 + ((teamIndex + rosterIndex) % 4)
    };

    private static WorkforceCareerStage StageFor(int age, RosterPosition position) =>
        age <= 21 ? WorkforceCareerStage.Rookie :
        age <= 24 ? WorkforceCareerStage.YoungDeveloping :
        age <= 26 ? WorkforceCareerStage.EmergingNhlPlayer :
        age <= 29 ? WorkforceCareerStage.Prime :
        age <= 33 ? WorkforceCareerStage.EstablishedVeteran :
        age <= 36 ? WorkforceCareerStage.AgingVeteran :
        age <= (position == RosterPosition.Goalie ? 39 : 38) ? WorkforceCareerStage.LateCareerDepth : WorkforceCareerStage.NearRetirement;

    public static RetirementRisk RetirementRiskFor(int age, int seed) =>
        age >= 40 ? RetirementRisk.ExpectedToRetire :
        age >= 38 ? RetirementRisk.RetirementRisk :
        age >= 36 ? seed % 3 == 0 ? RetirementRisk.LikelyFinalSeason : RetirementRisk.ConsideringRetirement :
        age >= 34 ? RetirementRisk.LateCareer : RetirementRisk.None;

    private static RosterPosition PositionFor(int index) => index switch
    {
        0 or 1 => RosterPosition.Goalie,
        <= 8 => RosterPosition.Defense,
        _ when index % 3 == 0 => RosterPosition.Center,
        _ when index % 3 == 1 => RosterPosition.LeftWing,
        _ => RosterPosition.RightWing
    };

    private static int EstimateOverall(int age, RosterPosition position, int seed)
    {
        var ageBonus = age switch { <= 21 => -6, <= 24 => -2, <= 29 => 4, <= 33 => 2, <= 36 => 0, _ => -3 };
        var goalieBonus = position == RosterPosition.Goalie && age >= 27 ? 2 : 0;
        return Math.Clamp(76 + ageBonus + goalieBonus + seed % 8 - 4, 68, 94);
    }

    private static int EstimatePotential(int age, RosterPosition position, int seed)
    {
        var remaining = age switch { <= 21 => 12, <= 24 => 8, <= 29 => 3, <= 33 => 1, _ => 0 };
        if (position == RosterPosition.Goalie && age <= 30) remaining += 2;
        return Math.Clamp(EstimateOverall(age, position, seed) + remaining + seed % 4, 70, 98);
    }

    private static decimal CapHitFor(WorkforceCareerStage stage, int index) => stage switch
    {
        WorkforceCareerStage.Prime => 4_000_000m + index % 4 * 850_000m,
        WorkforceCareerStage.EstablishedVeteran => 2_500_000m + index % 4 * 600_000m,
        WorkforceCareerStage.AgingVeteran or WorkforceCareerStage.LateCareerDepth => 900_000m + index % 4 * 450_000m,
        _ => 850_000m + index % 5 * 325_000m
    };

    private static PlayerAgeDistributionProfile AgeDistribution(IEnumerable<int> ages)
    {
        var list = ages.ToArray();
        return new PlayerAgeDistributionProfile(list.Count(age => age is >= 18 and <= 21), list.Count(age => age is >= 22 and <= 24), list.Count(age => age is >= 25 and <= 29), list.Count(age => age is >= 30 and <= 33), list.Count(age => age is >= 34 and <= 36), list.Count(age => age >= 37));
    }

    private static CareerStageDistributionProfile StageDistribution(IEnumerable<WorkforceCareerStage> stages) =>
        new(stages.GroupBy(stage => stage).ToDictionary(group => group.Key, group => group.Count()));

    private static int StableHash(string value) => value.Aggregate(17, (hash, character) => unchecked(hash * 31 + character)) & int.MaxValue;

    private static string PositionShort(RosterPosition position) => position switch { RosterPosition.Center => "C", RosterPosition.LeftWing => "LW", RosterPosition.RightWing => "RW", RosterPosition.Defense => "D", RosterPosition.Goalie => "G", _ => "P" };
    private static string ReadableStage(WorkforceCareerStage stage) => string.Concat(stage.ToString().SelectMany((character, index) => index > 0 && char.IsUpper(character) ? new[] { ' ', character } : new[] { character }));
    private static string ReadableStrategy(string strategy) => string.IsNullOrWhiteSpace(strategy) ? "mixed" : strategy.ToLowerInvariant();
}
