namespace LegacyEngine.Communication;

public sealed record Rumor(
    string RumorId,
    DateOnly CreatedOn,
    string Subject,
    string Body,
    int Reliability,
    CommunicationVisibility Visibility,
    string? SubjectPersonId = null,
    string? OrganizationId = null,
    string? SourceEventId = null,
    string? OriginId = null)
{
    public RumorConfidence Confidence => ConfidenceFor(Reliability);

    public bool IsConfirmed => Confidence == RumorConfidence.Confirmed;

    public static Rumor Create(
        string rumorId,
        DateOnly createdOn,
        string subject,
        string body,
        int reliability,
        CommunicationVisibility visibility,
        string? subjectPersonId = null,
        string? organizationId = null,
        string? sourceEventId = null,
        string? originId = null)
    {
        var rumor = new Rumor(
            rumorId,
            createdOn,
            subject,
            body,
            ClampReliability(reliability),
            visibility,
            subjectPersonId,
            organizationId,
            sourceEventId,
            originId);
        rumor.Validate();
        return rumor;
    }

    public Rumor Corroborate(int reliabilityDelta) =>
        this with { Reliability = ClampReliability(Reliability + reliabilityDelta) };

    public static int ClampReliability(int value) => Math.Clamp(value, 0, 100);

    public static RumorConfidence ConfidenceFor(int reliability) => reliability switch
    {
        >= 90 => RumorConfidence.Confirmed,
        >= 70 => RumorConfidence.Reliable,
        >= 50 => RumorConfidence.Credible,
        >= 25 => RumorConfidence.Speculative,
        _ => RumorConfidence.Baseless
    };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RumorId))
        {
            throw new ArgumentException("Rumor id is required.", nameof(RumorId));
        }

        if (string.IsNullOrWhiteSpace(Subject))
        {
            throw new ArgumentException("Rumor subject is required.", nameof(Subject));
        }

        if (string.IsNullOrWhiteSpace(Body))
        {
            throw new ArgumentException("Rumor body is required.", nameof(Body));
        }

        if (Reliability is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Reliability), "Rumor reliability must be between 0 and 100.");
        }
    }
}
