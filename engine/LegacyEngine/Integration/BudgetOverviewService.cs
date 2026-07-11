using LegacyEngine.Contracts;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class BudgetOverviewService
{
    public BudgetSnapshot Build(NewGmScenarioSnapshot scenario)
    {
        return Build(scenario, RulebookPresets.CreateJuniorMajor());
    }

    public BudgetSnapshot Build(NewGmScenarioSnapshot scenario, Rulebook rulebook)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var hockeyOps = new StaffBudgetService().Build(scenario, rulebook);
        var contracts = scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.Status == ContractStatus.Signed && contract.Term.EndDate > scenario.CurrentDate)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();

        var playerContracts = contracts
            .Where(contract => contract.ContractType == ContractType.JuniorPlayerAgreement)
            .Sum(contract => contract.Money.SalaryOrStipend + contract.Money.SigningBonus);
        var buyoutPenalty = scenario.ContractBuyouts
            .Where(buyout => buyout.Status == BuyoutStatus.Completed)
            .SelectMany(buyout => buyout.Calculation.Penalties)
            .Where(penalty => penalty.SeasonYear == scenario.Season.Year)
            .Sum(penalty => penalty.Amount);
        var ownerBudget = scenario.AlphaSnapshot.Owner.Budget;
        var totalBudget = hockeyOps.TotalBudget;
        var used = hockeyOps.UsedBudget;
        var remaining = totalBudget - used;
        var ratio = totalBudget == 0 ? 1 : used / totalBudget;
        var status = remaining < 0
            ? BudgetStatus.OverBudget
            : ratio >= 0.9m
                ? BudgetStatus.NearLimit
                : BudgetStatus.UnderBudget;

        var confidence = status switch
        {
            BudgetStatus.OverBudget => "Owner concern: spending is over the current budget.",
            BudgetStatus.NearLimit => "Owner caution: spending is near the current limit.",
            _ => "Owner confidence: spending is under control."
        };

        var snapshot = new BudgetSnapshot(
            TotalBudget: totalBudget,
            UsedBudget: used,
            RemainingBudget: remaining,
            PlayerContractsTotal: playerContracts + buyoutPenalty,
            StaffContractsTotal: hockeyOps.StaffTotal + hockeyOps.StaffReleaseObligations,
            ScoutingBudget: ownerBudget.Scouting,
            MedicalAndStaffOperationsBudget: ownerBudget.Staff + ownerBudget.Operations,
            Status: status,
            OwnerBudgetConfidence: hockeyOps.Warnings.Count == 0 ? confidence : string.Join(" ", hockeyOps.Warnings))
        {
            GmSalary = hockeyOps.GmSalary,
            CoachingSalaries = hockeyOps.CoachingSalaries,
            ScoutingSalaries = hockeyOps.ScoutingSalaries,
            MedicalTrainingSalaries = hockeyOps.MedicalTrainingSalaries,
            StaffTotal = hockeyOps.StaffTotal,
            StaffReleaseObligations = hockeyOps.StaffReleaseObligations
        };
        snapshot.Validate();
        return snapshot;
    }
}
