namespace LegacyEngine.Integration;

public sealed record OwnerMeeting(
    string MeetingId,
    OwnerMeetingType MeetingType,
    DateOnly ScheduledDate,
    string OwnerComments,
    IReadOnlyList<string> GmResponseOptions,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<OwnerExpectation> FutureExpectations,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MeetingId)
            || string.IsNullOrWhiteSpace(OwnerComments)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Owner meeting requires id, comments, and summary.");
        }

        if (GmResponseOptions.Count == 0 || Recommendations.Count == 0)
        {
            throw new ArgumentException("Owner meeting requires GM response options and recommendations.");
        }

        foreach (var expectation in FutureExpectations)
        {
            expectation.Validate();
        }
    }
}
