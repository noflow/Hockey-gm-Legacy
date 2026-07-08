namespace LegacyEngine.Integration;

public enum PowerPlaySlot
{
    LeftWing,
    Center,
    RightWing,
    QuarterbackDefense,
    NetFrontOrSecondDefense
}

public enum PenaltyKillSlot
{
    LeftWing,
    RightWing,
    LeftDefense,
    RightDefense
}

public enum GameUsageRecommendationType
{
    MovePlayerToPowerPlayOne,
    UseVeteranOnPenaltyKill,
    ReduceGoalieWorkload,
    PromoteYoungPlayerToPowerPlayTwo,
    RemoveTiredPlayer,
    ImprovePowerPlayBalance,
    ImprovePenaltyKillBalance
}

public sealed record PowerPlayUnit(
    int UnitNumber,
    LineupRoleAssignment? LeftWing,
    LineupRoleAssignment? Center,
    LineupRoleAssignment? RightWing,
    LineupRoleAssignment? QuarterbackDefense,
    LineupRoleAssignment? NetFrontOrSecondDefense)
{
    public IReadOnlyList<LineupRoleAssignment> Players =>
        new[] { LeftWing, Center, RightWing, QuarterbackDefense, NetFrontOrSecondDefense }
            .Where(player => player is not null)
            .Select(player => player!)
            .ToArray();

    public void Validate()
    {
        if (UnitNumber is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(UnitNumber), "Power play unit number must be 1 or 2.");
        }

        foreach (var player in Players)
        {
            player.Validate();
        }
    }
}

public sealed record PenaltyKillUnit(
    int UnitNumber,
    LineupRoleAssignment? LeftWing,
    LineupRoleAssignment? RightWing,
    LineupRoleAssignment? LeftDefense,
    LineupRoleAssignment? RightDefense)
{
    public IReadOnlyList<LineupRoleAssignment> Players =>
        new[] { LeftWing, RightWing, LeftDefense, RightDefense }
            .Where(player => player is not null)
            .Select(player => player!)
            .ToArray();

    public void Validate()
    {
        if (UnitNumber is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(UnitNumber), "Penalty kill unit number must be 1 or 2.");
        }

        foreach (var player in Players)
        {
            player.Validate();
        }
    }
}

public sealed record ExtraAttackerUnit(
    string UnitId,
    IReadOnlyList<LineupRoleAssignment> Players,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UnitId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Extra attacker unit requires identity and summary.");
        }

        if (Players.Count is < 1 or > 6)
        {
            throw new ArgumentException("Extra attacker unit should include one to six players.", nameof(Players));
        }

        foreach (var player in Players)
        {
            player.Validate();
        }
    }
}

public sealed record ThreeOnThreeUnit(
    string UnitId,
    string Combination,
    IReadOnlyList<LineupRoleAssignment> Players,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UnitId) || string.IsNullOrWhiteSpace(Combination) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Three-on-three unit requires identity and summary.");
        }

        if (Players.Count is < 1 or > 3)
        {
            throw new ArgumentException("Three-on-three unit should include one to three players.", nameof(Players));
        }

        foreach (var player in Players)
        {
            player.Validate();
        }
    }
}

public sealed record ShootoutOrder(
    IReadOnlyList<LineupRoleAssignment> Shooters,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Shootout order requires a summary.", nameof(Summary));
        }

        if (Shooters.Count == 0)
        {
            throw new ArgumentException("Shootout order requires at least one shooter.", nameof(Shooters));
        }

        foreach (var shooter in Shooters)
        {
            shooter.Validate();
        }
    }
}

public sealed record GoalieUsageProfile(
    string PersonId,
    string PlayerName,
    string UsageRole,
    int GamesStarted,
    int ExpectedStarts,
    string Workload,
    string RestRecommendation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(UsageRole)
            || string.IsNullOrWhiteSpace(Workload)
            || string.IsNullOrWhiteSpace(RestRecommendation))
        {
            throw new ArgumentException("Goalie usage profile requires readable context.");
        }

        if (GamesStarted < 0 || ExpectedStarts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(GamesStarted), "Goalie starts cannot be negative.");
        }
    }
}

public sealed record GameUsageProfile(
    string PersonId,
    string PlayerName,
    string CurrentLine,
    string PowerPlayUsage,
    string PenaltyKillUsage,
    string ExtraAttackerUsage,
    string ThreeOnThreeUsage,
    string ShootoutUsage,
    LineupRole Role,
    int DevelopmentModifier,
    string UsageSummary,
    string CoachComment)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(CurrentLine)
            || string.IsNullOrWhiteSpace(PowerPlayUsage)
            || string.IsNullOrWhiteSpace(PenaltyKillUsage)
            || string.IsNullOrWhiteSpace(ExtraAttackerUsage)
            || string.IsNullOrWhiteSpace(ThreeOnThreeUsage)
            || string.IsNullOrWhiteSpace(ShootoutUsage)
            || string.IsNullOrWhiteSpace(UsageSummary)
            || string.IsNullOrWhiteSpace(CoachComment))
        {
            throw new ArgumentException("Game usage profile requires readable player context.");
        }

        if (DevelopmentModifier is < -10 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(DevelopmentModifier), "Game usage development modifier must stay modest.");
        }
    }
}

public sealed record GameUsageCoachRecommendation(
    string RecommendationId,
    GameUsageRecommendationType RecommendationType,
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
            throw new ArgumentException("Game usage recommendation requires readable context.");
        }
    }
}

public sealed record SpecialTeams(
    IReadOnlyList<PowerPlayUnit> PowerPlayUnits,
    IReadOnlyList<PenaltyKillUnit> PenaltyKillUnits,
    ExtraAttackerUnit ExtraAttacker,
    ThreeOnThreeUnit ThreeOnThree,
    ShootoutOrder ShootoutOrder)
{
    public void Validate()
    {
        if (PowerPlayUnits.Count != 2)
        {
            throw new ArgumentException("Special teams requires two power play units.", nameof(PowerPlayUnits));
        }

        if (PenaltyKillUnits.Count != 2)
        {
            throw new ArgumentException("Special teams requires two penalty kill units.", nameof(PenaltyKillUnits));
        }

        foreach (var unit in PowerPlayUnits)
        {
            unit.Validate();
        }

        foreach (var unit in PenaltyKillUnits)
        {
            unit.Validate();
        }

        ExtraAttacker.Validate();
        ThreeOnThree.Validate();
        ShootoutOrder.Validate();
    }
}

public sealed record GameUsage(
    string GameUsageId,
    string OrganizationId,
    DateOnly CreatedOn,
    SpecialTeams SpecialTeams,
    IReadOnlyList<GoalieUsageProfile> GoalieUsage,
    IReadOnlyList<GameUsageProfile> PlayerProfiles,
    IReadOnlyList<GameUsageCoachRecommendation> CoachRecommendations,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GameUsageId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Game usage requires identity and summary.");
        }

        SpecialTeams.Validate();
        foreach (var goalie in GoalieUsage)
        {
            goalie.Validate();
        }

        foreach (var profile in PlayerProfiles)
        {
            profile.Validate();
        }

        foreach (var recommendation in CoachRecommendations)
        {
            recommendation.Validate();
        }
    }
}

public sealed record GameUsageManagementResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Game usage management result requires a message.");
        }
    }
}
