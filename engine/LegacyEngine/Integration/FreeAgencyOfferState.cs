namespace LegacyEngine.Integration;

public sealed record FreeAgencyOfferState(
    string OfferStateId,
    string PersonId,
    string PersonName,
    DateOnly SubmittedOn,
    DateOnly ResponseDate,
    FreeAgencyDecision ResponseStatus,
    int DecisionDelayDays,
    int MarketPressure,
    ContractOfferEvaluation Evaluation,
    string Explanation)
{
    public bool IsPendingResponse => ResponseStatus == FreeAgencyDecision.AwaitingResponse;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OfferStateId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PersonName)
            || string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("Free agency offer state requires id, person, and explanation.");
        }

        if (ResponseDate < SubmittedOn || DecisionDelayDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ResponseDate), "Free agency response date must be after submission.");
        }

        if (MarketPressure is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MarketPressure), "Market pressure must be between 0 and 100.");
        }

        Evaluation.Validate();
    }
}
