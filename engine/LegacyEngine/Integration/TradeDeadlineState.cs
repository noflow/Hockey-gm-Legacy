namespace LegacyEngine.Integration;

public sealed record TradeDeadlineState(
    DateOnly DeadlineDate,
    DateOnly LastEvaluatedOn,
    int DaysRemaining,
    TradeDeadlineStatus Status,
    bool HasExpandedTradeBlock,
    bool HasPostedClosed,
    IReadOnlyList<TradeDeadlineStatus> MessageStatusesCreated,
    IReadOnlyList<DeadlineRumor> Rumors,
    DeadlineTradeBlockUpdate? LastTradeBlockUpdate,
    BuyerSellerAssessment? BuyerSellerAssessment)
{
    public void Validate()
    {
        foreach (var rumor in Rumors)
        {
            rumor.Validate();
        }

        LastTradeBlockUpdate?.Validate();
        BuyerSellerAssessment?.Validate();
    }
}
