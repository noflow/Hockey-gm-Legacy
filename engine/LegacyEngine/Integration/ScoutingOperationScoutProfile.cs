namespace LegacyEngine.Integration;

public sealed record ScoutingOperationScoutProfile(
    string ScoutPersonId,
    string Name,
    string Role,
    string RegionSpecialty,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    int Reputation,
    int RelationshipWithGm,
    string CurrentAssignment,
    int Workload,
    string ConflictWarning)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScoutPersonId) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Scout profile requires identity.", nameof(ScoutPersonId));
        }

        if (string.IsNullOrWhiteSpace(Role) || string.IsNullOrWhiteSpace(RegionSpecialty) || string.IsNullOrWhiteSpace(CurrentAssignment))
        {
            throw new ArgumentException("Scout profile requires role, specialty, and assignment text.");
        }
    }
}
