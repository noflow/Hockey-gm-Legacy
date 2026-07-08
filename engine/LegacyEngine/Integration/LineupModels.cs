using LegacyEngine.Development;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum LineupRole
{
    FranchiseForward,
    FirstLineForward,
    TopSixForward,
    MiddleSixForward,
    CheckingLineForward,
    FourthLineForward,
    DepthForward,
    ProspectForward,
    FranchiseDefenseman,
    TopPairDefenseman,
    SecondPairDefenseman,
    ThirdPairDefenseman,
    DepthDefenseman,
    ProspectDefenseman,
    FranchiseGoalie,
    StartingGoalie,
    TandemGoalie,
    BackupGoalie,
    DepthGoalie,
    ProspectGoalie
}

public enum LineupSlot
{
    Line1LW,
    Line1C,
    Line1RW,
    Line2LW,
    Line2C,
    Line2RW,
    Line3LW,
    Line3C,
    Line3RW,
    Line4LW,
    Line4C,
    Line4RW,
    Pair1LD,
    Pair1RD,
    Pair2LD,
    Pair2RD,
    Pair3LD,
    Pair3RD,
    Starter,
    Backup,
    HealthyScratch
}

public enum LineupPosition
{
    LeftWing,
    Center,
    RightWing,
    LeftDefense,
    RightDefense,
    StarterGoalie,
    BackupGoalie,
    HealthyScratch
}

public enum LineupPromiseStatus
{
    NotYetEvaluated,
    Kept,
    AtRisk,
    Broken
}

public enum LineupRoleSatisfaction
{
    Satisfied,
    Neutral,
    Frustrated,
    VeryFrustrated
}

public enum CoachLineupRecommendationType
{
    PromotePlayer,
    DemotePlayer,
    MoveProspectToBiggerRole,
    ShelterYoungPlayer,
    UseCheckingLinePlayerDefensively,
    ImproveTopSixScoring,
    UpgradeTopPairDefense
}

public sealed record PlayerRolePromise(
    string PersonId,
    string PlayerName,
    LineupRole PromisedRole,
    DateOnly PromisedOn,
    string Source,
    LineupPromiseStatus Status = LineupPromiseStatus.NotYetEvaluated,
    string Summary = "Role promise has not been evaluated yet.")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Source)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player role promise requires readable context.");
        }
    }
}

public sealed record PlayerRoleExpectation(
    string PersonId,
    string PlayerName,
    LineupRole ExpectedRole,
    LineupRole CoachRecommendedRole,
    LineupRole PotentialRole,
    LineupRoleSatisfaction Satisfaction,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player role expectation requires readable context.");
        }
    }
}

public sealed record PlayerLineupUsage(
    string PersonId,
    string PlayerName,
    LineupSlot Slot,
    LineupRole CurrentRole,
    LineupRole ExpectedRole,
    LineupRole CoachRecommendedRole,
    LineupRole PotentialRole,
    LineupPromiseStatus PromiseStatus,
    LineupRoleSatisfaction Satisfaction,
    string DevelopmentImpactNote,
    string MoraleNote)
{
    public string SlotLabel => LineupDisplay.SlotLabel(Slot);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(DevelopmentImpactNote)
            || string.IsNullOrWhiteSpace(MoraleNote))
        {
            throw new ArgumentException("Player lineup usage requires readable context.");
        }
    }
}

public sealed record LineupValidationResult(
    bool IsValid,
    IReadOnlyList<string> Warnings,
    string Message)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Lineup validation requires a message.");
        }
    }
}

public sealed record LineupManagementResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    LineupValidationResult Validation,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Validation.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Lineup management result requires a message.");
        }
    }
}

public sealed record LineupRoleAssignment(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    string ShootsCatches,
    int? Age,
    string PlayerType,
    LineupRole CurrentRole,
    LineupRole PotentialRole,
    LineupSlot Slot,
    DevelopmentStage? DevelopmentStage,
    string ContractStatus,
    string CoachNote)
{
    public string SlotLabel => LineupDisplay.SlotLabel(Slot);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(PlayerType)
            || string.IsNullOrWhiteSpace(ContractStatus)
            || string.IsNullOrWhiteSpace(CoachNote))
        {
            throw new ArgumentException("Lineup role assignment requires readable player context.");
        }
    }
}

public sealed record ForwardLine(int LineNumber, LineupRoleAssignment? LeftWing, LineupRoleAssignment? Center, LineupRoleAssignment? RightWing)
{
    public void Validate()
    {
        if (LineNumber is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(LineNumber), "Forward line number must be 1-4.");
        }
    }
}

public sealed record DefensePair(int PairNumber, LineupRoleAssignment? LeftDefense, LineupRoleAssignment? RightDefense)
{
    public void Validate()
    {
        if (PairNumber is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(PairNumber), "Defense pair number must be 1-3.");
        }
    }
}

public sealed record GoalieDepth(LineupRoleAssignment? Starter, LineupRoleAssignment? Backup)
{
    public void Validate()
    {
        Starter?.Validate();
        Backup?.Validate();
    }
}

public sealed record CoachLineupRecommendation(
    string RecommendationId,
    CoachLineupRecommendationType RecommendationType,
    string? PersonId,
    string PlayerName,
    string Reason,
    string SuggestedAction,
    bool IsImportant)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecommendationId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(SuggestedAction))
        {
            throw new ArgumentException("Coach lineup recommendation requires readable context.");
        }
    }
}

public sealed record LineupDevelopmentImpact(
    string PersonId,
    LineupRole CurrentRole,
    int Modifier,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Lineup development impact requires player id and summary.");
        }

        if (Modifier is < -10 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(Modifier), "Lineup development modifier must stay modest.");
        }
    }
}

public sealed record Lineup(
    string LineupId,
    string OrganizationId,
    string OrganizationName,
    DateOnly CreatedOn,
    IReadOnlyList<ForwardLine> ForwardLines,
    IReadOnlyList<DefensePair> DefensePairs,
    GoalieDepth Goalies,
    IReadOnlyList<LineupRoleAssignment> Assignments,
    IReadOnlyList<CoachLineupRecommendation> CoachRecommendations,
    string Summary)
{
    public IReadOnlyList<PlayerLineupUsage> Usage { get; init; } = Array.Empty<PlayerLineupUsage>();

    public IReadOnlyList<PlayerRoleExpectation> RoleExpectations { get; init; } = Array.Empty<PlayerRoleExpectation>();

    public IReadOnlyList<PlayerRolePromise> RolePromises { get; init; } = Array.Empty<PlayerRolePromise>();

    public IReadOnlyList<string> RoleHistory { get; init; } = Array.Empty<string>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LineupId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Lineup requires identity and summary.");
        }

        if (ForwardLines.Count != 4)
        {
            throw new ArgumentException("Lineup must include four forward lines.", nameof(ForwardLines));
        }

        if (DefensePairs.Count != 3)
        {
            throw new ArgumentException("Lineup must include three defense pairs.", nameof(DefensePairs));
        }

        foreach (var line in ForwardLines)
        {
            line.Validate();
        }

        foreach (var pair in DefensePairs)
        {
            pair.Validate();
        }

        Goalies.Validate();
        foreach (var assignment in Assignments)
        {
            assignment.Validate();
        }

        foreach (var recommendation in CoachRecommendations)
        {
            recommendation.Validate();
        }

        foreach (var usage in Usage)
        {
            usage.Validate();
        }

        foreach (var expectation in RoleExpectations)
        {
            expectation.Validate();
        }

        foreach (var promise in RolePromises)
        {
            promise.Validate();
        }
    }
}

public static class LineupDisplay
{
    public static string Role(LineupRole role) =>
        role switch
        {
            LineupRole.FranchiseForward => "Franchise Forward",
            LineupRole.FirstLineForward => "First Line Forward",
            LineupRole.TopSixForward => "Top Six Forward",
            LineupRole.MiddleSixForward => "Middle Six Forward",
            LineupRole.CheckingLineForward => "Checking Line Forward",
            LineupRole.FourthLineForward => "Fourth Line Forward",
            LineupRole.DepthForward => "Depth Forward",
            LineupRole.ProspectForward => "Prospect Forward",
            LineupRole.FranchiseDefenseman => "Franchise Defenseman",
            LineupRole.TopPairDefenseman => "Top Pair Defenseman",
            LineupRole.SecondPairDefenseman => "Second Pair Defenseman",
            LineupRole.ThirdPairDefenseman => "Third Pair Defenseman",
            LineupRole.DepthDefenseman => "Depth Defenseman",
            LineupRole.ProspectDefenseman => "Prospect Defenseman",
            LineupRole.FranchiseGoalie => "Franchise Goalie",
            LineupRole.StartingGoalie => "Starting Goalie",
            LineupRole.TandemGoalie => "Tandem Goalie",
            LineupRole.BackupGoalie => "Backup Goalie",
            LineupRole.DepthGoalie => "Depth Goalie",
            LineupRole.ProspectGoalie => "Prospect Goalie",
            _ => role.ToString()
        };

    public static string SlotLabel(LineupSlot slot) =>
        slot switch
        {
            LineupSlot.Line1LW => "Line 1 LW",
            LineupSlot.Line1C => "Line 1 C",
            LineupSlot.Line1RW => "Line 1 RW",
            LineupSlot.Line2LW => "Line 2 LW",
            LineupSlot.Line2C => "Line 2 C",
            LineupSlot.Line2RW => "Line 2 RW",
            LineupSlot.Line3LW => "Line 3 LW",
            LineupSlot.Line3C => "Line 3 C",
            LineupSlot.Line3RW => "Line 3 RW",
            LineupSlot.Line4LW => "Line 4 LW",
            LineupSlot.Line4C => "Line 4 C",
            LineupSlot.Line4RW => "Line 4 RW",
            LineupSlot.Pair1LD => "Pair 1 LD",
            LineupSlot.Pair1RD => "Pair 1 RD",
            LineupSlot.Pair2LD => "Pair 2 LD",
            LineupSlot.Pair2RD => "Pair 2 RD",
            LineupSlot.Pair3LD => "Pair 3 LD",
            LineupSlot.Pair3RD => "Pair 3 RD",
            LineupSlot.Starter => "Starter",
            LineupSlot.Backup => "Backup",
            LineupSlot.HealthyScratch => "Healthy Scratch",
            _ => slot.ToString()
        };

    public static string Position(LineupPosition position) =>
        position switch
        {
            LineupPosition.LeftWing => "LW",
            LineupPosition.Center => "C",
            LineupPosition.RightWing => "RW",
            LineupPosition.LeftDefense => "LD",
            LineupPosition.RightDefense => "RD",
            LineupPosition.StarterGoalie => "Starter",
            LineupPosition.BackupGoalie => "Backup",
            LineupPosition.HealthyScratch => "Scratch",
            _ => position.ToString()
        };
}
