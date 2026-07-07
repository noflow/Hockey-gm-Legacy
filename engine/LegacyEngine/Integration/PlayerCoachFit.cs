namespace LegacyEngine.Integration;

public sealed record PlayerCoachFit(
    string PersonId,
    string PlayerName,
    string CoachPersonId,
    string CoachName,
    CoachPlayerFitGrade FitGrade,
    string Position,
    string Summary,
    IReadOnlyList<string> Reasons)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(CoachPersonId)
            || string.IsNullOrWhiteSpace(CoachName)
            || string.IsNullOrWhiteSpace(Position)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player coach fit requires identity and summary text.");
        }

        if (Reasons.Count == 0)
        {
            throw new ArgumentException("Player coach fit requires reasons.", nameof(Reasons));
        }
    }
}
