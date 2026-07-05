using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed record DraftRightsRecord(
    string ProspectPersonId,
    string ProspectName,
    int Age,
    RosterPosition Position,
    int RoundNumber,
    int PickNumber,
    ProspectStatus Status,
    string ProjectionText,
    ScoutingConfidenceLevel? ScoutingConfidence,
    string GmNotes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProspectPersonId))
        {
            throw new ArgumentException("Prospect person id is required.", nameof(ProspectPersonId));
        }

        if (string.IsNullOrWhiteSpace(ProspectName))
        {
            throw new ArgumentException("Prospect name is required.", nameof(ProspectName));
        }

        if (Age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Prospect age cannot be negative.");
        }

        if (RoundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RoundNumber), "Draft round must be positive.");
        }

        if (PickNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PickNumber), "Draft pick must be positive.");
        }

        if (string.IsNullOrWhiteSpace(ProjectionText))
        {
            throw new ArgumentException("Projection text is required.", nameof(ProjectionText));
        }
    }
}
