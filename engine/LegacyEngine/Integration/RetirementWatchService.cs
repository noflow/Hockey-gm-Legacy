using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class RetirementWatchService
{
    public NewGmScenarioSnapshot EnsureRetirementWatch(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var records = new List<RetirementConsideration>();
        foreach (var player in scenario.LeagueWorkforce?.LeaguePlayers ?? Array.Empty<WorkforcePlayerRecord>())
        {
            if (player.RetirementRisk == RetirementRisk.None)
            {
                continue;
            }

            records.Add(Build(player.PersonId, player.PlayerName, player.Age, player.Position, player.RetirementRisk));
        }

        foreach (var agent in scenario.FreeAgentMarket?.FreeAgents ?? Array.Empty<FreeAgent>())
        {
            if (agent.RetirementRisk == RetirementRisk.None)
            {
                continue;
            }

            records.RemoveAll(item => item.PersonId == agent.PersonId);
            records.Add(Build(agent.PersonId, agent.Name, agent.Age, agent.Position, agent.RetirementRisk));
        }

        var updated = scenario with { RetirementConsiderations = records.OrderByDescending(item => item.Risk).ThenByDescending(item => item.Age).ToArray() };
        updated.Validate();
        return updated;
    }

    private static RetirementConsideration Build(string personId, string name, int age, RosterPosition position, RetirementRisk risk)
    {
        var preference = new FinalContractPreference(
            PrefersOneYearTerm: risk >= RetirementRisk.ConsideringRetirement,
            RequiresNhlOpportunity: age >= 34,
            PrefersContender: age >= 34,
            risk >= RetirementRisk.LikelyFinalSeason ? "Likely to prioritize a one-year NHL opportunity with a competitive club." : "Late-career player values role clarity and a realistic opportunity.");
        var status = risk >= RetirementRisk.RetirementRisk ? LateCareerStatus.RetirementWatch : risk >= RetirementRisk.ConsideringRetirement ? LateCareerStatus.FinalContractCandidate : LateCareerStatus.ActiveLateCareer;
        return new RetirementConsideration(personId, name, age, position, risk, status, preference, $"{name} is {Readable(risk).ToLowerInvariant()}; review role, workload, health, and contract term before planning beyond the current season.");
    }

    private static string Readable(RetirementRisk risk) => string.Concat(risk.ToString().SelectMany((character, index) => index > 0 && char.IsUpper(character) ? new[] { ' ', character } : new[] { character }));
}
