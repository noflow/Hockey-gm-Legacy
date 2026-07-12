using LegacyEngine.Contracts;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

/// <summary>Applies rulebook-driven ELC slides without replacing contracts.</summary>
public sealed class EntryLevelSlideService
{
    private readonly RosterAllocationService _allocation = new();

    public EntryLevelSlideResult Evaluate(NewGmScenarioSnapshot scenario, string personId, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var resolvedRulebook = rulebook ?? scenario.LeagueProfile.Rulebook;
        var allocated = _allocation.EnsureAllocation(scenario, resolvedRulebook);
        var player = allocated.OrganizationRoster!.Players.SingleOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Player was not found in organization allocation.", nameof(personId));
        var contract = PlayerContract(allocated, personId);
        var eligibility = player.SlideEligibility ?? new EntryLevelSlideEligibility(personId, false, false, player.Age, 0, 0, 0, 0, false, "No entry-level slide data is available.");

        if (contract is null || !eligibility.IsEligible)
        {
            return new EntryLevelSlideResult(allocated, eligibility, contract, null, eligibility.Reason);
        }

        var priorSlides = allocated.ContractSlideHistory
            .Where(history => history.ContractId == contract.ContractId && history.Status == EntryLevelSlideStatus.SlidePreserved)
            .ToArray();
        var currentSeasonSlide = priorSlides.LastOrDefault(history => history.EvaluatedOn.Year == allocated.CurrentDate.Year);
        if (currentSeasonSlide is not null)
        {
            return new EntryLevelSlideResult(allocated, eligibility, contract, currentSeasonSlide, "Contract slide was already evaluated for the current season.");
        }

        if (priorSlides.Length >= eligibility.MaximumSlides)
        {
            var existing = priorSlides.Last();
            return new EntryLevelSlideResult(allocated, eligibility, contract, existing, "Contract slide limit has already been reached.");
        }

        var previousExpiry = contract.Term.EndDate;
        var newExpiry = ContractExpiryCalendar.CommonExpiryDate(previousExpiry.Year + 1, allocated.Season.Settings);
        var updatedContract = contract with { Term = contract.Term with { EndDate = newExpiry } };
        var history = new ContractSlideHistory(
            $"contract-slide:{Guid.NewGuid():N}",
            personId,
            contract.ContractId,
            allocated.CurrentDate,
            EntryLevelSlideStatus.SlidePreserved,
            previousExpiry,
            newExpiry,
            $"Entry-level contract year slid after {eligibility.NhlGames}/{eligibility.NhlGameThreshold} NHL games and a junior return.");
        history.Validate();
        var updated = ReplaceContract(allocated, updatedContract) with
        {
            ContractSlideHistory = allocated.ContractSlideHistory.Append(history).ToArray()
        };
        updated = _allocation.EnsureAllocation(updated, resolvedRulebook);
        return new EntryLevelSlideResult(updated, eligibility, updatedContract, history, history.Summary);
    }

    public NewGmScenarioSnapshot EvaluateSeasonRollover(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var result = _allocation.EnsureAllocation(scenario, rulebook);
        foreach (var player in result.OrganizationRoster!.In(OrganizationRosterGroup.SignedJuniorReturn).ToArray())
        {
            var evaluation = Evaluate(result, player.PersonId, rulebook);
            result = evaluation.ScenarioSnapshot;
        }

        return _allocation.EnsureAllocation(result, rulebook);
    }

    private static Contract? PlayerContract(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .LastOrDefault(contract => contract.PersonId == personId && contract.Status == ContractStatus.Signed);

    private static NewGmScenarioSnapshot ReplaceContract(NewGmScenarioSnapshot scenario, Contract replacement)
    {
        var contracts = scenario.Contracts.Select(contract => contract.ContractId == replacement.ContractId ? replacement : contract).ToArray();
        var alphaContracts = scenario.AlphaSnapshot.Contracts.Select(contract => contract.ContractId == replacement.ContractId ? replacement : contract).ToArray();
        return scenario with { Contracts = contracts, AlphaSnapshot = scenario.AlphaSnapshot with { Contracts = alphaContracts } };
    }
}
