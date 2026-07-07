namespace LegacyEngine.Integration;

public sealed record DeadlineRumor(
    string RumorId,
    DateOnly Date,
    string TeamName,
    string Summary,
    DeadlineRumorConfidence Confidence)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RumorId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Deadline rumor requires id, team, and summary.");
        }
    }
}
