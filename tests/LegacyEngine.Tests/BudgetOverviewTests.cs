using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Owners;

internal sealed class BudgetOverviewTests
{
    public void BudgetOverviewCalculatesContractTotals()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var date = scenario.ScenarioSnapshot.CurrentDate;
        var playerContract = SignedContract(
            "contract-budget-player",
            scenario.ScenarioSnapshot.AlphaSnapshot.Roster.Players.First().PersonId,
            scenario.ScenarioSnapshot.Organization.OrganizationId,
            ContractType.JuniorPlayerAgreement,
            12_500,
            1_500,
            date);
        var staffContract = SignedContract(
            "contract-budget-staff",
            scenario.ScenarioSnapshot.StaffMembers.First().PersonId,
            scenario.ScenarioSnapshot.Organization.OrganizationId,
            ContractType.StaffContract,
            50_000,
            5_000,
            date);
        var snapshot = scenario.ScenarioSnapshot with
        {
            Contracts = scenario.ScenarioSnapshot.Contracts.Concat(new[] { playerContract, staffContract }).ToArray()
        };

        var budget = new BudgetOverviewService().Build(snapshot);

        Assert.True(budget.TotalBudget > 0, "Budget overview should expose owner total budget.");
        Assert.True(budget.PlayerContractsTotal >= 14_000, "Budget overview should include signed player contract totals.");
        Assert.True(budget.StaffContractsTotal >= 55_000, "Budget overview should include signed staff contract totals.");
        Assert.Equal(budget.PlayerContractsTotal + budget.StaffContractsTotal, budget.UsedBudget);
        Assert.Equal(budget.TotalBudget - budget.UsedBudget, budget.RemainingBudget);
        Assert.Equal(budget.RemainingBudget, budget.OverUnderBudget);
    }

    public void BudgetOverviewWarnsWhenOverBudget()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var owner = scenario.ScenarioSnapshot.AlphaSnapshot.Owner with
        {
            Budget = new OwnerBudget(1, 0, 0, 0, 0)
        };
        var alpha = scenario.ScenarioSnapshot.AlphaSnapshot with { Owner = owner };
        var snapshot = scenario.ScenarioSnapshot with { AlphaSnapshot = alpha };

        var budget = new BudgetOverviewService().Build(snapshot);

        Assert.Equal(BudgetStatus.OverBudget, budget.Status);
        Assert.True(budget.OwnerBudgetConfidence.Contains("over", StringComparison.OrdinalIgnoreCase), "Owner status should explain over-budget concern.");
    }

    private static Contract SignedContract(
        string contractId,
        string personId,
        string organizationId,
        ContractType contractType,
        decimal salary,
        decimal bonus,
        DateOnly date)
    {
        var contract = new Contract(
            ContractId: contractId,
            PersonId: personId,
            OrganizationId: organizationId,
            ContractType: contractType,
            Status: ContractStatus.Signed,
            Term: new ContractTerm(date, date.AddYears(1).AddDays(-1)),
            Money: new ContractMoney(salary, bonus, "CAD"),
            Clauses: Array.Empty<ContractClause>(),
            OfferedOn: date,
            SignedOn: date,
            RejectedOn: null,
            TerminatedOn: null,
            ExpiredOn: null);
        contract.Validate();
        return contract;
    }
}
