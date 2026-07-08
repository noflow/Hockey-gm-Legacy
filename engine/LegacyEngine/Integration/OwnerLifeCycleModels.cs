namespace LegacyEngine.Integration;

public enum OwnerLifeStage
{
    NewOwner,
    EstablishedOwner,
    PatientBuilder,
    PressureOwner,
    ChampionshipOwner,
    DecliningInterest,
    TransitionPlanning,
    FormerOwner
}

public enum OwnerMilestoneType
{
    OwnershipStarted,
    GmHired,
    ExpectationsSet,
    BudgetReviewed,
    ConfidenceChanged,
    JobSecurityChanged,
    OwnerLetterSent,
    OwnerMeetingHeld,
    PhilosophyChanged,
    RebuildApproved,
    ContenderEraStarted
}

public enum OwnerTrend
{
    Rising,
    Stable,
    Falling,
    Critical
}

public enum OwnerExpectationResult
{
    NotStarted,
    OnTrack,
    Met,
    Missed,
    Mixed
}

public sealed record OwnerMilestone(
    string MilestoneId,
    string OwnerId,
    string OwnerName,
    OwnerMilestoneType MilestoneType,
    DateOnly Date,
    int SeasonYear,
    string Summary,
    bool IsNotable)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MilestoneId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || string.IsNullOrWhiteSpace(OwnerName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Owner milestone requires identity and summary.");
        }

        if (SeasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Owner milestone season year must be positive.");
        }
    }
}

public sealed record OwnerExpectationHistoryRecord(
    string ExpectationHistoryId,
    int SeasonYear,
    OwnerExpectationType ExpectationType,
    int Difficulty,
    int Priority,
    OwnerExpectationResult Result,
    int Progress,
    string OwnerReaction,
    string GmPerformanceSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ExpectationHistoryId)
            || string.IsNullOrWhiteSpace(OwnerReaction)
            || string.IsNullOrWhiteSpace(GmPerformanceSummary))
        {
            throw new ArgumentException("Owner expectation history requires identity and readable result.");
        }

        if (SeasonYear < 1 || Difficulty is < 1 or > 5 || Priority is < 1 or > 5 || Progress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Owner expectation history values are out of range.");
        }
    }
}

public sealed record OwnerConfidenceHistoryRecord(
    string ConfidenceHistoryId,
    DateOnly Date,
    int Confidence,
    int Trust,
    int Patience,
    int Pressure,
    int BudgetSupport,
    JobSecurityLevel JobSecurity,
    OwnerTrend Trend,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConfidenceHistoryId) || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Owner confidence history requires id and reason.");
        }

        foreach (var value in new[] { Confidence, Trust, Patience, Pressure, BudgetSupport })
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(Confidence), "Owner confidence history values must be between zero and one hundred.");
            }
        }
    }
}

public sealed record OwnerMeetingHistoryRecord(
    string MeetingHistoryId,
    OwnerMeetingType MeetingType,
    DateOnly Date,
    string Topic,
    string OwnerMessage,
    string GmResponsePlaceholder,
    string Outcome,
    int ConfidenceImpact)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MeetingHistoryId)
            || string.IsNullOrWhiteSpace(Topic)
            || string.IsNullOrWhiteSpace(OwnerMessage)
            || string.IsNullOrWhiteSpace(GmResponsePlaceholder)
            || string.IsNullOrWhiteSpace(Outcome))
        {
            throw new ArgumentException("Owner meeting history requires readable meeting context.");
        }

        if (ConfidenceImpact is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ConfidenceImpact), "Owner meeting confidence impact must be between -100 and 100.");
        }
    }
}

public sealed record OwnerJobSecurityHistoryRecord(
    string JobSecurityHistoryId,
    DateOnly Date,
    JobSecurityLevel Level,
    int Score,
    OwnerTrend Trend,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(JobSecurityHistoryId) || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Owner job security history requires id and reason.");
        }

        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Owner job security score must be between zero and one hundred.");
        }
    }
}

public sealed record OwnerLegacyProfile(
    string OwnerId,
    string OwnerName,
    int TenureYears,
    string PhilosophyEra,
    string BudgetEra,
    string GmRelationshipEra,
    string CompetitiveEra,
    string LegacySummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OwnerId)
            || string.IsNullOrWhiteSpace(OwnerName)
            || string.IsNullOrWhiteSpace(PhilosophyEra)
            || string.IsNullOrWhiteSpace(BudgetEra)
            || string.IsNullOrWhiteSpace(GmRelationshipEra)
            || string.IsNullOrWhiteSpace(CompetitiveEra)
            || string.IsNullOrWhiteSpace(LegacySummary))
        {
            throw new ArgumentException("Owner legacy profile requires readable era context.");
        }

        if (TenureYears < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TenureYears), "Owner tenure cannot be negative.");
        }
    }
}

public sealed record OwnerCareerState(
    string OwnerId,
    string OwnerName,
    OwnerLifeStage LifeStage,
    OwnerPersonalityType CurrentPersonality,
    OwnerTrend ConfidenceTrend,
    int TenureYears,
    int Trust,
    int Confidence,
    int Patience,
    int Pressure,
    JobSecurityLevel JobSecurity,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OwnerId)
            || string.IsNullOrWhiteSpace(OwnerName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Owner career state requires identity and summary.");
        }

        if (TenureYears < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TenureYears), "Owner tenure cannot be negative.");
        }

        foreach (var value in new[] { Trust, Confidence, Patience, Pressure })
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(Trust), "Owner career state values must be between zero and one hundred.");
            }
        }
    }
}

public sealed record OwnerCareerSummary(
    string OwnerId,
    string OwnerName,
    OwnerLifeStage LifeStage,
    OwnerPersonalityType CurrentPersonality,
    OwnerTrend ConfidenceTrend,
    string CareerSummaryText,
    IReadOnlyList<OwnerExpectationHistoryRecord> ExpectationHistory,
    IReadOnlyList<OwnerConfidenceHistoryRecord> ConfidenceHistory,
    IReadOnlyList<OwnerMeetingHistoryRecord> MeetingHistory,
    IReadOnlyList<OwnerLetter> Letters,
    IReadOnlyList<OwnerJobSecurityHistoryRecord> JobSecurityHistory,
    IReadOnlyList<OwnerMilestone> Milestones,
    OwnerLegacyProfile LegacyProfile,
    string BudgetRelationship,
    string PersonalityEvolution,
    string OrganizationHistorySummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OwnerId)
            || string.IsNullOrWhiteSpace(OwnerName)
            || string.IsNullOrWhiteSpace(CareerSummaryText)
            || string.IsNullOrWhiteSpace(BudgetRelationship)
            || string.IsNullOrWhiteSpace(PersonalityEvolution)
            || string.IsNullOrWhiteSpace(OrganizationHistorySummary))
        {
            throw new ArgumentException("Owner career summary requires readable owner context.");
        }

        foreach (var item in ExpectationHistory)
        {
            item.Validate();
        }

        foreach (var item in ConfidenceHistory)
        {
            item.Validate();
        }

        foreach (var item in MeetingHistory)
        {
            item.Validate();
        }

        foreach (var letter in Letters)
        {
            letter.Validate();
        }

        foreach (var item in JobSecurityHistory)
        {
            item.Validate();
        }

        foreach (var milestone in Milestones)
        {
            milestone.Validate();
        }

        LegacyProfile.Validate();
    }
}
