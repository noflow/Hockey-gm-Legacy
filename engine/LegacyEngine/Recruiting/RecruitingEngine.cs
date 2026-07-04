using LegacyEngine.Events;
using LegacyEngine.Scouting;

namespace LegacyEngine.Recruiting;

public sealed class RecruitingEngine
{
    private readonly EventEngine _eventEngine;

    public RecruitingEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public RecruitProfile CreateProfile(string recruitPersonId, IReadOnlyDictionary<RecruitPriority, int> priorities) =>
        RecruitProfile.Create(recruitPersonId, priorities);

    public RecruitProfile ChangeInterest(RecruitProfile profile, string organizationId, int amount, DateOnly date)
    {
        profile.Validate();
        return profile.ChangeInterest(organizationId, amount, date);
    }

    public RecruitProfile SubmitPitch(RecruitProfile profile, RecruitingPitch pitch)
    {
        profile.Validate();
        return profile.AddPitch(pitch);
    }

    public RecruitProfile AddPromise(RecruitProfile profile, RecruitingPromise promise)
    {
        profile.Validate();
        return profile.AddPromise(promise);
    }

    public RecruitProfile RecordVisit(RecruitProfile profile, RecruitingVisit visit)
    {
        profile.Validate();
        return profile.AddVisit(visit);
    }

    public RecruitProfile SubmitOffer(RecruitProfile profile, string organizationId, DateOnly date)
    {
        profile.Validate();

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        var offeredProfile = profile.ChangeInterest(organizationId, 10, date).SetStatus(RecruitStatus.Offered);
        QueueRecruitingEvent(
            offeredProfile,
            organizationId,
            date,
            LegacyEventType.RecruitingOfferSubmitted,
            "Recruiting offer submitted",
            "A recruiting offer was submitted.",
            new Dictionary<string, object?> { ["status"] = offeredProfile.Status.ToString() });

        return offeredProfile;
    }

    public RecruitingDecisionResult MakeDecision(
        RecruitProfile profile,
        string organizationId,
        DateOnly date,
        int organizationFit,
        int? relationshipTrust = null,
        ScoutingConfidenceLevel? scoutingConfidence = null)
    {
        profile.Validate();

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        ValidateScore(organizationFit, nameof(organizationFit));
        if (relationshipTrust.HasValue)
        {
            ValidateScore(relationshipTrust.Value, nameof(relationshipTrust));
        }

        var interest = profile.GetInterest(organizationId);
        var promiseScore = CalculatePromiseScore(profile, organizationId);
        var visitScore = CalculateVisitScore(profile, organizationId);
        var scoutingScore = scoutingConfidence.HasValue ? ToScoutingScore(scoutingConfidence.Value) : 50;
        var trustScore = relationshipTrust ?? 50;
        var offerBonus = profile.Status == RecruitStatus.Offered ? 8 : -8;
        var decisionScore = ClampScore((int)Math.Round(
            (interest * 0.35)
            + (promiseScore * 0.20)
            + (organizationFit * 0.20)
            + (trustScore * 0.15)
            + (scoutingScore * 0.05)
            + (visitScore * 0.05)
            + offerBonus));

        var decision = decisionScore >= 65 ? RecruitingDecision.Committed : RecruitingDecision.Rejected;
        var status = decision == RecruitingDecision.Committed ? RecruitStatus.Committed : RecruitStatus.Rejected;
        var updatedProfile = profile.SetStatus(status);
        var eventType = decision == RecruitingDecision.Committed
            ? LegacyEventType.RecruitCommitted
            : LegacyEventType.RecruitRejected;
        var legacyEvent = QueueRecruitingEvent(
            updatedProfile,
            organizationId,
            date,
            eventType,
            decision == RecruitingDecision.Committed ? "Recruit committed" : "Recruit rejected offer",
            decision == RecruitingDecision.Committed
                ? "A recruit committed to the organization."
                : "A recruit rejected the organization.",
            new Dictionary<string, object?>
            {
                ["decision_score"] = decisionScore,
                ["interest"] = interest,
                ["promise_score"] = promiseScore,
                ["organization_fit"] = organizationFit,
                ["relationship_trust"] = trustScore,
                ["scouting_confidence"] = scoutingConfidence?.ToString(),
                ["visit_score"] = visitScore
            });

        return new RecruitingDecisionResult(
            RecruitPersonId: profile.RecruitPersonId,
            OrganizationId: organizationId,
            Decision: decision,
            ResultingStatus: status,
            DecisionScore: decisionScore,
            Message: decision == RecruitingDecision.Committed
                ? "Recruit committed."
                : "Recruit rejected the opportunity.",
            UpdatedProfile: updatedProfile,
            CreatedEvent: legacyEvent,
            Details: legacyEvent.Metadata);
    }

    private LegacyEvent QueueRecruitingEvent(
        RecruitProfile profile,
        string organizationId,
        DateOnly date,
        LegacyEventType eventType,
        string title,
        string description,
        IReadOnlyDictionary<string, object?> metadata)
    {
        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: profile.RecruitPersonId,
                OrganizationId: organizationId),
            metadata);

        return _eventEngine.QueueEvent(legacyEvent);
    }

    private static int CalculatePromiseScore(RecruitProfile profile, string organizationId)
    {
        var promises = profile.Promises.Where(item => item.OrganizationId == organizationId).ToArray();
        return promises.Length == 0 ? 0 : (int)Math.Round(promises.Average(item => item.Strength));
    }

    private static int CalculateVisitScore(RecruitProfile profile, string organizationId)
    {
        var visits = profile.Visits.Where(item => item.OrganizationId == organizationId).ToArray();
        return visits.Length == 0 ? 50 : (int)Math.Round(visits.Average(item => item.FitScore));
    }

    private static int ToScoutingScore(ScoutingConfidenceLevel confidence) =>
        confidence switch
        {
            ScoutingConfidenceLevel.VeryHigh => 90,
            ScoutingConfidenceLevel.High => 75,
            ScoutingConfidenceLevel.Medium => 60,
            ScoutingConfidenceLevel.Low => 40,
            _ => 25
        };

    private static int ClampScore(int value) => Math.Clamp(value, 0, 100);

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Recruiting scores must be between 0 and 100.");
        }
    }
}
