using LegacyEngine.Contracts;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class ContractExpiryService
{
    public NewGmScenarioSnapshot ProcessExpiredContracts(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var scenarioContracts = ExpireContracts(scenario.Contracts, scenario.CurrentDate);
        var alphaContracts = ExpireContracts(scenario.AlphaSnapshot.Contracts, scenario.CurrentDate);
        var alpha = scenario.AlphaSnapshot with { Contracts = alphaContracts };
        var updated = scenario with
        {
            AlphaSnapshot = alpha,
            Contracts = scenarioContracts
        };

        updated = new RfaUfaService().EnsureRights(updated, rulebook ?? scenario.LeagueProfile.Rulebook);
        updated.Validate();
        return updated;
    }

    private static IReadOnlyList<Contract> ExpireContracts(IReadOnlyList<Contract> contracts, DateOnly currentDate) =>
        contracts
            .Select(contract => contract.Status == ContractStatus.Signed && contract.Term.EndDate <= currentDate
                ? contract.Expire(currentDate)
                : contract)
            .ToArray();
}
