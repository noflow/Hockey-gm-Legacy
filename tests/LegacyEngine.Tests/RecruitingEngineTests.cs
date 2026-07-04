using LegacyEngine.Events;
using LegacyEngine.Recruiting;
using LegacyEngine.Scouting;

internal sealed class RecruitingEngineTests
{
    public void RecruitProfileCreation()
    {
        var profile = BuildProfile();

        profile.Validate();
        Assert.Equal("person-recruit-001", profile.RecruitPersonId);
        Assert.Equal(9, profile.Priorities.Count);
        Assert.Equal(95, profile.Priorities[RecruitPriority.Development]);
        Assert.Equal(0, profile.Interests.Count);
        Assert.Equal(0, profile.Pitches.Count);
        Assert.Equal(0, profile.Promises.Count);
        Assert.Equal(0, profile.Visits.Count);
    }

    public void RecruitStartsAsAvailable()
    {
        Assert.Equal(RecruitStatus.Available, BuildProfile().Status);
    }

    public void InterestCanIncreaseAndDecrease()
    {
        var engine = new RecruitingEngine();
        var profile = BuildProfile();

        profile = engine.ChangeInterest(profile, "org-1", 35, new DateOnly(2026, 9, 1));
        Assert.Equal(RecruitStatus.Interested, profile.Status);
        Assert.Equal(35, profile.GetInterest("org-1"));

        profile = engine.ChangeInterest(profile, "org-1", -20, new DateOnly(2026, 9, 2));
        Assert.Equal(15, profile.GetInterest("org-1"));

        profile = engine.ChangeInterest(profile, "org-1", -50, new DateOnly(2026, 9, 3));
        Assert.Equal(0, profile.GetInterest("org-1"));
    }

    public void OfferChangesStatusToOffered()
    {
        var eventEngine = new EventEngine();
        var engine = new RecruitingEngine(eventEngine);
        var profile = engine.SubmitOffer(BuildProfile(), "org-1", new DateOnly(2026, 9, 5));

        Assert.Equal(RecruitStatus.Offered, profile.Status);
        Assert.Equal(10, profile.GetInterest("org-1"));
        Assert.Equal(1, eventEngine.Queue.Count);
        Assert.Equal(LegacyEventType.RecruitingOfferSubmitted, eventEngine.Queue.PendingEvents[0].EventType);
    }

    public void VisitCanBeRecorded()
    {
        var engine = new RecruitingEngine();
        var profile = engine.RecordVisit(BuildProfile(), BuildVisit(fitScore: 88));

        Assert.Equal(1, profile.Visits.Count);
        Assert.Equal("visit-001", profile.Visits[0].VisitId);
        Assert.True(profile.GetInterest("org-1") > 0, "A strong visit should increase interest.");
    }

    public void PromiseCanBeAdded()
    {
        var engine = new RecruitingEngine();
        var profile = engine.AddPromise(BuildProfile(), BuildPromise(RecruitingPromiseType.DevelopmentPlan, 90));

        Assert.Equal(1, profile.Promises.Count);
        Assert.Equal(RecruitingPromiseType.DevelopmentPlan, profile.Promises[0].PromiseType);
        Assert.True(profile.GetInterest("org-1") > 0, "A meaningful promise should increase interest.");
    }

    public void DecisionCanCommit()
    {
        var engine = new RecruitingEngine();
        var profile = BuildStrongRecruitingProfile(engine);

        var result = engine.MakeDecision(
            profile,
            "org-1",
            new DateOnly(2026, 10, 1),
            organizationFit: 90,
            relationshipTrust: 85,
            scoutingConfidence: ScoutingConfidenceLevel.High);

        Assert.Equal(RecruitingDecision.Committed, result.Decision);
        Assert.Equal(RecruitStatus.Committed, result.ResultingStatus);
        Assert.Equal(RecruitStatus.Committed, result.UpdatedProfile.Status);
        Assert.True(result.DecisionScore >= 65, "Committed decision should meet the commitment threshold.");
    }

    public void DecisionCanReject()
    {
        var engine = new RecruitingEngine();
        var profile = engine.SubmitOffer(BuildProfile(), "org-1", new DateOnly(2026, 9, 5));

        var result = engine.MakeDecision(
            profile,
            "org-1",
            new DateOnly(2026, 10, 1),
            organizationFit: 20,
            relationshipTrust: 25,
            scoutingConfidence: ScoutingConfidenceLevel.Low);

        Assert.Equal(RecruitingDecision.Rejected, result.Decision);
        Assert.Equal(RecruitStatus.Rejected, result.ResultingStatus);
        Assert.True(result.DecisionScore < 65, "Rejected decision should fall below the commitment threshold.");
    }

    public void HigherInterestImprovesDecisionScore()
    {
        var lowEngine = new RecruitingEngine();
        var highEngine = new RecruitingEngine();
        var lowInterestProfile = lowEngine.SubmitOffer(BuildProfile(), "org-1", new DateOnly(2026, 9, 5));
        var highInterestProfile = highEngine.ChangeInterest(BuildProfile(), "org-1", 75, new DateOnly(2026, 9, 1));
        highInterestProfile = highEngine.SubmitOffer(highInterestProfile, "org-1", new DateOnly(2026, 9, 5));

        var lowResult = lowEngine.MakeDecision(lowInterestProfile, "org-1", new DateOnly(2026, 10, 1), 55, 50, ScoutingConfidenceLevel.Medium);
        var highResult = highEngine.MakeDecision(highInterestProfile, "org-1", new DateOnly(2026, 10, 1), 55, 50, ScoutingConfidenceLevel.Medium);

        Assert.True(highResult.DecisionScore > lowResult.DecisionScore, "Higher interest should improve decision score.");
    }

    public void PromisesInfluenceDecision()
    {
        var noPromiseEngine = new RecruitingEngine();
        var promiseEngine = new RecruitingEngine();
        var noPromiseProfile = noPromiseEngine.SubmitOffer(
            noPromiseEngine.ChangeInterest(BuildProfile(), "org-1", 40, new DateOnly(2026, 9, 1)),
            "org-1",
            new DateOnly(2026, 9, 5));
        var promiseProfile = promiseEngine.SubmitOffer(
            promiseEngine.ChangeInterest(BuildProfile(), "org-1", 40, new DateOnly(2026, 9, 1)),
            "org-1",
            new DateOnly(2026, 9, 5));
        promiseProfile = promiseEngine.AddPromise(promiseProfile, BuildPromise(RecruitingPromiseType.IceTime, 95));
        promiseProfile = promiseEngine.AddPromise(promiseProfile, BuildPromise(RecruitingPromiseType.EducationSupport, 90, "promise-education"));

        var noPromiseResult = noPromiseEngine.MakeDecision(noPromiseProfile, "org-1", new DateOnly(2026, 10, 1), 55, 50, ScoutingConfidenceLevel.Medium);
        var promiseResult = promiseEngine.MakeDecision(promiseProfile, "org-1", new DateOnly(2026, 10, 1), 55, 50, ScoutingConfidenceLevel.Medium);

        Assert.True(promiseResult.DecisionScore > noPromiseResult.DecisionScore, "Promises should improve decision score.");
    }

    public void EventsCreatedForOfferCommitAndReject()
    {
        var eventEngine = new EventEngine();
        var engine = new RecruitingEngine(eventEngine);
        var committedProfile = BuildStrongRecruitingProfile(engine);
        var commitResult = engine.MakeDecision(committedProfile, "org-1", new DateOnly(2026, 10, 1), 90, 85, ScoutingConfidenceLevel.High);
        var rejectedProfile = engine.SubmitOffer(BuildProfile("person-recruit-002"), "org-2", new DateOnly(2026, 9, 5));
        var rejectResult = engine.MakeDecision(rejectedProfile, "org-2", new DateOnly(2026, 10, 2), 20, 20, ScoutingConfidenceLevel.Low);

        Assert.Equal(4, eventEngine.Queue.Count);
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.RecruitingOfferSubmitted), "Offer event should be queued.");
        Assert.Equal(LegacyEventType.RecruitCommitted, commitResult.CreatedEvent.EventType);
        Assert.Equal(LegacyEventType.RecruitRejected, rejectResult.CreatedEvent.EventType);
    }

    public void NoRosterModificationOccurs()
    {
        var roster = new List<string> { "player-a", "player-b" };
        var engine = new RecruitingEngine();
        var profile = BuildStrongRecruitingProfile(engine);

        var result = engine.MakeDecision(profile, "org-1", new DateOnly(2026, 10, 1), 90, 85, ScoutingConfidenceLevel.High);

        Assert.Equal(RecruitingDecision.Committed, result.Decision);
        Assert.Equal(2, roster.Count);
        Assert.Equal("player-a", roster[0]);
        Assert.Equal("player-b", roster[1]);
    }

    private static RecruitProfile BuildStrongRecruitingProfile(RecruitingEngine engine)
    {
        var profile = engine.ChangeInterest(BuildProfile(), "org-1", 70, new DateOnly(2026, 9, 1));
        profile = engine.SubmitPitch(profile, BuildPitch());
        profile = engine.AddPromise(profile, BuildPromise(RecruitingPromiseType.DevelopmentPlan, 95));
        profile = engine.AddPromise(profile, BuildPromise(RecruitingPromiseType.EducationSupport, 90, "promise-education"));
        profile = engine.RecordVisit(profile, BuildVisit(fitScore: 88));
        return engine.SubmitOffer(profile, "org-1", new DateOnly(2026, 9, 5));
    }

    private static RecruitProfile BuildProfile(string personId = "person-recruit-001") =>
        new RecruitingEngine().CreateProfile(personId, new Dictionary<RecruitPriority, int>
        {
            [RecruitPriority.IceTime] = 90,
            [RecruitPriority.Development] = 95,
            [RecruitPriority.Education] = 82,
            [RecruitPriority.Winning] = 60,
            [RecruitPriority.DistanceFromHome] = 70,
            [RecruitPriority.Facilities] = 75,
            [RecruitPriority.Coaching] = 88,
            [RecruitPriority.PathwayToHigherHockey] = 92,
            [RecruitPriority.FamilyComfort] = 85
        });

    private static RecruitingPitch BuildPitch() =>
        new(
            PitchId: "pitch-001",
            OrganizationId: "org-1",
            Date: new DateOnly(2026, 9, 2),
            PriorityFits: new Dictionary<RecruitPriority, int>
            {
                [RecruitPriority.IceTime] = 90,
                [RecruitPriority.Development] = 94,
                [RecruitPriority.Education] = 86,
                [RecruitPriority.FamilyComfort] = 80,
                [RecruitPriority.PathwayToHigherHockey] = 90
            },
            Message: "Clear development plan with education support and family comfort.");

    private static RecruitingPromise BuildPromise(
        RecruitingPromiseType promiseType,
        int strength,
        string promiseId = "promise-001") =>
        new(
            PromiseId: promiseId,
            OrganizationId: "org-1",
            PromiseType: promiseType,
            Strength: strength,
            Date: new DateOnly(2026, 9, 3),
            Description: "Specific recruiting promise tied to the player's priorities.");

    private static RecruitingVisit BuildVisit(int fitScore = 85) =>
        new(
            VisitId: "visit-001",
            OrganizationId: "org-1",
            Date: new DateOnly(2026, 9, 4),
            FitScore: fitScore,
            Notes: "Family visit went well and the player liked the facilities.");
}
