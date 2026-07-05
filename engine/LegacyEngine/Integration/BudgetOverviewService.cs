using LegacyEngine.Contracts;

namespace LegacyEngine.Integration;

public sealed class BudgetOverviewService
{
    public BudgetSnapshot Build(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var contracts = scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.Status == ContractStatus.Signed)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();

        var playerContracts = contracts
            .Where(contract => contract.ContractType == ContractType.JuniorPlayerAgreement)
            .Sum(contract => contract.Money.SalaryOrStipend + contract.Money.SigningBonus);
        var staffContracts = contracts
            .Where(contract => contract.ContractType != ContractType.JuniorPlayerAgreement)
            .Sum(contract => contract.Money.SalaryOrStipend + contract.Money.SigningBonus);

        var ownerBudget = scenario.AlphaSnapshot.Owner.Budget;
        var used = playerContracts + staffContracts;
        var remaining = ownerBudget.Total - used;
        var ratio = ownerBudget.Total == 0 ? 1 : used / ownerBudget.Total;
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
            TotalBudget: ownerBudget.Total,
            UsedBudget: used,
            RemainingBudget: remaining,
            PlayerContractsTotal: playerContracts,
            StaffContractsTotal: staffContracts,
            ScoutingBudget: ownerBudget.Scouting,
            MedicalAndStaffOperationsBudget: ownerBudget.Staff + ownerBudget.Operations,
            Status: status,
            OwnerBudgetConfidence: confidence);
        snapshot.Validate();
        return snapshot;
    }
}
