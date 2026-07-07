namespace LegacyEngine.Integration;

public sealed record StaffMeetingReport(
    DateOnly MeetingDate,
    string HeadCoachName,
    string Summary,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> DevelopmentNotes,
    IReadOnlyList<string> RosterNotes,
    IReadOnlyList<string> MedicalNotes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HeadCoachName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff meeting report requires coach and summary text.");
        }

        if (Recommendations.Count == 0)
        {
            throw new ArgumentException("Staff meeting report requires recommendations.", nameof(Recommendations));
        }
    }
}
