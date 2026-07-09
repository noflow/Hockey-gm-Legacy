using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum PlayerRatingCategory
{
    Offensive,
    Defensive,
    Skating,
    Physical,
    Skill,
    Mental,
    Team,
    Goalie
}

public enum PlayerRatingColor
{
    Unknown,
    Red,
    Green,
    Blue,
    Black
}

public enum PlayerRatingSource
{
    Unknown,
    Scout,
    Staff,
    DevelopmentReport,
    SeasonReview,
    InternalTeamKnowledge
}

public enum PlayerAttributeKey
{
    Shooting,
    Playmaking,
    PuckSkill,
    HockeyIq,
    Vision,
    OffensiveAwareness,
    Creativity,
    Positioning,
    StickChecking,
    ShotBlocking,
    DefensiveAwareness,
    Backchecking,
    BoardPlay,
    Speed,
    Acceleration,
    Agility,
    EdgeWork,
    Balance,
    Endurance,
    Strength,
    Hitting,
    Aggression,
    Grit,
    Toughness,
    Durability,
    Passing,
    HandEye,
    Faceoffs,
    Deception,
    PuckProtection,
    Stickhandling,
    Leadership,
    Consistency,
    Coachability,
    WorkEthic,
    Composure,
    Confidence,
    TeamPlay,
    Adaptability,
    Professionalism,
    LockerRoom,
    MentorAbility,
    Reflexes,
    Glove,
    Blocker,
    ReboundManagement,
    PuckTracking,
    LateralMovement,
    PuckHandling,
    Recovery
}

public sealed record PlayerRatingRange(int? Low, int? High)
{
    public static PlayerRatingRange Unknown { get; } = new(null, null);

    public bool IsUnknown => Low is null || High is null;

    public bool IsExact => !IsUnknown && Low == High;

    public int Midpoint => IsUnknown ? 0 : (Low!.Value + High!.Value) / 2;

    public string Display => IsUnknown ? "???" : IsExact ? Low!.Value.ToString() : $"{Low}-{High}";

    public void Validate()
    {
        if (IsUnknown)
        {
            return;
        }

        if (Low is < 0 or > 100 || High is < 0 or > 100 || Low > High)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerRatingRange), "Rating ranges must stay within 0-100.");
        }
    }
}

public sealed record PlayerAttributeRating(PlayerRatingCategory Category, PlayerAttributeKey Attribute, int Value)
{
    public void Validate()
    {
        if (Value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Value), "True attribute ratings must stay within 0-100.");
        }
    }
}

public sealed record PlayerScoutedAttributeRating(
    PlayerRatingCategory Category,
    PlayerAttributeKey Attribute,
    PlayerRatingRange Range,
    PlayerRatingColor ConfidenceColor,
    PlayerRatingSource Source,
    DateOnly? LastUpdated,
    string ScoutNote)
{
    public void Validate()
    {
        Range.Validate();
        if (string.IsNullOrWhiteSpace(ScoutNote))
        {
            throw new ArgumentException("Scouted attribute requires a scout note.", nameof(ScoutNote));
        }
    }
}

public sealed record PlayerTrueRatings(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int Overall,
    int Potential,
    string RoleArchetype,
    IReadOnlyList<PlayerAttributeRating> Attributes,
    DateOnly LastUpdated)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(RoleArchetype))
        {
            throw new ArgumentException("True ratings require player identity and archetype.");
        }

        if (Overall is < 0 or > 100 || Potential is < 0 or > 100 || Potential < Overall)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerTrueRatings), "True overall/potential must stay within 0-100 and potential cannot be below overall.");
        }

        foreach (var attribute in Attributes)
        {
            attribute.Validate();
        }
    }
}

public sealed record PlayerScoutedRatings(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    PlayerRatingRange Overall,
    PlayerRatingRange Potential,
    PlayerRatingColor ConfidenceColor,
    PlayerRatingSource Source,
    DateOnly? LastScoutedDate,
    string ScoutSource,
    string ScoutNote,
    IReadOnlyList<PlayerScoutedAttributeRating> Attributes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(ScoutSource) || string.IsNullOrWhiteSpace(ScoutNote))
        {
            throw new ArgumentException("Scouted ratings require player identity and source note.");
        }

        Overall.Validate();
        Potential.Validate();
        foreach (var attribute in Attributes)
        {
            attribute.Validate();
        }
    }
}
