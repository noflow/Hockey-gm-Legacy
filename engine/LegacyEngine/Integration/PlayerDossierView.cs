using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed record PlayerDossierView(
    string PersonId,
    string PlayerName,
    int Age,
    RosterPosition Position,
    string Status,
    string TeamOrRights,
    PlayerDossierSource Source,
    IReadOnlyList<PlayerDossierSection> Sections,
    string GmNotes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Dossier person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            throw new ArgumentException("Dossier player name is required.", nameof(PlayerName));
        }

        if (Age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Dossier age cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(Status))
        {
            throw new ArgumentException("Dossier status is required.", nameof(Status));
        }

        if (string.IsNullOrWhiteSpace(TeamOrRights))
        {
            throw new ArgumentException("Dossier team or rights value is required.", nameof(TeamOrRights));
        }

        foreach (var section in Sections)
        {
            section.Validate();
        }
    }
}
