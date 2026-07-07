using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record CoachingStaffProfile(
    string PersonId,
    string StaffName,
    StaffRole Role,
    StaffDepartment Department,
    CoachPhilosophy Philosophy,
    IReadOnlyList<CoachSpecialty> Specialties,
    CoachPersonality Personality,
    string PhilosophySummary,
    string PlayerDevelopmentImpact,
    string RosterRecommendationStyle,
    string CareerSummary,
    string CurrentResponsibilities)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(StaffName)
            || string.IsNullOrWhiteSpace(PhilosophySummary)
            || string.IsNullOrWhiteSpace(PlayerDevelopmentImpact)
            || string.IsNullOrWhiteSpace(RosterRecommendationStyle)
            || string.IsNullOrWhiteSpace(CareerSummary)
            || string.IsNullOrWhiteSpace(CurrentResponsibilities))
        {
            throw new ArgumentException("Coaching staff profile requires identity and summary text.");
        }

        if (Specialties.Count == 0)
        {
            throw new ArgumentException("Coaching staff profile requires at least one specialty.", nameof(Specialties));
        }
    }
}
