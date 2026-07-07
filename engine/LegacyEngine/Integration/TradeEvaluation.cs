namespace LegacyEngine.Integration;

public sealed record TradeEvaluation(
    TradeOfferStatus Decision,
    int Score,
    string Explanation,
    IReadOnlyList<string> Reasons,
    decimal BudgetImpact,
    int RosterImpact,
    TradeInterest Interest = TradeInterest.Medium,
    string CounterSuggestion = "",
    IReadOnlyList<string>? StaffReactions = null,
    IReadOnlyList<string>? PlayerReactions = null,
    TeamDirection OtherTeamDirection = TeamDirection.Neutral,
    TradeGmPersonality OtherGmPersonality = TradeGmPersonality.Conservative,
    IReadOnlyList<PositionNeed>? MatchedNeeds = null)
{
    public IReadOnlyList<string> StaffReactionNotes => StaffReactions ?? Array.Empty<string>();

    public IReadOnlyList<string> PlayerReactionNotes => PlayerReactions ?? Array.Empty<string>();

    public IReadOnlyList<PositionNeed> MatchedTeamNeeds => MatchedNeeds ?? Array.Empty<PositionNeed>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("Trade evaluation explanation is required.", nameof(Explanation));
        }

        if (Reasons.Count == 0 || Reasons.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Trade evaluation requires readable reasons.", nameof(Reasons));
        }

        if (StaffReactionNotes.Any(string.IsNullOrWhiteSpace) || PlayerReactionNotes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Trade evaluation reaction notes must be readable.");
        }
    }
}
