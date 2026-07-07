using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed record ScoutIntelligenceProfile(
    string ScoutPersonId,
    string Name,
    string Role,
    IReadOnlyList<ScoutPersonalityTrait> Traits,
    IReadOnlyList<ScoutingRegionFocus> KnownRegions,
    IReadOnlyList<ScoutSpecialty> Specialties,
    int Reputation,
    int ExperiencePoints,
    int Workload,
    string BudgetSupport,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScoutPersonId)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Role)
            || string.IsNullOrWhiteSpace(BudgetSupport)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Scout intelligence profile requires readable identity and summary.");
        }

        if (Traits.Count == 0 || KnownRegions.Count == 0 || Specialties.Count == 0)
        {
            throw new ArgumentException("Scout intelligence profile requires traits, known regions, and specialties.");
        }
    }
}
