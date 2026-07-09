using LegacyEngine.Contracts;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class SalaryCapService
{
    public SalaryCapProfile BuildProfile(Rulebook? rulebook)
    {
        var source = rulebook?.SalaryCapRules;
        var roster = rulebook?.RosterRules;
        var contract = rulebook?.ContractRules;
        var budget = rulebook?.BudgetRules;
        var enabled = source?.SalaryCapEnabled
            ?? contract?.SalaryCapEnabled
            ?? budget?.HardSalaryCapEnabled
            ?? false;
        var cap = source?.CapAmount
            ?? contract?.SalaryCapAmount
            ?? budget?.HardSalaryCapAmount
            ?? 0m;
        var profile = new SalaryCapProfile(
            enabled,
            enabled ? cap : 0m,
            enabled ? source?.SalaryFloor ?? 0m : 0m,
            source?.MaximumRosterSize ?? roster?.ActiveRoster ?? roster?.MaxRoster ?? 0,
            source?.MaximumContracts ?? 0,
            source?.MaximumRetainedSalaryPlaceholder ?? 0m,
            source?.OffseasonCapRulesPlaceholder ?? "Offseason cap rules are a placeholder in Alpha 5.6.");
        profile.Validate();
        return profile;
    }

    public SalaryCapSnapshot BuildSnapshot(NewGmScenarioSnapshot scenario, Rulebook? rulebook)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var profile = BuildProfile(rulebook ?? scenario.LeagueProfile.Rulebook);
        if (!profile.IsEnabled)
        {
            var disabled = new SalaryCapSnapshot(
                profile,
                new SalaryCapSpace(0m, 0m, 0m, 0m, 0m, 0m),
                0m,
                0m,
                0m,
                0m,
                0m,
                PlayerContracts(scenario).Count,
                Array.Empty<SalaryCapContractCommitment>(),
                Array.Empty<BuyoutPenalty>(),
                SalaryCapStatus.Disabled,
                new[] { "Salary cap disabled by rulebook; use operating budget instead." });
            disabled.Validate();
            return disabled;
        }

        var commitments = PlayerContracts(scenario)
            .Select(contract => CommitmentFor(scenario, contract))
            .ToArray();
        var buyoutPenalties = BuyoutPenaltiesFor(scenario).ToArray();
        var currentBuyoutPenalty = buyoutPenalties.Where(penalty => penalty.SeasonYear == scenario.Season.Year).Sum(penalty => penalty.Amount);
        var used = commitments.Sum(commitment => commitment.CapHit) + currentBuyoutPenalty;
        var remaining = profile.CapAmount - used;
        var floorRemaining = Math.Max(0m, profile.SalaryFloor - used);
        var percent = profile.CapAmount <= 0 ? 0m : Math.Round(used / profile.CapAmount * 100m, 2);
        var warnings = ReasonsFor(profile, used, remaining, commitments.Length, includeFloor: scenario.Season.CurrentPhase != LegacyEngine.Seasons.SeasonPhase.Offseason).ToList();
        var status = StatusFor(scenario, profile, used, remaining, commitments.Length);
        var snapshot = new SalaryCapSnapshot(
            profile,
            new SalaryCapSpace(profile.CapAmount, used, remaining, profile.SalaryFloor, floorRemaining, percent),
            used,
            Math.Max(0m, remaining),
            commitments.Sum(commitment => commitment.TotalRemainingValue) + buyoutPenalties.Where(penalty => penalty.SeasonYear > scenario.Season.Year).Sum(penalty => penalty.Amount),
            commitments.Where(commitment => commitment.ExpiresOn <= scenario.CurrentDate.AddDays(365)).Sum(commitment => commitment.CapHit),
            currentBuyoutPenalty,
            commitments.Length,
            commitments,
            buyoutPenalties,
            status,
            warnings);
        snapshot.Validate();
        return snapshot;
    }

    public SalaryCapCalculation ProjectAfterSigning(
        NewGmScenarioSnapshot scenario,
        Rulebook? rulebook,
        decimal annualSalary,
        int termYears,
        decimal signingBonus = 0m)
    {
        var before = BuildSnapshot(scenario, rulebook);
        if (!before.IsEnabled)
        {
            return Calculation(before, before, true, Array.Empty<string>());
        }

        var projectedHit = CapHit(annualSalary, signingBonus, termYears);
        var after = ProjectSnapshot(scenario, rulebook, before.CurrentCapHit + projectedHit, before.ContractCount + 1);
        return Calculation(before, after, IsCompliant(after, includeFloor: false), ReasonsFor(after, includeFloor: false));
    }

    public SalaryCapCalculation ProjectAfterTrade(NewGmScenarioSnapshot scenario, Rulebook? rulebook, TradeOffer offer)
    {
        offer.Validate();
        var before = BuildSnapshot(scenario, rulebook);
        if (!before.IsEnabled)
        {
            return Calculation(before, before, true, Array.Empty<string>());
        }

        var outgoing = offer.PlayerGives.Where(asset => asset.AssetType == TradeAssetType.Player).Sum(asset => asset.SalaryImpact);
        var incoming = offer.PlayerReceives.Where(asset => asset.AssetType == TradeAssetType.Player).Sum(asset => asset.SalaryImpact);
        var contractDelta = offer.PlayerReceives.Count(asset => asset.AssetType == TradeAssetType.Player && asset.SalaryImpact > 0)
            - offer.PlayerGives.Count(asset => asset.AssetType == TradeAssetType.Player && asset.SalaryImpact > 0);
        var after = ProjectSnapshot(scenario, rulebook, Math.Max(0m, before.CurrentCapHit - outgoing + incoming), Math.Max(0, before.ContractCount + contractDelta));
        return Calculation(before, after, IsCompliant(after, includeFloor: false), ReasonsFor(after, includeFloor: false));
    }

    public SalaryCapCalculation ValidateRosterCompliance(NewGmScenarioSnapshot scenario, Rulebook? rulebook)
    {
        var before = BuildSnapshot(scenario, rulebook);
        var reasons = ReasonsFor(before, includeFloor: scenario.Season.CurrentPhase != LegacyEngine.Seasons.SeasonPhase.Offseason).ToList();
        var active = scenario.AlphaSnapshot.Roster.ActivePlayers.Count;
        if (before.Profile.MaximumRosterSize > 0 && active > before.Profile.MaximumRosterSize)
        {
            reasons.Add($"Roster size {active}/{before.Profile.MaximumRosterSize} exceeds the professional roster limit.");
        }

        var calculation = new SalaryCapCalculation(before, before, reasons.Count == 0, reasons);
        calculation.Validate();
        return calculation;
    }

    public string FormatSummary(SalaryCapSnapshot snapshot)
    {
        snapshot.Validate();
        if (!snapshot.IsEnabled)
        {
            return "Salary cap disabled by rulebook.";
        }

        return $"Cap used {snapshot.CapUsed:C0} of {snapshot.Profile.CapAmount:C0}; remaining {snapshot.CapRemaining:C0}; floor {snapshot.Profile.SalaryFloor:C0}; status {snapshot.Status}.";
    }

    private static SalaryCapCalculation Calculation(SalaryCapSnapshot before, SalaryCapSnapshot after, bool compliant, IReadOnlyList<string> reasons)
    {
        var calculation = new SalaryCapCalculation(before, after, compliant, reasons);
        calculation.Validate();
        return calculation;
    }

    private static SalaryCapSnapshot ProjectSnapshot(NewGmScenarioSnapshot scenario, Rulebook? rulebook, decimal projectedCapHit, int contractCount)
    {
        var profile = new SalaryCapService().BuildProfile(rulebook ?? scenario.LeagueProfile.Rulebook);
        var remaining = profile.CapAmount - projectedCapHit;
        var floorRemaining = Math.Max(0m, profile.SalaryFloor - projectedCapHit);
        var status = StatusFor(scenario, profile, projectedCapHit, remaining, contractCount);
        var warnings = ReasonsFor(profile, projectedCapHit, remaining, contractCount, includeFloor: false);
        var snapshot = new SalaryCapSnapshot(
            profile,
            new SalaryCapSpace(profile.CapAmount, projectedCapHit, remaining, profile.SalaryFloor, floorRemaining, profile.CapAmount <= 0 ? 0m : Math.Round(projectedCapHit / profile.CapAmount * 100m, 2)),
            projectedCapHit,
            Math.Max(0m, remaining),
            0m,
            0m,
            0m,
            contractCount,
            Array.Empty<SalaryCapContractCommitment>(),
            Array.Empty<BuyoutPenalty>(),
            status,
            warnings);
        snapshot.Validate();
        return snapshot;
    }

    private static IReadOnlyList<string> ReasonsFor(SalaryCapSnapshot snapshot, bool includeFloor) =>
        ReasonsFor(snapshot.Profile, snapshot.CurrentCapHit, snapshot.Space.CapRemaining, snapshot.ContractCount, includeFloor);

    private static IReadOnlyList<string> ReasonsFor(SalaryCapProfile profile, decimal capHit, decimal remaining, int contractCount, bool includeFloor)
    {
        if (!profile.IsEnabled)
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        if (remaining < 0)
        {
            reasons.Add("Move would exceed salary cap.");
        }

        if (profile.MaximumContracts > 0 && contractCount > profile.MaximumContracts)
        {
            reasons.Add("Move would exceed contract limit.");
        }

        if (includeFloor && profile.SalaryFloor > 0 && capHit < profile.SalaryFloor)
        {
            reasons.Add("Club is below salary floor.");
        }

        return reasons;
    }

    private static bool IsCompliant(SalaryCapSnapshot snapshot, bool includeFloor) =>
        ReasonsFor(snapshot, includeFloor).Count == 0;

    private static IReadOnlyList<Contract> PlayerContracts(NewGmScenarioSnapshot scenario) =>
        scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .Where(contract => contract.Status == ContractStatus.Signed
                && string.Equals(contract.OrganizationId, scenario.Organization.OrganizationId, StringComparison.Ordinal)
                && IsPlayerContract(contract))
            .ToArray();

    private static bool IsPlayerContract(Contract contract) =>
        contract.ContractType == ContractType.JuniorPlayerAgreement;

    private static IReadOnlyList<BuyoutPenalty> BuyoutPenaltiesFor(NewGmScenarioSnapshot scenario) =>
        scenario.ContractBuyouts
            .Where(buyout => buyout.Status == BuyoutStatus.Completed)
            .SelectMany(buyout => buyout.Calculation.Penalties)
            .GroupBy(penalty => penalty.PenaltyId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();

    private static SalaryCapContractCommitment CommitmentFor(NewGmScenarioSnapshot scenario, Contract contract)
    {
        var years = YearsRemaining(scenario.CurrentDate, contract.Term.EndDate);
        var capHit = CapHit(contract.Money.SalaryOrStipend, contract.Money.SigningBonus, years);
        var commitment = new SalaryCapContractCommitment(
            contract.ContractId,
            contract.PersonId,
            PersonName(scenario, contract.PersonId),
            capHit,
            years,
            contract.Term.EndDate,
            capHit * years);
        commitment.Validate();
        return commitment;
    }

    private static decimal CapHit(decimal annualSalary, decimal signingBonus, int termYears) =>
        annualSalary + (termYears <= 0 ? 0m : signingBonus / termYears);

    private static int YearsRemaining(DateOnly currentDate, DateOnly endDate)
    {
        if (endDate <= currentDate)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling((endDate.DayNumber - currentDate.DayNumber) / 365m));
    }

    private static SalaryCapStatus StatusFor(NewGmScenarioSnapshot scenario, SalaryCapProfile profile, decimal used, decimal remaining, int contractCount)
    {
        if (!profile.IsEnabled)
        {
            return SalaryCapStatus.Disabled;
        }

        if (remaining < 0 || (profile.MaximumContracts > 0 && contractCount > profile.MaximumContracts))
        {
            return SalaryCapStatus.Violation;
        }

        if (profile.SalaryFloor > 0 && used < profile.SalaryFloor && scenario.Season.CurrentPhase != LegacyEngine.Seasons.SeasonPhase.Offseason)
        {
            return SalaryCapStatus.Violation;
        }

        if (profile.CapAmount > 0 && used >= profile.CapAmount)
        {
            return SalaryCapStatus.OverCap;
        }

        return profile.CapAmount > 0 && used / profile.CapAmount >= 0.9m
            ? SalaryCapStatus.NearLimit
            : SalaryCapStatus.Comfortable;
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? personId;
}
