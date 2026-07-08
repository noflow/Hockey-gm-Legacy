namespace LegacyEngine.Integration;

public enum ExpandedRelationshipType
{
    GmPlayer,
    GmStaff,
    GmOwner,
    GmAgent,
    PlayerCoach,
    PlayerStaff,
    PlayerPlayer,
    StaffStaff,
    StaffOwner,
    OrganizationAgent,
    OrganizationPlayer,
    OrganizationStaff,
    OrganizationOrganization
}

public enum ExpandedRelationshipTrend
{
    Rising,
    Stable,
    Falling,
    Strained,
    Recovering
}

public enum RelationshipChangeTrigger
{
    Signing,
    RejectedOffer,
    Trade,
    Release,
    BrokenPromise,
    FulfilledPromise,
    Promotion,
    Demotion,
    IceTime,
    InjuryHandling,
    StaffConflict,
    OwnerMeeting,
    ScoutingSuccess,
    DevelopmentSuccess
}

public enum RelationshipConflictType
{
    MinorTension,
    PersonalityClash,
    TrustIssue,
    RoleFrustration,
    BrokenPromise,
    StaffDisagreement
}

public enum RelationshipChemistryLevel
{
    Excellent,
    Good,
    Neutral,
    Poor,
    Problem
}

public enum RelationshipImpactArea
{
    Contracts,
    FreeAgency,
    Trades,
    StaffHiring,
    PlayerDevelopment,
    Morale,
    OwnerConfidence,
    AgentNegotiation,
    CoachingFit,
    ScoutingTrust
}

public sealed record RelationshipChangeRecord(
    string ChangeId,
    string RelationshipProfileId,
    RelationshipChangeTrigger Trigger,
    DateOnly Date,
    string Reason,
    int Amount,
    string? RelatedEventId,
    string VisibleExplanation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ChangeId) || string.IsNullOrWhiteSpace(RelationshipProfileId))
        {
            throw new ArgumentException("Relationship change requires ids.");
        }

        if (string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(VisibleExplanation))
        {
            throw new ArgumentException("Relationship change requires a reason and visible explanation.");
        }
    }
}

public sealed record RelationshipConflict(
    string ConflictId,
    string RelationshipProfileId,
    RelationshipConflictType ConflictType,
    DateOnly Date,
    int Severity,
    string Reason,
    string VisibleExplanation,
    bool IsMajor,
    bool IsActive = true)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConflictId) || string.IsNullOrWhiteSpace(RelationshipProfileId))
        {
            throw new ArgumentException("Relationship conflict requires ids.");
        }

        if (Severity is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), "Conflict severity must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(VisibleExplanation))
        {
            throw new ArgumentException("Relationship conflict requires readable context.");
        }
    }
}

public sealed record ExpandedRelationshipProfile(
    string RelationshipProfileId,
    ExpandedRelationshipType RelationshipType,
    string SourceId,
    string SourceName,
    string TargetId,
    string TargetName,
    int Trust,
    int Respect,
    int Loyalty,
    int Conflict,
    int CommunicationQuality,
    ExpandedRelationshipTrend Trend,
    IReadOnlyList<string> History,
    IReadOnlyList<string> KeyMoments,
    string Summary)
{
    public int OverallScore => Math.Clamp((Trust + Respect + Loyalty + CommunicationQuality + (100 - Conflict)) / 5, 0, 100);

    public string Label =>
        OverallScore >= 78 ? "Excellent" :
        OverallScore >= 63 ? "Good" :
        OverallScore >= 45 ? "Neutral" :
        OverallScore >= 30 ? "Poor" :
        "Problem";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RelationshipProfileId)
            || string.IsNullOrWhiteSpace(SourceId)
            || string.IsNullOrWhiteSpace(TargetId)
            || string.IsNullOrWhiteSpace(SourceName)
            || string.IsNullOrWhiteSpace(TargetName))
        {
            throw new ArgumentException("Expanded relationship profile requires ids and names.");
        }

        ValidateScore(Trust, nameof(Trust));
        ValidateScore(Respect, nameof(Respect));
        ValidateScore(Loyalty, nameof(Loyalty));
        ValidateScore(Conflict, nameof(Conflict));
        ValidateScore(CommunicationQuality, nameof(CommunicationQuality));

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Relationship profile summary is required.", nameof(Summary));
        }
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Relationship scores must be between 0 and 100.");
        }
    }
}

public sealed record RelationshipChemistrySummary(
    RelationshipChemistryLevel RosterChemistry,
    RelationshipChemistryLevel StaffChemistry,
    RelationshipChemistryLevel ScoutingDepartmentChemistry,
    RelationshipChemistryLevel CoachPlayerFit,
    RelationshipChemistryLevel GmOfficeRelationship,
    IReadOnlyList<string> SummaryLines)
{
    public void Validate()
    {
        if (SummaryLines.Count == 0 || SummaryLines.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Relationship chemistry summary requires readable lines.", nameof(SummaryLines));
        }
    }
}

public sealed record RelationshipImpactSummary(
    RelationshipImpactArea ImpactArea,
    int Modifier,
    string Explanation)
{
    public void Validate()
    {
        if (Modifier is < -20 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(Modifier), "Relationship impact modifier must be modest.");
        }

        if (string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("Relationship impact requires an explanation.", nameof(Explanation));
        }
    }
}
