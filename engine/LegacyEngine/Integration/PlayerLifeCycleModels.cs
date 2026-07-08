using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum PlayerLifeStage
{
    Youth,
    Junior,
    Prospect,
    DevelopingProfessional,
    NhlRegular,
    Prime,
    Veteran,
    Declining,
    Retired,
    HallOfFameCandidate
}

public enum PlayerCareerPhase
{
    Developing,
    Breakout,
    Plateau,
    Prime,
    Regression,
    LateBloomer,
    BustRisk,
    VeteranLeadership,
    CareerRevival,
    CareerDecline
}

public enum PlayerReputationCategory
{
    Unknown,
    Prospect,
    Reliable,
    EmergingStar,
    Star,
    Elite,
    Superstar,
    FranchisePlayer,
    VeteranLeader,
    DecliningVeteran
}

public enum PlayerMilestoneType
{
    Drafted,
    SignedFirstContract,
    JuniorChampionship,
    AhlDebut,
    NhlDebut,
    FirstNhlGoal,
    FirstNhlPoint,
    FirstShutout,
    FirstCaptaincy,
    Games100,
    Games250,
    Games500,
    Games750,
    Games1000,
    Goals100,
    Goals250,
    Goals500,
    PlayoffDebut,
    Championship,
    Retirement
}

public enum PlayerAchievementType
{
    TeamCaptain,
    AlternateCaptain,
    MostImproved,
    RookieLeader,
    IronMan,
    TopScorer,
    TeamMvp,
    VeteranLeader,
    BreakoutSeason,
    ComebackSeason
}

public sealed record PlayerReputation(
    string PersonId,
    PlayerReputationCategory Category,
    int Score,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player reputation requires person id and summary.");
        }

        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Player reputation score must be between 0 and 100.");
        }
    }
}

public sealed record PlayerMilestone(
    string MilestoneId,
    string PersonId,
    string PlayerName,
    PlayerMilestoneType MilestoneType,
    DateOnly Date,
    int SeasonYear,
    string Summary,
    bool IsNotable)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MilestoneId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player milestone requires identity and summary.");
        }

        if (SeasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Milestone season year must be positive.");
        }
    }
}

public sealed record PlayerAchievement(
    string AchievementId,
    string PersonId,
    string PlayerName,
    PlayerAchievementType AchievementType,
    DateOnly Date,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AchievementId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player achievement requires identity and summary.");
        }
    }
}

public sealed record PlayerCareerProgression(
    string PersonId,
    PlayerCareerPhase Phase,
    int TrendScore,
    DateOnly EvaluatedOn,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player career progression requires person id and summary.");
        }

        if (TrendScore is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(TrendScore), "Career trend score must be between -100 and 100.");
        }
    }
}

public sealed record PlayerCareerState(
    string PersonId,
    string PlayerName,
    int Age,
    RosterPosition Position,
    PlayerLifeStage LifeStage,
    PlayerCareerPhase CareerPhase,
    PlayerReputation Reputation,
    int GamesPlayed,
    int Goals,
    int Assists,
    int Points,
    int LegacyScore,
    string CurrentTeam,
    string CurrentLeague,
    string Summary)
{
    public void Validate()
    {
        Reputation.Validate();
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(CurrentTeam)
            || string.IsNullOrWhiteSpace(CurrentLeague)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Player career state requires identity, current context, and summary.");
        }

        if (Age < 0 || GamesPlayed < 0 || Goals < 0 || Assists < 0 || Points < 0 || LegacyScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Player career state values cannot be negative.");
        }
    }
}

public sealed record PlayerCareerSummary(
    string PersonId,
    string PlayerName,
    PlayerLifeStage LifeStage,
    PlayerCareerPhase CareerPhase,
    PlayerReputationCategory Reputation,
    int LegacyScore,
    string CareerSummaryText,
    IReadOnlyList<string> CareerStory,
    IReadOnlyList<PlayerMilestone> Milestones,
    IReadOnlyList<PlayerAchievement> Achievements,
    IReadOnlyList<string> InfluentialStaff,
    string CoachComment,
    string ScoutComment,
    string MedicalSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(CareerSummaryText)
            || string.IsNullOrWhiteSpace(CoachComment)
            || string.IsNullOrWhiteSpace(ScoutComment)
            || string.IsNullOrWhiteSpace(MedicalSummary))
        {
            throw new ArgumentException("Player career summary requires readable career context.");
        }

        if (LegacyScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LegacyScore), "Legacy score cannot be negative.");
        }

        foreach (var milestone in Milestones)
        {
            milestone.Validate();
        }

        foreach (var achievement in Achievements)
        {
            achievement.Validate();
        }
    }
}
