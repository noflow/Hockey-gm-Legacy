using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public enum StaffLifeStage
{
    Prospect,
    Assistant,
    Established,
    Respected,
    Elite,
    Veteran,
    NearRetirement,
    Retired
}

public enum StaffReputationCategory
{
    Unknown,
    Promising,
    Respected,
    Elite,
    Legendary
}

public enum StaffCareerPhase
{
    Learning,
    Rising,
    Established,
    Peak,
    Rebuilding,
    Mentor,
    Declining,
    Transition
}

public enum StaffMilestoneType
{
    Hired,
    Promoted,
    Released,
    FirstHeadRole,
    Years5,
    Years10,
    Years20,
    PlayerDeveloped,
    ProspectDiscovered,
    StaffTreeExpanded,
    ChampionshipPlaceholder,
    Retirement
}

public sealed record StaffReputation(
    string PersonId,
    StaffReputationCategory Category,
    int Score,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff reputation requires person id and summary.");
        }

        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Staff reputation score must be between zero and one hundred.");
        }
    }
}

public sealed record StaffMilestone(
    string MilestoneId,
    string PersonId,
    string StaffName,
    StaffMilestoneType MilestoneType,
    DateOnly Date,
    int SeasonYear,
    string Summary,
    bool IsNotable)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MilestoneId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(StaffName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff milestone requires identity and summary.");
        }

        if (SeasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SeasonYear), "Staff milestone season year must be positive.");
        }
    }
}

public sealed record StaffCareerProgression(
    string PersonId,
    StaffCareerPhase Phase,
    int TrendScore,
    DateOnly EvaluatedOn,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff career progression requires person id and summary.");
        }

        if (TrendScore is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(TrendScore), "Staff career trend must be between -100 and 100.");
        }
    }
}

public sealed record StaffCareerState(
    string PersonId,
    string StaffName,
    StaffRole CurrentRole,
    StaffDepartment Department,
    StaffLifeStage LifeStage,
    StaffCareerPhase CareerPhase,
    StaffReputation Reputation,
    int Age,
    int YearsExperience,
    int LegacyScore,
    string CurrentOrganization,
    string Summary)
{
    public void Validate()
    {
        Reputation.Validate();
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(StaffName)
            || string.IsNullOrWhiteSpace(CurrentOrganization)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Staff career state requires identity, organization, and summary.");
        }

        if (Age < 0 || YearsExperience < 0 || LegacyScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Staff career state values cannot be negative.");
        }
    }
}

public sealed record StaffCareerSummary(
    string PersonId,
    string StaffName,
    StaffRole CurrentRole,
    StaffDepartment Department,
    StaffLifeStage LifeStage,
    StaffCareerPhase CareerPhase,
    StaffReputationCategory Reputation,
    int LegacyScore,
    string CareerSummaryText,
    IReadOnlyList<string> CareerStory,
    IReadOnlyList<StaffMilestone> Milestones,
    IReadOnlyList<string> Organizations,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> SalaryHistory,
    IReadOnlyList<string> PlayersDeveloped,
    IReadOnlyList<string> PlayersDiscovered,
    IReadOnlyList<string> CoachingTree,
    IReadOnlyList<string> Relationships,
    string PersonalLegacy,
    string PromotionReadiness,
    string ConcernSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(StaffName)
            || string.IsNullOrWhiteSpace(CareerSummaryText)
            || string.IsNullOrWhiteSpace(PersonalLegacy)
            || string.IsNullOrWhiteSpace(PromotionReadiness)
            || string.IsNullOrWhiteSpace(ConcernSummary))
        {
            throw new ArgumentException("Staff career summary requires readable career context.");
        }

        if (LegacyScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LegacyScore), "Staff legacy score cannot be negative.");
        }

        foreach (var milestone in Milestones)
        {
            milestone.Validate();
        }
    }
}
