namespace LegacyEngine.Integration;

public sealed class WorkforceRealismValidator
{
    public WorkforceValidationResult Validate(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.LeagueProfile.Experience != LeagueExperience.Nhl)
        {
            return Result(true, "Workforce validation is informational outside NHL-style scenarios.");
        }

        var profile = scenario.LeagueWorkforce ?? throw new InvalidOperationException("NHL workforce profile was not generated.");
        var issues = new List<WorkforceValidationIssue>();
        var ages = profile.AgeDistribution;
        Check(ages.Age25To29 > 0, "prime-age", "Prime-age players must form part of the league workforce.");
        Check(ages.Age34To36 > 0, "veterans", "The league needs aging veterans.");
        Check(ages.Age37Plus > 0, "late-career", "The league needs a small number of late-career players.");
        Check(profile.Teams.All(team => team.AgeDistribution.Total >= 20), "team-roster-size", "Every NHL team needs a full workforce profile.");
        Check(profile.Teams.All(team => team.CareerStageDistribution.Counts.Keys.Count() >= 3), "team-age-mix", "Every NHL team should have multiple career stages.");
        Check(scenario.FreeAgentMarket?.FreeAgents.Any(agent => agent.Age >= 34) == true, "veteran-market", "The market needs veteran free agents.");
        Check(scenario.FreeAgentMarket?.FreeAgents.Any(agent => agent.Age <= 24) == true, "young-market", "The market needs young free agents.");
        Check(scenario.RetirementConsiderations.Any(item => item.Risk >= RetirementRisk.ConsideringRetirement), "retirement-watch", "The league needs visible retirement-watch candidates.");
        Check(profile.Teams.All(team => team.Players.Any(player => player.ContractYearsRemaining <= 1)), "expiry-decisions", "Every NHL team needs upcoming contract decisions.");

        var valid = issues.All(issue => issue.Severity != WorkforceValidationSeverity.Invalid);
        return new WorkforceValidationResult(valid, issues, valid ? "NHL workforce generation passed age, veteran, market, retirement, and contract-decision checks." : "NHL workforce generation needs rebalancing.");

        void Check(bool condition, string code, string message)
        {
            if (!condition)
            {
                issues.Add(new WorkforceValidationIssue(WorkforceValidationSeverity.Invalid, code, message));
            }
        }
    }

    private static WorkforceValidationResult Result(bool valid, string summary) => new(valid, Array.Empty<WorkforceValidationIssue>(), summary);
}
