using LegacyEngine.Recruiting;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed record RecruitingV2Profile(
    string RecruitPersonId,
    string Name,
    string Position,
    int? Age,
    string RegionOrHometown,
    string CurrentTeam,
    RecruitStatus Status,
    int InterestLevel,
    IReadOnlyDictionary<RecruitPriority, int> Priorities,
    IReadOnlyDictionary<RecruitingFamilyPriority, int> FamilyPriorities,
    RecruitingDecisionStyle DecisionStyle,
    int RelationshipWithGm,
    ScoutingConfidenceLevel ScoutingConfidence,
    string ProjectionSummary,
    string RiskSummary,
    IReadOnlyList<string> CurrentOffers,
    IReadOnlyList<RecruitingCompetingTeam> CompetingTeams,
    string WhyTheyAreInterested,
    string WhyTheyMayChooseUs,
    string WhyTheyMayRejectUs,
    IReadOnlyList<string> PromisesMade,
    string GmNotes)
{
    public RecruitingCompetingTeam? TopCompetitor =>
        CompetingTeams.OrderByDescending(team => team.InterestStrength).FirstOrDefault();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecruitPersonId) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Recruiting profile requires a recruit id and name.", nameof(RecruitPersonId));
        }

        if (string.IsNullOrWhiteSpace(Position) || string.IsNullOrWhiteSpace(RegionOrHometown) || string.IsNullOrWhiteSpace(CurrentTeam))
        {
            throw new ArgumentException("Recruiting profile requires position, hometown, and current team context.", nameof(Position));
        }

        if (InterestLevel is < 0 or > 100 || RelationshipWithGm is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(InterestLevel), "Recruiting profile scores must be between 0 and 100.");
        }

        if (Priorities.Count == 0 || FamilyPriorities.Count == 0 || CompetingTeams.Count == 0)
        {
            throw new ArgumentException("Recruiting profile requires priorities, family priorities, and competing teams.", nameof(Priorities));
        }

        foreach (var team in CompetingTeams)
        {
            team.Validate();
        }
    }
}
