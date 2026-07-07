namespace LegacyEngine.Integration;

public sealed record OwnerOfficeSummary(
    OwnerPersonalityProfile Personality,
    IReadOnlyList<OwnerExpectation> Expectations,
    OwnerConfidenceState Confidence,
    JobSecurityAssessment JobSecurity,
    IReadOnlyList<OwnerMeeting> Meetings,
    IReadOnlyList<OwnerLetter> Letters,
    IReadOnlyList<OwnerDecision> Decisions,
    OwnerPerformanceReview PerformanceReview)
{
    public void Validate()
    {
        Personality.Validate();
        Confidence.Validate();
        JobSecurity.Validate();
        PerformanceReview.Validate();

        foreach (var expectation in Expectations)
        {
            expectation.Validate();
        }

        foreach (var meeting in Meetings)
        {
            meeting.Validate();
        }

        foreach (var letter in Letters)
        {
            letter.Validate();
        }

        foreach (var decision in Decisions)
        {
            decision.Validate();
        }
    }
}
