namespace LegacyEngine.Integration;

public sealed record ContractManagementSummary(
    IReadOnlyList<ContractAsk> ExpiringPlayers,
    IReadOnlyList<ContractAsk> ExpiringStaff,
    IReadOnlyList<ContractAsk> UnsignedProspects,
    IReadOnlyList<ContractAsk> PendingOffers,
    IReadOnlyList<ContractAsk> AcceptedOffersAwaitingApproval,
    IReadOnlyList<ContractAsk> RejectedOffers,
    BudgetSnapshot Budget)
{
    public void Validate()
    {
        foreach (var ask in ExpiringPlayers.Concat(ExpiringStaff).Concat(UnsignedProspects).Concat(PendingOffers).Concat(AcceptedOffersAwaitingApproval).Concat(RejectedOffers))
        {
            ask.Validate();
        }

        Budget.Validate();
    }
}
